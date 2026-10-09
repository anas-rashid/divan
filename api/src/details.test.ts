import { test, after } from 'node:test';
import assert from 'node:assert/strict';
import { mkdtemp, readFile, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { execFileSync } from 'node:child_process';
import Fastify from 'fastify';
import { authRoutes } from './auth.ts';
import { adminRoutes } from './admin.ts';
import { permissionRoutes } from './permissions.ts';
import { moderationRoutes } from './moderation.ts';
import { checkDetails, parseDetails } from './details.ts';
import { pool } from './db.ts';

after(() => pool.end());

test('details text: fields, the intro to the end, checks', () => {
  const d = parseDetails('نام: محمد اقبال\nتخلص: اقبال\nپیدائش: ۱۸۷۷\nوفات: 1938\nتعارف:\nپہلی سطر\n\nدوسری: سطر');
  assert.equal(d['تعارف'], 'پہلی سطر\n\nدوسری: سطر', 'a colon in the intro is text');
  assert.equal(checkDetails('poet', 'نام: محمد اقبال\nتخلص: اقبال\nپیدائش: ۱۸۷۷\nوفات: 1938\nتعارف:\nx'), null, 'Urdu digits are years');
  assert.match(checkDetails('poet', 'نام: \nتخلص: اقبال')!, /ضروری/);
  assert.match(checkDetails('poet', 'نام: الف\nتخلص: ب\nپیدائش: 1900\nوفات: 1800')!, /پہلے/);
  assert.match(checkDetails('poet', 'نام: الف\nتخلص: ب\nپیدائش: سن')!, /عیسوی/);
  assert.match(checkDetails('book', 'عنوان:')!, /ضروری/);
});

test('poet and book editors: permissions, publishing to divan-data and the site, history and revert', async () => {
  const data = await mkdtemp(join(tmpdir(), 'divan-data-'));
  process.env.DIVAN_DATA_DIR = data;
  const git = (...a: string[]) => execFileSync('git', ['-C', data, ...a], { encoding: 'utf8' });
  git('init', '-q'); git('-c', 'user.name=t', '-c', 'user.email=t@t', 'commit', '-q', '--allow-empty', '-m', 'start');
  const app = Fastify();
  authRoutes(app); adminRoutes(app); permissionRoutes(app); moderationRoutes(app);
  const run = Date.now();
  const call = (method: string, url: string, body?: object, token?: string) =>
    app.inject({ method: method as any, url, payload: body, headers: { ...(token && { authorization: `Bearer ${token}` }), 'x-client-ip': `det-${run}` } });
  const person = async (name: string, role: string) => {
    const s = (await call('POST', '/api/auth/signup', { email: `${name}-${run}@divan.test`, password: 'pass-word-1' })).json();
    await pool.query('UPDATE users SET role = $2 WHERE id = $1', [s.user.id, role]);
    return { ...s, id: s.user.id };
  };
  const admin = await person('admin', 'admin'), l2 = await person('l2', 'mod-l2');
  const poet = (await pool.query(`SELECT * FROM poets WHERE url = '/p266'`)).rows[0];
  const root = (await pool.query(`SELECT id, title FROM categories WHERE poet_id = $1 AND parent_id IS NULL`, [poet.id])).rows[0];
  const book = (await pool.query(`SELECT id, url, title FROM categories WHERE url = '/p266/ghazal'`)).rows[0];
  try {
    assert.equal((await call('POST', `/api/mod/details/poet/${poet.id}/draft`, undefined, l2.token)).statusCode, 403, 'no grant yet');
    await call('POST', `/api/admin/users/${l2.id}/grants`, { scope: 'poet', target: '/p266', content: ['poets', 'books'], actions: ['edit'] }, admin.token);
    const can = (await call('GET', `/api/mod/can?category=${book.id}`, undefined, l2.token)).json();
    assert.deepEqual([can.poet, can.book], [true, true]);

    // the poet: L2 drafts a new intro and years; the admin publishes
    const cur = (await call('GET', `/api/mod/details/poet/${poet.id}`, undefined, l2.token)).json();
    assert.equal(cur.fields['تخلص'], poet.nickname);
    const id = (await call('POST', `/api/mod/details/poet/${poet.id}/draft`, undefined, l2.token)).json().id;
    const text = `نام: ${poet.name}\nتخلص: ${poet.nickname}\nپیدائش: 1797\nوفات: 1869\nتعارف:\nنیا تعارف\nدوسرا پیراگراف`;
    assert.equal((await call('POST', `/api/mod/revisions/${id}/save`, { content: 'نام:\nتخلص: x' }, l2.token)).statusCode, 400);
    assert.equal((await call('POST', `/api/mod/revisions/${id}/save`, { content: text, summary: 'تعارف' }, l2.token)).statusCode, 200);
    await pool.query(`UPDATE revisions SET status = 'approved' WHERE id = $1`, [id]); // the L1 step is tested in moderation.test.ts
    assert.deepEqual((await call('POST', `/api/mod/revisions/${id}/publish`, {}, admin.token)).json(), { status: 'published', version: 1 });
    const now = (await pool.query('SELECT description, birth_year_ce, death_year_ce, birth_year_ah FROM poets WHERE id = $1', [poet.id])).rows[0];
    assert.deepEqual([now.description, now.birth_year_ce, now.death_year_ce, now.birth_year_ah], ['نیا تعارف\nدوسرا پیراگراف', 1797, 1869, 1212]);
    assert.match(await readFile(join(data, 'divan', 'p266.poet'), 'utf8'), /^نام: .+\nتخلص: .+\nپیدائش: 1797\nوفات: 1869\nتعارف:\nنیا تعارف/);
    assert.match(git('log', '-1', '--format=%s'), new RegExp(`^شاعر: ${poet.nickname}: تعارف \\(ورژن 1\\)`));

    // the book's title, by the admin directly
    const b = (await call('POST', `/api/mod/details/book/${book.id}/draft`, undefined, admin.token)).json().id;
    await call('POST', `/api/mod/revisions/${b}/save`, { content: 'عنوان: غزلیات (نیا)' }, admin.token);
    assert.equal((await call('POST', `/api/mod/revisions/${b}/submit`, {}, admin.token)).json().version, 1);
    assert.equal((await pool.query('SELECT title FROM categories WHERE id = $1', [book.id])).rows[0].title, 'غزلیات (نیا)');
    assert.equal((await readFile(join(data, 'divan', 'p266', 'ghazal.book'), 'utf8')), 'عنوان: غزلیات (نیا)\n');

    // history: compare with the text before Divan's version, and bring it back as a draft
    const cmp = (await call('GET', `/api/mod/compare/poet/${poet.id}?a=0&b=1`, undefined, l2.token)).json();
    assert.ok(cmp.diff.some((d: any) => d.op === '+' && d.text === 'نیا تعارف'));
    const back = (await call('POST', `/api/mod/revert/book/${book.id}`, { version: 0 }, l2.token)).json().id;
    assert.equal((await call('GET', `/api/mod/revisions/${back}`, undefined, l2.token)).json().revision.content, `عنوان: ${book.title}`);
    const log = (await call('GET', `/api/mod/log?who=l2-${run}`, undefined, admin.token)).json().entries;
    assert.ok(log.some((e: any) => e.entity === 'poet' && e.title === poet.nickname && e.url === '/p266'), 'the log names the poet');
  } finally {
    await pool.query(`UPDATE poets SET name = $2, nickname = $3, description = $4, birth_year_ce = $5, death_year_ce = $6, birth_year_ah = $7, death_year_ah = $8 WHERE id = $1`,
      [poet.id, poet.name, poet.nickname, poet.description, poet.birth_year_ce, poet.death_year_ce, poet.birth_year_ah, poet.death_year_ah]);
    await pool.query('UPDATE categories SET title = $2 WHERE id = $1', [root.id, root.title]);
    await pool.query('UPDATE categories SET title = $2 WHERE id = $1', [book.id, book.title]);
    await pool.query(`DELETE FROM revisions WHERE entity IN ('poet', 'book') AND author_email LIKE $1`, [`%-${run}@divan.test`]);
    await pool.query('DELETE FROM users WHERE email LIKE $1', [`%-${run}@divan.test`]);
    await pool.query('DELETE FROM audit_log WHERE actor_email LIKE $1 OR target_email LIKE $1', [`%-${run}@divan.test`]);
    await rm(data, { recursive: true, force: true });
    await app.close();
  }
});
