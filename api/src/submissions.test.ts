import { test, after } from 'node:test';
import assert from 'node:assert/strict';
import { mkdtemp, readFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { execFileSync } from 'node:child_process';
import Fastify from 'fastify';
import { authRoutes } from './auth.ts';
import { submissionRoutes, draftText } from './submissions.ts';
import { pool } from './db.ts';

after(() => pool.end());

test('a reader\'s text as Divan text', () => {
  assert.equal(draftText({ title: 'غزل', poet: 'میر', kind: 'poetry', text: 'الف\r\nب\n\n\n\nج\nد', source: 'https://archive.org/details/x' }),
    '{{دیوان\n| عنوان = غزل\n| شاعر = میر\n| ماخذ_لنک = https://archive.org/details/x\n}}\n<poem>\nالف\nب\n\nج\nد\n</poem>\n');
  assert.match(draftText({ title: 'خط', poet: 'غالب', kind: 'prose', text: 'میاں!', source: 'عود ہندی، ص ۱۲' }), /\| ماخذ = عود ہندی، ص ۱۲\n\}\}\nمیاں!\n$/);
});

test('submissions: a reader sends, a moderator accepts or rejects, an admin publishes to divan-data', async () => {
  const data = await mkdtemp(join(tmpdir(), 'divan-data-'));
  process.env.DIVAN_DATA_DIR = data;
  const git = (...a: string[]) => execFileSync('git', ['-C', data, ...a], { encoding: 'utf8' });
  git('init', '-q'); git('-c', 'user.name=t', '-c', 'user.email=t@t', 'commit', '-q', '--allow-empty', '-m', 'start');
  const app = Fastify();
  authRoutes(app); submissionRoutes(app);
  const run = Date.now();
  const call = (method: string, url: string, body?: any, token?: string) => app.inject({ method: method as any, url, payload: body,
    headers: { ...(token && { authorization: `Bearer ${token}` }), 'x-client-ip': `sub-${run}` } });
  const person = async (name: string, role: string) => {
    const s = (await call('POST', '/api/auth/signup', { email: `${name}-${run}@divan.test`, password: 'pass-word-1' })).json();
    await pool.query('UPDATE users SET role = $2, full_name = $3 WHERE id = $1', [s.user.id, role, role === 'reader' ? `قاری ${run}` : '']);
    return { ...s, id: s.user.id };
  };
  const reader = await person('reader', 'reader'), l2 = await person('l2', 'mod-l2'), admin = await person('admin', 'admin');
  const work = { poet: 'محمد اقبال', book: 'بانگ درا', title: `نئی نظم ${run}`, kind: 'poetry', text: 'پہلا مصرع\nدوسرا مصرع', source: 'کلیاتِ اقبال، ص ۱' };

  assert.equal((await call('POST', '/api/submissions', work)).statusCode, 401, 'signed in only');
  assert.match((await call('POST', '/api/submissions', { ...work, source: '' }, reader.token)).json().error, /ماخذ/, 'a source is required');
  const { id } = (await call('POST', '/api/submissions', work, reader.token)).json();
  const other = (await call('POST', '/api/submissions', { ...work, title: `دوسری ${run}` }, reader.token)).json().id;
  assert.equal((await call('GET', '/api/submissions/mine', undefined, reader.token)).json().find((s: any) => s.id === id).status, 'pending');
  assert.equal((await call('GET', '/api/mod/submissions', undefined, reader.token)).statusCode, 403, 'readers do not see the queue');
  assert.ok((await call('GET', '/api/mod/submissions', undefined, l2.token)).json().some((s: any) => s.id === id));
  assert.match((await call('GET', `/api/mod/submissions/${id}`, undefined, l2.token)).json().content, /<poem>\nپہلا مصرع/);

  // accepting needs the right to create works in the chosen book
  const book = { category: '/p238/shaeri/c641' };
  assert.equal((await call('POST', `/api/mod/submissions/${id}/accept`, book, l2.token)).statusCode, 403);
  await pool.query(`INSERT INTO grants (user_id, scope, scope_id, content, actions) VALUES ($1, 'poet', 238, '{works}', '{create}')`, [l2.id]);
  assert.equal((await call('POST', `/api/mod/submissions/${id}/accept`, { category: '/p0/none' }, l2.token)).statusCode, 400, 'a real book');
  assert.equal((await call('POST', `/api/mod/submissions/${id}/accept`, book, l2.token)).json().status, 'accepted');
  assert.equal((await call('POST', `/api/mod/submissions/${id}/publish`, {}, l2.token)).statusCode, 403, 'admins publish');
  assert.equal((await call('POST', `/api/mod/submissions/${other}/reject`, {}, l2.token)).statusCode, 400, 'a reason for the reader');
  assert.equal((await call('POST', `/api/mod/submissions/${other}/reject`, { note: 'یہ پہلے سے موجود ہے' }, l2.token)).json().status, 'rejected');

  const pub = (await call('POST', `/api/mod/submissions/${id}/publish`, {}, admin.token)).json();
  assert.equal(pub.status, 'published'); assert.equal(pub.url, `/p238/shaeri/c641/s${id}`);
  const json = JSON.parse(await readFile(join(data, 'divan', 'p238', 'shaeri', 'c641', `s${id}.json`), 'utf8'));
  assert.deepEqual([json.Title, json.SourceName, json.Edited.by, json.Verses.length], [`نئی نظم ${run}`, 'کلیاتِ اقبال، ص ۱', `قاری ${run}`, 2]);
  assert.match(git('log', '-1', '--format=%s%n%an'), new RegExp(`بانگ درا .*میں نیا کلام\\nقاری ${run}`));
  const mine = (await call('GET', '/api/submissions/mine', undefined, reader.token)).json();
  assert.deepEqual(mine.filter((s: any) => [id, other].includes(s.id)).map((s: any) => [s.status, s.review_note]).sort(),
    [['published', null], ['rejected', 'یہ پہلے سے موجود ہے']]);
  assert.equal((await call('POST', `/api/mod/submissions/${id}/publish`, {}, admin.token)).statusCode, 409, 'once');
});
