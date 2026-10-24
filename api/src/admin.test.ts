import { test, after } from 'node:test';
import assert from 'node:assert/strict';
import Fastify from 'fastify';
import { authRoutes } from './auth.ts';
import { adminRoutes, temporaryPassword } from './admin.ts';
import { pool } from './db.ts';

after(() => pool.end());

test('temporary passwords: 12 characters, no look-alikes', () => {
  const p = temporaryPassword();
  assert.match(p, /^[a-km-np-zA-HJ-NP-Z2-9]{12}$/);
  assert.notEqual(p, temporaryPassword());
});

test('admin panel: only admins; reset, disable, role, delete, self-protection, audit', async () => {
  const app = Fastify();
  authRoutes(app);
  adminRoutes(app);
  const run = Date.now();
  const call = (method: string, url: string, body?: object, token?: string) =>
    app.inject({ method: method as any, url, payload: body, headers: { ...(token && { authorization: `Bearer ${token}` }), 'x-client-ip': `admin-test-${run}` } });
  const signup = async (email: string) => (await call('POST', '/api/auth/signup', { email, password: 'pass-word-1' })).json();

  const a = await signup(`admin-${run}@divan.test`), r = await signup(`reader-${run}@divan.test`);
  await pool.query(`UPDATE users SET role = 'admin' WHERE id = $1`, [a.user.id]);

  assert.equal((await call('GET', '/api/admin/users', undefined, r.token)).statusCode, 403, 'readers are refused');
  assert.equal((await call('GET', '/api/admin/users')).statusCode, 401);

  const list = (await call('GET', `/api/admin/users?q=reader-${run}`, undefined, a.token)).json();
  assert.deepEqual(list.users.map((u: any) => u.email), [`reader-${run}@divan.test`]);
  const rid = r.user.id;
  // role filter and counts: the search for this run's emails finds one admin and one reader
  const both = (await call('GET', `/api/admin/users?q=-${run}@`, undefined, a.token)).json();
  assert.equal(both.counts.all, 2); assert.equal(both.counts.admin, 1); assert.equal(both.counts.reader, 1);
  assert.deepEqual((await call('GET', `/api/admin/users?q=-${run}@&role=admin`, undefined, a.token)).json().users.map((u: any) => u.email), [`admin-${run}@divan.test`]);
  assert.equal(both.users[0].grants, 0);

  // password reset: new temporary password works, the old one and old sessions do not
  const { password } = (await call('POST', `/api/admin/users/${rid}/password`, undefined, a.token)).json();
  assert.equal((await call('GET', '/api/auth/me', undefined, r.token)).statusCode, 401);
  assert.equal((await call('POST', '/api/auth/signin', { email: `reader-${run}@divan.test`, password: 'pass-word-1' })).statusCode, 401);
  const r2 = (await call('POST', '/api/auth/signin', { email: `reader-${run}@divan.test`, password })).json();
  assert.ok(r2.token);

  // disable ends sessions and blocks sign-in; enable restores
  await call('POST', `/api/admin/users/${rid}/disable`, { disabled: true }, a.token);
  assert.equal((await call('GET', '/api/auth/me', undefined, r2.token)).statusCode, 401);
  assert.equal((await call('POST', '/api/auth/signin', { email: `reader-${run}@divan.test`, password })).statusCode, 403);
  await call('POST', `/api/admin/users/${rid}/disable`, { disabled: false }, a.token);
  assert.equal((await call('POST', '/api/auth/signin', { email: `reader-${run}@divan.test`, password })).statusCode, 200);

  // roles
  assert.equal((await call('POST', `/api/admin/users/${rid}/role`, { role: 'emperor' }, a.token)).statusCode, 400);
  assert.equal((await call('POST', `/api/admin/users/${rid}/role`, { role: 'admin' }, a.token)).statusCode, 200);
  assert.equal((await call('POST', `/api/admin/users/${rid}/role`, { role: 'reader' }, a.token)).statusCode, 200);

  // an admin cannot lock themself out
  for (const [path, body] of [['disable', { disabled: true }], ['role', { role: 'reader' }], ['delete', {}]] as const)
    assert.equal((await call('POST', `/api/admin/users/${a.user.id}/${path}`, body, a.token)).statusCode, 400, path);

  assert.equal((await call('POST', `/api/admin/users/${rid}/delete`, undefined, a.token)).statusCode, 200);
  assert.equal((await call('POST', `/api/admin/users/${rid}/delete`, undefined, a.token)).statusCode, 404);

  const log = (await call('GET', '/api/admin/audit', undefined, a.token)).json().entries
    .filter((e: any) => e.target_email === `reader-${run}@divan.test`).map((e: any) => e.action).reverse();
  assert.deepEqual(log, ['password-reset', 'disable', 'enable', 'role', 'role', 'delete']);

  await pool.query('DELETE FROM users WHERE id = $1', [a.user.id]);
  await pool.query(`DELETE FROM audit_log WHERE actor_email = $1 OR target_email = $1`, [`admin-${run}@divan.test`]);
  await app.close();
});

test('closed sign-up: no self sign-up; an admin creates the account and invites them', async () => {
  const app = Fastify();
  authRoutes(app);
  adminRoutes(app);
  const run = Date.now();
  const call = (method: string, url: string, body?: object, token?: string) =>
    app.inject({ method: method as any, url, payload: body, headers: { ...(token && { authorization: `Bearer ${token}` }), 'x-client-ip': `accounts-test-${run}` } });
  const a = (await call('POST', '/api/auth/signup', { email: `admin2-${run}@divan.test`, password: 'pass-word-1' })).json();
  await pool.query(`UPDATE users SET role = 'admin' WHERE id = $1`, [a.user.id]);
  process.env.DIVAN_SIGNUP = '0';
  try {
    assert.equal((await call('POST', '/api/auth/signup', { email: `self-${run}@divan.test`, password: 'pass-word-1' })).statusCode, 403, 'sign-up closed');
    const email = `asked-${run}@divan.test`;
    assert.equal((await call('POST', '/api/admin/users', { email }, undefined)).statusCode, 401);
    assert.equal((await call('POST', '/api/admin/users', { email: 'not an email' }, a.token)).statusCode, 400);
    const made = (await call('POST', '/api/admin/users', { email: ` ${email.toUpperCase()} `, name: 'نیا قاری' }, a.token)).json();
    assert.ok(made.id && !made.emailed && made.link, 'no SMTP_URL in the tests: the link is given to the admin');
    assert.equal((await call('POST', '/api/admin/users', { email }, a.token)).statusCode, 409, 'once per email');
    // the invitation: shows whose it is, sets the password once, signs in
    const t = new URL(made.link).searchParams.get('t');
    assert.equal((await call('POST', '/api/auth/invite', { token: 'made-up' })).statusCode, 404);
    assert.deepEqual((await call('POST', '/api/auth/invite', { token: t })).json(), { email });
    assert.equal((await call('POST', '/api/auth/invite', { token: t, password: 'short' })).statusCode, 400);
    const set = (await call('POST', '/api/auth/invite', { token: t, password: 'pass-word-2' })).json();
    assert.deepEqual([set.user.email, set.user.role, set.user.full_name], [email, 'reader', 'نیا قاری'], 'a reader, signed in');
    assert.equal((await call('POST', '/api/auth/invite', { token: t, password: 'pass-word-3' })).statusCode, 404, 'used up');
    assert.equal((await call('POST', '/api/auth/signin', { email, password: 'pass-word-2' })).statusCode, 200);
    assert.ok((await pool.query(`SELECT 1 FROM audit_log WHERE action = 'create' AND target_id = $1`, [made.id])).rowCount, 'audited');
  } finally {
    delete process.env.DIVAN_SIGNUP;
  }
});
