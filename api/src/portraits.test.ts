import { test, after } from 'node:test';
import assert from 'node:assert/strict';
import { mkdtemp, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import Fastify from 'fastify';
import { authRoutes } from './auth.ts';
import { ebookRoutes } from './ebooks.ts';
import { portraitRoutes, SHOWN } from './portraits.ts';
import { pool } from './db.ts';

after(() => pool.end());

// a tiny valid PNG (1x1)
const PNG = Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==', 'base64');

test('portraits: moderators with the permission frame and upload; uploads wait for an admin', async () => {
  const files = await mkdtemp(join(tmpdir(), 'divan-portraits-')); process.env.DIVAN_FILES_DIR = files;
  const app = Fastify(); authRoutes(app); ebookRoutes(app); portraitRoutes(app); // ebookRoutes: the octet-stream parser
  const run = Date.now();
  const call = (method: string, url: string, body?: any, token?: string, type?: string) => app.inject({ method: method as any, url, payload: body,
    headers: { ...(token && { authorization: `Bearer ${token}` }), ...(type && { 'content-type': type }), 'x-client-ip': `portraits-${run}` } });
  const signup = async (email: string) => (await call('POST', '/api/auth/signup', { email, password: 'pass-word-1' })).json();
  const mod = await signup(`pmod-${run}@divan.test`), admin = await signup(`padmin-${run}@divan.test`);
  await pool.query(`UPDATE users SET role = 'mod-l2' WHERE id = $1`, [mod.user.id]);
  await pool.query(`UPDATE users SET role = 'admin' WHERE id = $1`, [admin.user.id]);
  // a poet without a portrait, put back as it was at the end
  const poet = (await pool.query('SELECT id, portrait, portrait_credit FROM poets WHERE portrait IS NULL ORDER BY id LIMIT 1')).rows[0];
  try {
    const upload = (tk: string) => call('POST', `/api/mod/portraits/${poet.id}/upload?licence=CC0&source=${encodeURIComponent('https://example.org/p.png')}`, PNG, tk, 'application/octet-stream');
    assert.equal((await call('GET', '/api/mod/portraits', undefined, mod.token)).json().upload.length, 0, 'no permission: nothing to upload for');
    assert.equal((await upload(mod.token)).statusCode, 403);
    // the "تصاویر" permission for this poet: create and edit
    await pool.query(`INSERT INTO grants (user_id, scope, scope_id, content, actions) VALUES ($1, 'poet', $2, '{portraits}', '{create,edit}')`, [mod.user.id, poet.id]);
    assert.deepEqual((await call('GET', '/api/mod/portraits', undefined, mod.token)).json().upload.map((p: any) => p.id), [poet.id], 'only that poet');
    assert.equal((await call('POST', `/api/mod/portraits/${poet.id}/upload?licence=CC0`, PNG, mod.token, 'application/octet-stream')).statusCode, 400, 'a source is needed');
    assert.deepEqual((await upload(mod.token)).json(), { ok: true, waiting: true });
    const shown = async () => (await pool.query(`SELECT ${SHOWN} AS p FROM poets WHERE id = $1`, [poet.id])).rows[0].p;
    assert.equal(await shown(), null, 'not shown before approval');
    const listed = (await call('GET', '/api/mod/portraits', undefined, mod.token)).json().portraits.find((p: any) => p.id === poet.id);
    assert.ok(listed.waiting && listed.may.edit && !listed.may.delete && !listed.may.approve);
    assert.equal((await call('POST', `/api/mod/portraits/${poet.id}`, { act: 'approve' }, mod.token)).statusCode, 403, 'moderators do not approve');
    assert.equal((await call('POST', `/api/mod/portraits/${poet.id}`, { act: 'remove' }, mod.token)).statusCode, 403, 'no delete permission');
    assert.equal((await call('POST', `/api/mod/portraits/${poet.id}`, { act: 'focus', x: .6, y: .3, z: 1.5 }, mod.token)).json().ok, true, 'framing: edit');
    assert.equal((await call('POST', `/api/mod/portraits/${poet.id}`, { act: 'approve' }, admin.token)).json().ok, true);
    assert.ok(await shown(), 'shown once an admin approved it');
    assert.equal((await call('GET', `/api/poet/${poet.id}/portrait`)).statusCode, 200);
  } finally {
    await pool.query('UPDATE poets SET portrait = $2, portrait_credit = $3 WHERE id = $1', [poet.id, poet.portrait, poet.portrait_credit]);
    await rm(files, { recursive: true, force: true });
  }
});
