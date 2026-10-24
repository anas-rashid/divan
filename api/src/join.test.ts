import { test, after } from 'node:test';
import assert from 'node:assert/strict';
import Fastify from 'fastify';
import { authRoutes } from './auth.ts';
import { adminRoutes } from './admin.ts';
import { joinRoutes } from './join.ts';
import { outbox } from './mail.ts';
import { pool } from './db.ts';

let run = 0;
after(async () => { await pool.query(`DELETE FROM join_requests WHERE email LIKE $1`, [`%-${run}@divan.test`]); await pool.end(); });

test('requests to join: admins emailed, no telling who has an account, approve invites, decline', async () => {
  const app = Fastify();
  authRoutes(app); adminRoutes(app); joinRoutes(app);
  run = Date.now();
  const call = (method: string, url: string, body?: object, token?: string, from = 'a') =>
    app.inject({ method: method as any, url, payload: body, headers: { ...(token && { authorization: `Bearer ${token}` }), 'x-client-ip': `join-test-${run}-${from}` } });
  const a = (await call('POST', '/api/auth/signup', { email: `join-admin-${run}@divan.test`, password: 'pass-word-1' })).json();
  await pool.query(`UPDATE users SET role = 'admin' WHERE id = $1`, [a.user.id]);

  const ask = (email: string, from = 'b') => call('POST', '/api/auth/join', { email, name: ' نیا\nقاری ', note: 'غالب کے خطوط بھیجنا چاہتا ہوں' }, undefined, from);
  assert.equal((await call('POST', '/api/auth/join', { email: 'nope', name: 'x' })).statusCode, 400);
  assert.equal((await call('POST', '/api/auth/join', { email: `x-${run}@divan.test` })).statusCode, 400, 'a name is needed');
  const before = outbox.length;
  assert.deepEqual((await ask(`new-${run}@divan.test`)).json(), { ok: true });
  const mail = outbox.at(-1)!;
  assert.equal(outbox.length, before + 1);
  assert.ok(mail.to.includes(`join-admin-${run}@divan.test`) && mail.subject.includes('نیا قاری') && mail.text.includes('/admin/requests'), 'admins told');
  assert.deepEqual((await ask(`new-${run}@divan.test`)).json(), { ok: true }, 'asked twice');
  assert.deepEqual((await ask(`join-admin-${run}@divan.test`)).json(), { ok: true }, 'has an account: same answer');
  assert.equal(outbox.length, before + 1, 'neither stored nor emailed again');

  const r = (await call('GET', '/api/admin/requests', undefined, a.token)).json();
  const mine = r.requests.find((q: any) => q.email === `new-${run}@divan.test`);
  assert.equal(mine.name, 'نیا قاری');
  assert.equal((await call('GET', '/api/admin/requests', undefined, a.user.id && 'bad-token')).statusCode, 401);

  const made = (await call('POST', `/api/admin/requests/${mine.id}/approve`, {}, a.token)).json();
  assert.ok(made.id && made.link, 'an account and its invitation');
  assert.equal((await call('POST', `/api/admin/requests/${mine.id}/decline`, {}, a.token)).statusCode, 409, 'decided once');
  assert.equal((await pool.query('SELECT full_name FROM users WHERE id = $1', [made.id])).rows[0].full_name, 'نیا قاری');

  await ask(`spam-${run}@divan.test`, 'c');
  const spam = (await call('GET', '/api/admin/requests', undefined, a.token)).json().requests.find((q: any) => q.email === `spam-${run}@divan.test`);
  assert.deepEqual((await call('POST', `/api/admin/requests/${spam.id}/decline`, {}, a.token)).json(), { ok: true });
  assert.ok((await call('GET', '/api/admin/requests?status=declined', undefined, a.token)).json().requests.some((q: any) => q.id === spam.id));
  assert.equal((await pool.query('SELECT 1 FROM users WHERE email = $1', [`spam-${run}@divan.test`])).rowCount, 0);

  for (let i = 0; i < 6; i++) await call('POST', '/api/auth/join', { email: `flood-${i}-${run}@divan.test`, name: 'x' }, undefined, 'd');
  assert.equal((await call('POST', '/api/auth/join', { email: `flood-${run}@divan.test`, name: 'x' }, undefined, 'd')).statusCode, 429, 'rate limited');
});
