import { test, after } from 'node:test';
import assert from 'node:assert/strict';
import { mkdtemp, readFile, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { execFileSync } from 'node:child_process';
import Fastify from 'fastify';
import { authRoutes } from './auth.ts';
import { permissionRoutes } from './permissions.ts';
import { moderationRoutes } from './moderation.ts';
import { pool } from './db.ts';

after(() => pool.end());

test('arranging: its own permission; L2 reorders, L1 approves, admin publishes to divan-data and the site', async () => {
  const data = await mkdtemp(join(tmpdir(), 'divan-data-'));
  process.env.DIVAN_DATA_DIR = data;
  const git = (...a: string[]) => execFileSync('git', ['-C', data, ...a], { encoding: 'utf8' });
  git('init', '-q'); git('-c', 'user.name=t', '-c', 'user.email=t@t', 'commit', '-q', '--allow-empty', '-m', 'start');
  const app = Fastify();
  authRoutes(app); permissionRoutes(app); moderationRoutes(app);
  const run = Date.now();
  const call = (method: string, url: string, body?: object, token?: string) =>
    app.inject({ method: method as any, url, payload: body, headers: { ...(token && { authorization: `Bearer ${token}` }), 'x-client-ip': `ord-${run}` } });
  const person = async (name: string, role: string) => {
    const s = (await call('POST', '/api/auth/signup', { email: `${name}-${run}@divan.test`, password: 'pass-word-1' })).json();
    await pool.query('UPDATE users SET role = $2 WHERE id = $1', [s.user.id, role]);
    return { ...s, id: s.user.id };
  };
  const admin = await person('admin', 'admin'), l1 = await person('l1', 'mod-l1'), l2 = await person('l2', 'mod-l2');

  // Ghalib's ghazals; their order is restored at the end
  const cat = (await pool.query(`SELECT id, url FROM categories WHERE url = '/p266/ghazal'`)).rows[0];
  const before = (await pool.query('SELECT id, position FROM poems WHERE category_id = $1', [cat.id])).rows;
  try {
    const grant = (m: any, actions: string[]) =>
      pool.query(`INSERT INTO grants (user_id, scope, scope_id, content, actions) VALUES ($1, 'category', $2, '{works}', $3)`, [m.id, cat.id, actions]);
    await grant(l2, ['edit']);
    assert.deepEqual((await call('GET', `/api/mod/can?category=${cat.id}`, undefined, l2.token)).json(), { arrange: false, tags: false, ebooks: false, book: false, poet: false }, 'editing is not arranging');
    assert.equal((await call('POST', `/api/mod/order/${cat.id}/draft`, undefined, l2.token)).statusCode, 403);
    await grant(l2, ['arrange']); await grant(l1, ['arrange']);
    assert.deepEqual((await call('GET', `/api/mod/can?category=${cat.id}`, undefined, l2.token)).json(), { arrange: true, tags: false, ebooks: false, book: false, poet: false });

    const { id } = (await call('POST', `/api/mod/order/${cat.id}/draft`, undefined, l2.token)).json();
    const cur = (await call('GET', `/api/mod/order/${cat.id}`, undefined, l2.token)).json();
    const lines = cur.content.split('\n');
    assert.ok(lines.length > 100 && /^sh\d+ /.test(lines[0]), 'one line per work: "<url part> <title>"');
    // the list must keep every item once
    assert.equal((await call('POST', `/api/mod/revisions/${id}/save`, { content: lines.slice(1).join('\n') }, l2.token)).statusCode, 400, 'an item missing');
    assert.equal((await call('POST', `/api/mod/revisions/${id}/save`, { content: [lines[0], ...lines].join('\n') }, l2.token)).statusCode, 400, 'an item twice');
    // move the last ghazal to the top; titles may be anything (only the order counts)
    const moved = [lines.at(-1), ...lines.slice(0, -1)].join('\n');
    assert.equal((await call('POST', `/api/mod/revisions/${id}/save`, { content: moved, summary: 'آخری غزل پہلے' }, l2.token)).statusCode, 200);
    assert.equal((await call('POST', `/api/mod/revisions/${id}/submit`, {}, l2.token)).json().status, 'submitted');
    assert.equal((await call('GET', '/api/mod/queue', undefined, l1.token)).json().review.some((r: any) => r.id === id && r.entity === 'order'), true);
    assert.equal((await call('POST', `/api/mod/revisions/${id}/approve`, {}, l1.token)).json().status, 'approved');
    assert.deepEqual((await call('POST', `/api/mod/revisions/${id}/publish`, {}, admin.token)).json(), { status: 'published', version: 1 });

    // the site follows the new order; divan-data holds it and its commit
    const first = (await pool.query('SELECT url FROM poems WHERE category_id = $1 ORDER BY position, id LIMIT 1', [cat.id])).rows[0].url;
    assert.equal(first.split('/').pop(), lines.at(-1).split(' ')[0]);
    assert.equal(await readFile(join(data, 'divan', 'p266', 'ghazal.order'), 'utf8'), moved + '\n');
    assert.match(git('log', '-1', '--format=%s').trim(), /^ترتیب: .+: آخری غزل پہلے \(ورژن 1\)$/);
    assert.equal((await call('GET', `/api/mod/order/${cat.id}`, undefined, l2.token)).json().version, 1);

    // a new draft saved unchanged is dropped
    const again = (await call('POST', `/api/mod/order/${cat.id}/draft`, undefined, l2.token)).json().id;
    assert.deepEqual((await call('POST', `/api/mod/revisions/${again}/save`, { content: moved }, l2.token)).json(), { discarded: true });
  } finally {
    for (const p of before) await pool.query('UPDATE poems SET position = $2 WHERE id = $1', [p.id, p.position]);
    await pool.query(`DELETE FROM revisions WHERE author_email LIKE $1`, [`%-${run}@divan.test`]);
    await pool.query('DELETE FROM users WHERE email LIKE $1', [`%-${run}@divan.test`]);
    await pool.query('DELETE FROM audit_log WHERE actor_email LIKE $1 OR target_email LIKE $1', [`%-${run}@divan.test`]);
    await rm(data, { recursive: true, force: true });
    await app.close();
  }
});
