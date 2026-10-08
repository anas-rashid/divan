import { test, after } from 'node:test';
import assert from 'node:assert/strict';
import Fastify from 'fastify';
import { authRoutes } from './auth.ts';
import { adminRoutes } from './admin.ts';
import { permissionRoutes, can, covers } from './permissions.ts';
import { pool } from './db.ts';

after(() => pool.end());

test('scope coverage: all, poet, book (with everything under it), one work', () => {
  const at = { poemId: 7, chain: [30, 20, 10], poetId: 1 }; // work 7 in section 30, inside book 20, inside the poet's root 10
  assert.ok(covers({ scope: 'all', scope_id: null }, at));
  assert.ok(covers({ scope: 'poet', scope_id: 1 }, at) && !covers({ scope: 'poet', scope_id: 2 }, at));
  assert.ok(covers({ scope: 'category', scope_id: 20 }, at) && !covers({ scope: 'category', scope_id: 31 }, at));
  assert.ok(covers({ scope: 'poem', scope_id: 7 }, at) && !covers({ scope: 'poem', scope_id: 8 }, at));
});

test('grants through the admin API; can() on real content; demotion clears grants', async () => {
  const app = Fastify();
  authRoutes(app); adminRoutes(app); permissionRoutes(app);
  const run = Date.now();
  const call = (method: string, url: string, body?: object, token?: string) =>
    app.inject({ method: method as any, url, payload: body, headers: { ...(token && { authorization: `Bearer ${token}` }), 'x-client-ip': `perm-${run}` } });
  const signup = async (email: string) => (await call('POST', '/api/auth/signup', { email, password: 'pass-word-1' })).json();
  const admin = await signup(`perm-admin-${run}@divan.test`), mod = await signup(`perm-mod-${run}@divan.test`);
  await pool.query(`UPDATE users SET role = 'admin' WHERE id = $1`, [admin.user.id]);
  const grant = (body: object) => call('POST', `/api/admin/users/${mod.user.id}/grants`, body, admin.token);

  // real content: a Ghalib ghazal, a Ghalib work outside the ghazal section, an Iqbal work
  const ghazals = (await pool.query(`SELECT id FROM categories WHERE url = '/p266/ghazal'`)).rows[0].id;
  const ghazal = (await pool.query('SELECT id FROM poems WHERE category_id = $1 LIMIT 1', [ghazals])).rows[0].id;
  const otherGhalib = (await pool.query('SELECT id FROM poems WHERE poet_id = 266 AND category_id <> $1 LIMIT 1', [ghazals])).rows[0].id;
  const iqbal = (await pool.query('SELECT id FROM poems WHERE poet_id = 238 LIMIT 1')).rows[0].id;

  assert.equal((await grant({ scope: 'poet', target: '266', content: ['works'], actions: ['edit'] })).statusCode, 400, 'readers get no grants');
  await call('POST', `/api/admin/users/${mod.user.id}/role`, { role: 'mod-l2' }, admin.token);
  const m = { id: mod.user.id, role: 'mod-l2' };

  assert.equal((await grant({ scope: 'category', target: 'http://127.0.0.1:4200/p266/ghazal', content: ['works'], actions: ['edit', 'arrange'] })).statusCode, 200);
  assert.equal(await can(m, 'edit', 'works', { poemId: ghazal }), true, 'a ghazal inside the granted section');
  assert.equal(await can(m, 'arrange', 'works', { categoryId: ghazals }), true);
  assert.equal(await can(m, 'edit', 'works', { poemId: otherGhalib }), false, 'outside the section');
  assert.equal(await can(m, 'delete', 'works', { poemId: ghazal }), false, 'action not granted');
  assert.equal(await can(m, 'edit', 'poets', { poetId: 266 }), false, 'content type not granted');
  assert.equal(await can(m, 'edit', 'works', { poemId: iqbal }), false, 'another poet');

  assert.equal((await grant({ scope: 'poet', target: '/p238', content: ['works', 'books'], actions: ['create', 'edit', 'delete'] })).statusCode, 200);
  assert.equal(await can(m, 'delete', 'works', { poemId: iqbal }), true, "anything of Iqbal's");
  assert.equal(await can(m, 'create', 'books', { poetId: 238 }), true);

  assert.equal((await grant({ scope: 'poet', target: '266', content: ['dictionary'], actions: ['edit'] })).statusCode, 400, 'dictionary is site-wide only');
  assert.equal((await grant({ scope: 'all', content: ['dictionary'], actions: ['edit'] })).statusCode, 200);
  assert.equal(await can(m, 'edit', 'dictionary'), true);
  assert.equal(await can(m, 'delete', 'dictionary'), false);
  assert.equal((await grant({ scope: 'poem', target: '/p266/nowhere', content: ['works'], actions: ['edit'] })).statusCode, 400, 'unknown page');

  assert.equal(await can({ id: 0, role: 'reader' }, 'edit', 'works', { poemId: ghazal }), false);
  assert.equal(await can({ id: admin.user.id, role: 'admin' }, 'delete', 'poets', { poetId: 266 }), true);
  assert.equal(await can(null, 'edit', 'works', { poemId: ghazal }), false);
  assert.equal((await call('GET', `/api/admin/users/${mod.user.id}/grants`, undefined, mod.token)).statusCode, 403, 'moderators cannot manage grants');

  const list = (await call('GET', `/api/admin/users/${mod.user.id}/grants`, undefined, admin.token)).json().grants;
  assert.deepEqual(list.map((g: any) => [g.scope, g.label]), [['category', 'غزل'], ['poet', (await pool.query('SELECT nickname FROM poets WHERE id = 238')).rows[0].nickname], ['all', null]]);
  assert.equal((await call('POST', `/api/admin/grants/${list[1].id}/delete`, undefined, admin.token)).statusCode, 200);
  assert.equal(await can(m, 'delete', 'works', { poemId: iqbal }), false, 'revoked');

  await call('POST', `/api/admin/users/${mod.user.id}/role`, { role: 'reader' }, admin.token);
  assert.equal((await pool.query('SELECT count(*)::int AS n FROM grants WHERE user_id = $1', [mod.user.id])).rows[0].n, 0, 'demotion clears grants');

  await pool.query('DELETE FROM users WHERE id = ANY($1)', [[admin.user.id, mod.user.id]]);
  await pool.query(`DELETE FROM audit_log WHERE actor_email LIKE $1 OR target_email LIKE $1`, [`perm-%-${run}@divan.test`]);
  await app.close();
});
