import { test, after } from 'node:test';
import assert from 'node:assert/strict';
import { mkdtemp, readFile, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import Fastify from 'fastify';
import { authRoutes } from './auth.ts';
import { adminRoutes } from './admin.ts';
import { permissionRoutes } from './permissions.ts';
import { moderationRoutes } from './moderation.ts';
import { diffLines } from './diff.ts';
import { execFileSync } from 'node:child_process';
import { pool } from './db.ts';

after(() => pool.end());

test('line diff: kept, removed and added lines', () => {
  assert.deepEqual(diffLines('ا\nب\nج', 'ا\nد\nج'), [{ op: '=', text: 'ا' }, { op: '-', text: 'ب' }, { op: '+', text: 'د' }, { op: '=', text: 'ج' }]);
});

test('pipeline: L2 drafts, L1 approves, admin publishes; returns, rejects, permissions, conflicts, history', async () => {
  const data = await mkdtemp(join(tmpdir(), 'divan-data-'));
  process.env.DIVAN_DATA_DIR = data;
  const git = (...a: string[]) => execFileSync('git', ['-C', data, ...a], { encoding: 'utf8' });
  git('init', '-q'); git('-c', 'user.name=t', '-c', 'user.email=t@t', 'commit', '-q', '--allow-empty', '-m', 'start');
  const app = Fastify();
  authRoutes(app); adminRoutes(app); permissionRoutes(app); moderationRoutes(app);
  const run = Date.now();
  const call = (method: string, url: string, body?: object, token?: string) =>
    app.inject({ method: method as any, url, payload: body, headers: { ...(token && { authorization: `Bearer ${token}` }), 'x-client-ip': `mod-${run}` } });
  const person = async (name: string, role: string) => {
    const s = (await call('POST', '/api/auth/signup', { email: `${name}-${run}@divan.test`, password: 'pass-word-1' })).json();
    await pool.query('UPDATE users SET role = $2 WHERE id = $1', [s.user.id, role]);
    return { ...s, id: s.user.id };
  };
  const admin = await person('admin', 'admin'), l1 = await person('l1', 'mod-l1'), l2 = await person('l2', 'mod-l2');
  await call('POST', '/api/auth/profile', { full_name: 'نیا موڈریٹر' }, l2.token); // a public name; the L1 has none
  const other = await person('l1other', 'mod-l1'), reader = await person('reader', 'reader');

  // a Ghalib ghazal; the work is restored at the end
  const poem = (await pool.query(`SELECT p.id, p.url, p.title FROM poems p JOIN categories c ON c.id = p.category_id WHERE c.url = '/p266/ghazal' ORDER BY p.position LIMIT 1`)).rows[0];
  const before = (await pool.query('SELECT vorder, position, couplet, text FROM verses WHERE poem_id = $1 ORDER BY vorder', [poem.id])).rows;
  const searchBefore = (await pool.query('SELECT search_text FROM poems WHERE id = $1', [poem.id])).rows[0].search_text;
  try {
    // grants: L2 and L1 on Ghalib's ghazals; the other L1 on Iqbal only
    for (const [m, target] of [[l2, '/p266/ghazal'], [l1, '/p266/ghazal'], [other, '/p238']] as const)
      await call('POST', `/api/admin/users/${m.id}/grants`, { scope: target === '/p238' ? 'poet' : 'category', target, content: ['works'], actions: ['edit'] }, admin.token);

    assert.equal((await call('POST', `/api/mod/work/${poem.id}/draft`, undefined, reader.token)).statusCode, 403, 'readers cannot moderate');
    assert.equal((await call('POST', `/api/mod/work/${poem.id}/draft`, undefined, other.token)).statusCode, 403, 'outside the grant');
    assert.deepEqual((await call('GET', `/api/mod/can?poem=${poem.id}`, undefined, l2.token)).json(), { edit: true, review: false, publish: false, tags: false });

    // opening the editor is not a change: an unchanged draft is not listed, and saving it unchanged drops it
    const blank = (await call('POST', `/api/mod/work/${poem.id}/draft`, undefined, l2.token)).json().id;
    assert.equal((await call('GET', '/api/mod/queue', undefined, l2.token)).json().mine.some((r: any) => r.id === blank), false, 'unchanged draft not listed');
    const text = (await call('GET', `/api/mod/work/${poem.id}`, undefined, l2.token)).json().content;
    assert.deepEqual((await call('POST', `/api/mod/revisions/${blank}/save`, { content: text.replace('}}\n', '}}\n\n') }, l2.token)).json(), { discarded: true }, 'layout-only difference is no change');
    assert.equal((await call('GET', `/api/mod/revisions/${blank}`, undefined, l2.token)).statusCode, 404, 'dropped');

    // L2 drafts: the draft starts from the current text; reopening returns the same draft
    const { id } = (await call('POST', `/api/mod/work/${poem.id}/draft`, undefined, l2.token)).json();
    assert.equal((await call('POST', `/api/mod/work/${poem.id}/draft`, undefined, l2.token)).json().id, id);
    let rev = (await call('GET', `/api/mod/revisions/${id}`, undefined, l2.token)).json();
    assert.equal(rev.changes, 0);
    const misra = before.find((v: any) => v.couplet === 1 && v.position === 'Left').text;
    const edited = rev.revision.content.replace(misra, misra + ' (ترمیم)');
    assert.equal((await call('POST', `/api/mod/revisions/${id}/save`, { content: 'متن بغیر شعر کے {{', summary: 'x' }, l2.token)).statusCode, 200, 'prose is a paragraph');
    assert.equal((await call('POST', `/api/mod/revisions/${id}/save`, { content: edited, summary: 'ایک مصرع درست کیا' }, l2.token)).statusCode, 200);
    assert.equal((await call('GET', '/api/mod/queue', undefined, l2.token)).json().mine.some((r: any) => r.id === id), true, 'changed draft listed');
    rev = (await call('GET', `/api/mod/revisions/${id}`, undefined, l2.token)).json();
    assert.deepEqual(rev.diff.filter((d: any) => d.op !== '=').map((d: any) => d.op), ['-', '+'], 'one line changed');
    assert.equal(rev.may.approve, false, 'not my own draft');

    // L2 submits; the out-of-scope L1 cannot see it; L1 returns it, L2 resubmits, L1 approves
    assert.equal((await call('POST', `/api/mod/revisions/${id}/submit`, {}, l2.token)).json().status, 'submitted');
    assert.equal((await call('GET', `/api/mod/revisions/${id}`, undefined, other.token)).statusCode, 403);
    assert.equal((await call('GET', '/api/mod/queue', undefined, l1.token)).json().review.some((r: any) => r.id === id), true);
    assert.equal((await call('POST', `/api/mod/revisions/${id}/return`, {}, l1.token)).statusCode, 400, 'a reason is required');
    assert.equal((await call('POST', `/api/mod/revisions/${id}/return`, { comment: 'وزن دیکھیں' }, l1.token)).json().status, 'returned');
    assert.equal((await call('POST', `/api/mod/revisions/${id}/publish`, {}, admin.token)).statusCode, 403, 'not yet approved');
    await call('POST', `/api/mod/revisions/${id}/submit`, {}, l2.token);
    assert.equal((await call('POST', `/api/mod/revisions/${id}/approve`, {}, l2.token)).statusCode, 403, 'L2 cannot approve');
    assert.equal((await call('POST', `/api/mod/revisions/${id}/approve`, { comment: 'درست' }, l1.token)).json().status, 'approved');
    assert.equal((await call('GET', '/api/mod/queue', undefined, admin.token)).json().publish.some((r: any) => r.id === id), true);

    // admin publishes: version 1 in divan-data and on the site
    assert.deepEqual((await call('POST', `/api/mod/revisions/${id}/publish`, {}, admin.token)).json(), { status: 'published', version: 1 });
    const file = JSON.parse(await readFile(join(data, 'divan', poem.url.slice(1) + '.json'), 'utf8'));
    // public names only in divan-data (it is public); a commit with the moderator as author and review trailers
    assert.equal(file.Edited.by, 'نیا موڈریٹر');
    assert.equal(file.Edited.reviewedBy, `موڈریٹر ${l1.id}`);
    assert.equal(file.Edited.publishedBy, `موڈریٹر ${admin.id}`);
    assert.ok(!JSON.stringify(file).includes('@'), 'no email addresses');
    const log = git('log', '-1', '--format=%an <%ae>%n%B');
    assert.match(log, new RegExp(`^نیا موڈریٹر <moderator-${l2.id}@users\\.noreply\\.divan>`));
    assert.match(log, /\(ورژن 1\)/);
    assert.match(log, new RegExp(`Reviewed-by: موڈریٹر ${l1.id} <moderator-${l1.id}@`));
    assert.match(log, new RegExp(`Approved-by: موڈریٹر ${admin.id} <moderator-${admin.id}@`));
    assert.ok(!log.includes('divan.test'), 'no real emails in git');
    assert.deepEqual(git('show', '--name-only', '--format=', 'HEAD').trim().split('\n').sort(), [`divan${poem.url}.dtx`, `divan${poem.url}.json`]);
    const committed = (await pool.query(`SELECT commit FROM revisions WHERE id = $1`, [id])).rows[0].commit;
    assert.equal(committed, git('rev-parse', 'HEAD').trim(), 'the commit is recorded with the version');
    assert.ok((await readFile(join(data, 'divan', poem.url.slice(1) + '.dtx'), 'utf8')).includes(misra + ' (ترمیم)'));
    assert.equal((await pool.query('SELECT text FROM verses WHERE poem_id = $1 AND couplet = 1 AND position = $2', [poem.id, 'Left'])).rows[0].text, misra + ' (ترمیم)');

    // the full event trail, in order
    rev = (await call('GET', `/api/mod/revisions/${id}`, undefined, admin.token)).json();
    assert.deepEqual(rev.events.map((e: any) => e.action), ['created', 'saved', 'saved', 'submitted', 'returned', 'submitted', 'approved', 'published']);
    assert.equal(rev.events.find((e: any) => e.action === 'returned').comment, 'وزن دیکھیں');

    // conflict: an L1 draft started from version 1, but an admin publishes version 2 first
    const d2 = (await call('POST', `/api/mod/work/${poem.id}/draft`, undefined, l1.token)).json().id;
    const d3 = (await call('POST', `/api/mod/work/${poem.id}/draft`, undefined, admin.token)).json().id;
    const cur = (await call('GET', `/api/mod/work/${poem.id}`, undefined, admin.token)).json();
    assert.equal(cur.version, 1);
    await call('POST', `/api/mod/revisions/${d3}/save`, { content: cur.content.replace('(ترمیم)', '(ترمیم دوم)') }, admin.token);
    assert.deepEqual((await call('POST', `/api/mod/revisions/${d3}/submit`, {}, admin.token)).json(), { status: 'published', version: 2 }, "an admin's own edit publishes");
    await call('POST', `/api/mod/revisions/${d2}/save`, { content: cur.content.replace('(ترمیم)', '(ترمیم سوم)') }, l1.token);
    assert.equal((await call('POST', `/api/mod/revisions/${d2}/submit`, {}, l1.token)).json().status, 'approved', "an L1's own draft goes to the admin");
    const conflict = await call('POST', `/api/mod/revisions/${d2}/publish`, {}, admin.token);
    assert.equal(conflict.statusCode, 409, 'a newer version was published meanwhile');
    assert.equal((await call('POST', `/api/mod/revisions/${d2}/reject`, { comment: 'پرانا متن' }, admin.token)).json().status, 'rejected');

    const hist = (await call('GET', `/api/mod/work/${poem.id}`, undefined, l1.token)).json();
    assert.deepEqual(hist.history.filter((h: any) => h.version).map((h: any) => h.version), [2, 1]);
    assert.ok((await call('GET', '/api/mod/log', undefined, l2.token)).json().entries.length >= 10);

    // version history (#51): compare any two versions, bring an old one back through the pipeline, filter the log
    const cmp = (await call('GET', `/api/mod/compare/work/${poem.id}`, undefined, l2.token)).json();
    assert.deepEqual([cmp.a, cmp.b, cmp.changes], [1, 2, 2], 'by default the latest version against the one before');
    assert.ok(cmp.diff.some((d: any) => d.op === '+' && d.text.includes('(ترمیم دوم)')));
    const v0 = (await call('GET', `/api/mod/compare/work/${poem.id}?a=0&b=2`, undefined, l2.token)).json();
    assert.ok(v0.diff.some((d: any) => d.op === '-' && d.text === misra), 'version 0 is the Wikisource text');
    assert.equal((await call('GET', `/api/mod/compare/work/${poem.id}?a=7`, undefined, l2.token)).statusCode, 404);
    assert.equal((await call('POST', `/api/mod/revert/work/${poem.id}`, { version: 0 }, other.token)).statusCode, 403, 'outside the grant');
    assert.equal((await call('POST', `/api/mod/revert/work/${poem.id}`, { version: 2 }, l2.token)).statusCode, 400, 'already the current version');
    const back = (await call('POST', `/api/mod/revert/work/${poem.id}`, { version: 0 }, l2.token)).json().id;
    const br = (await call('GET', `/api/mod/revisions/${back}`, undefined, l2.token)).json();
    assert.deepEqual([br.revision.status, br.revision.base_version, br.revision.summary], ['draft', 2, 'ویکی ماخذ کا متن واپس لایا']);
    assert.ok(br.diff.some((d: any) => d.op === '+' && d.text === misra), 'the draft brings the old line back');
    assert.deepEqual(br.events.map((e: any) => e.action), ['created', 'reverted']);
    assert.deepEqual((await call('POST', `/api/mod/revisions/${back}/submit`, {}, l2.token)).json().status, 'submitted', 'then reviewed like any edit');
    const mine = (await call('GET', `/api/mod/log?who=l2-${run}&action=reverted`, undefined, l1.token)).json().entries;
    assert.deepEqual(mine.map((e: any) => e.revision), [back], 'log filtered by person and step');
    assert.equal((await call('GET', `/api/mod/log?who=l2-${run}&kind=order`, undefined, l1.token)).json().entries.length, 0);
  } finally {
    // restore the work and remove the test people and their revisions
    await pool.query('DELETE FROM verses WHERE poem_id = $1', [poem.id]);
    for (const v of before) await pool.query('INSERT INTO verses (poem_id, vorder, position, couplet, text) VALUES ($1, $2, $3, $4, $5)', [poem.id, v.vorder, v.position, v.couplet, v.text]);
    await pool.query('UPDATE poems SET title = $2, search_text = $3 WHERE id = $1', [poem.id, poem.title, searchBefore]);
    await pool.query(`DELETE FROM revisions WHERE entity = 'work' AND entity_id = $1 AND author_email LIKE $2`, [poem.id, `%-${run}@divan.test`]);
    await pool.query('DELETE FROM users WHERE email LIKE $1', [`%-${run}@divan.test`]);
    await pool.query('DELETE FROM audit_log WHERE actor_email LIKE $1 OR target_email LIKE $1', [`%-${run}@divan.test`]);
    await rm(data, { recursive: true, force: true });
    await app.close();
  }
});
