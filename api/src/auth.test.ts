import { test, after } from 'node:test';
import assert from 'node:assert/strict';
import Fastify from 'fastify';
import { hashPassword, verifyPassword, normaliseEmail, validEmail, passwordProblem, limiter, authRoutes } from './auth.ts';
import { pool } from './db.ts';

after(() => pool.end());

test('passwords: salted scrypt, verified in constant time, wrong ones rejected', async () => {
  const h = await hashPassword('دیوان-غالب-123');
  assert.match(h, /^scrypt\$32768\$8\$1\$[\w-]+\$[\w-]+$/);
  assert.notEqual(h, await hashPassword('دیوان-غالب-123'), 'a new salt each time');
  assert.equal(await verifyPassword('دیوان-غالب-123', h), true);
  assert.equal(await verifyPassword('دیوان-غالب-124', h), false);
});

test('email and password checks', () => {
  assert.equal(normaliseEmail('  Anas@Example.COM '), 'anas@example.com');
  assert.ok(validEmail('a@b.pk') && !validEmail('a@b') && !validEmail('a b@c.pk'));
  assert.equal(passwordProblem('1234567'), 'پاس ورڈ کم از کم ۸ حروف کا ہو');
  assert.equal(passwordProblem('12345678'), null);
});

test('rate limiter: max per window, then resets', () => {
  const allow = limiter(2, 1000);
  assert.deepEqual([allow('ip', 0), allow('ip', 1), allow('ip', 2), allow('other', 2)], [true, true, false, true]);
  assert.equal(allow('ip', 1001), true);
});

test('HTTP flow: sign up, sign in, wrong password, change password, delete', async () => {
  const app = Fastify();
  authRoutes(app);
  const email = `test-${Date.now()}@divan.test`;
  const call = (method: string, url: string, body?: object, token?: string) =>
    app.inject({ method: method as any, url, payload: body, headers: { ...(token && { authorization: `Bearer ${token}` }), 'x-client-ip': `t-${email}` } });

  const up = await call('POST', '/api/auth/signup', { email, password: 'pass-word-1' });
  assert.equal(up.statusCode, 200);
  const t1 = up.json().token;
  assert.equal((await call('POST', '/api/auth/signup', { email: email.toUpperCase(), password: 'pass-word-1' })).statusCode, 409, 'one account per email');
  assert.equal((await call('GET', '/api/auth/me', undefined, t1)).json().user.email, email);

  assert.equal((await call('POST', '/api/auth/signin', { email, password: 'wrong-pass' })).statusCode, 401);
  const t2 = (await call('POST', '/api/auth/signin', { email, password: 'pass-word-1' })).json().token;

  assert.equal((await call('POST', '/api/auth/password', { current: 'wrong-pass', next: 'pass-word-2' }, t2)).statusCode, 403);
  assert.equal((await call('POST', '/api/auth/password', { current: 'pass-word-1', next: 'pass-word-2' }, t2)).statusCode, 200);
  assert.equal((await call('GET', '/api/auth/me', undefined, t1)).statusCode, 401, 'other sessions signed out');
  assert.equal((await call('GET', '/api/auth/me', undefined, t2)).statusCode, 200, 'this session kept');

  await call('POST', '/api/auth/signout', undefined, t2);
  assert.equal((await call('GET', '/api/auth/me', undefined, t2)).statusCode, 401);

  const t3 = (await call('POST', '/api/auth/signin', { email, password: 'pass-word-2' })).json().token;
  assert.equal((await call('POST', '/api/auth/delete', { password: 'wrong' }, t3)).statusCode, 403);
  assert.equal((await call('POST', '/api/auth/delete', { password: 'pass-word-2' }, t3)).statusCode, 200);
  assert.equal((await pool.query('SELECT count(*)::int AS n FROM users WHERE email = $1', [email])).rows[0].n, 0);
  assert.equal((await pool.query("SELECT count(*)::int AS n FROM sessions s LEFT JOIN users u ON u.id = s.user_id WHERE u.id IS NULL")).rows[0].n, 0);
  await app.close();
});

test('sign-in is rate limited per email', async () => {
  const app = Fastify();
  authRoutes(app);
  const email = `limit-${Date.now()}@divan.test`;
  const codes = [];
  for (let i = 0; i < 10; i++)
    codes.push((await app.inject({ method: 'POST', url: '/api/auth/signin', payload: { email, password: 'x' }, headers: { 'x-client-ip': `ip-${i}` } })).statusCode);
  assert.deepEqual(codes, [401, 401, 401, 401, 401, 401, 401, 401, 429, 429]);
  await app.close();
});
