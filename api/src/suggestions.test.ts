import { test, after } from 'node:test';
import assert from 'node:assert/strict';
import Fastify from 'fastify';
import { authRoutes } from './auth.ts';
import { suggestionRoutes, openSuggestions } from './suggestions.ts';
import { pool } from './db.ts';

after(() => pool.end());

test('suggestions: a reader posts about a page, others vote, a moderator answers, hidden ones and deleting', async () => {
  const app = Fastify();
  authRoutes(app); suggestionRoutes(app);
  const run = Date.now();
  const call = (method: string, url: string, body?: object, token?: string) =>
    app.inject({ method: method as any, url, payload: body, headers: { ...(token && { authorization: `Bearer ${token}` }), 'x-client-ip': `sg-${run}` } });
  const person = async (name: string, role: string) => {
    const s = (await call('POST', '/api/auth/signup', { email: `${name}-${run}@divan.test`, password: 'pass-word-1' })).json();
    await pool.query('UPDATE users SET role = $2, full_name = $3 WHERE id = $1', [s.user.id, role, name === 'sga' ? 'ایک قاری' : null]);
    return { ...s, id: s.user.id };
  };
  const a = await person('sga', 'reader'), b = await person('sgb', 'reader'), mod = await person('sgm', 'mod-l2');
  const work = (await pool.query(`SELECT url, title FROM poems WHERE language = 'ur-PK' ORDER BY id LIMIT 1`)).rows[0];
  const body = { type: 'doc', content: [{ type: 'paragraph', content: [{ type: 'text', text: 'دوسرا مصرع غلط ہے' }] }] };
  const post = (over: object = {}, token = a.token) => call('POST', '/api/suggestions', { category: 'text', title: `متن کی تصحیح ${run}`, body, url: `https://divan.pk${work.url}`, ...over }, token);
  try {
    assert.equal((await post({}, '')).statusCode, 401, 'signed in only');
    assert.match((await post({ category: 'x' })).json().error, /قسم/);
    assert.match((await post({ body: { type: 'doc', content: [] } })).json().error, /تجویز/);
    assert.match((await post({ url: '/p999999/nothing' })).json().error, /صفحہ/);
    const { id } = (await post()).json();
    const before = await openSuggestions(work.url);
    assert.ok(before >= 1);

    let s = (await call('GET', `/api/suggestions/${id}`)).json();
    assert.deepEqual([s.target_url, s.author, s.votes, s.status, s.html], [work.url, 'ایک قاری', 0, 'open', '<p>دوسرا مصرع غلط ہے</p>']);
    assert.ok(s.target_title.startsWith(work.title));

    assert.match((await call('POST', `/api/suggestions/${id}/vote`, {}, a.token)).json().error, /اپنی/, 'not one\'s own');
    assert.deepEqual((await call('POST', `/api/suggestions/${id}/vote`, {}, b.token)).json(), { voted: true, votes: 1 });
    assert.equal((await call('GET', `/api/suggestions/${id}`, undefined, b.token)).json().voted, true);
    assert.deepEqual((await call('POST', `/api/suggestions/${id}/vote`, {}, b.token)).json(), { voted: false, votes: 0 }, 'taken back');
    await call('POST', `/api/suggestions/${id}/vote`, {}, b.token);
    const list = (await call('GET', `/api/suggestions?url=${encodeURIComponent(work.url)}`)).json();
    assert.ok(list.items.some((x: any) => x.id === id && x.votes === 1 && x.excerpt === 'دوسرا مصرع غلط ہے'));

    assert.equal((await call('POST', `/api/suggestions/${id}/status`, { status: 'done' }, b.token)).statusCode, 403, 'moderators only');
    assert.equal((await call('POST', `/api/suggestions/${id}/status`, { status: 'done', note: 'درست کر دیا' }, mod.token)).json().ok, true);
    s = (await call('GET', `/api/suggestions/${id}`)).json();
    assert.deepEqual([s.status, s.mod_note, s.moderator], ['done', 'درست کر دیا', `موڈریٹر ${mod.id}`]);
    assert.equal(await openSuggestions(work.url), before - 1, 'done: no longer open');
    assert.ok(!(await call('GET', '/api/suggestions')).json().items.some((x: any) => x.id === id), 'open ones by default');
    assert.ok((await call('GET', '/api/suggestions?status=done')).json().items.some((x: any) => x.id === id));
    assert.equal((await call('POST', `/api/suggestions/${id}/delete`, {}, a.token)).statusCode, 403, 'no longer open: the author cannot delete');

    // hidden (spam): only moderators and the author see it
    const h = (await post({ title: `اشتہار ${run}` })).json().id;
    await call('POST', `/api/suggestions/${h}/status`, { status: 'hidden' }, mod.token);
    assert.equal((await call('GET', `/api/suggestions/${h}`, undefined, b.token)).statusCode, 404);
    assert.equal((await call('GET', `/api/suggestions/${h}`, undefined, a.token)).statusCode, 200);
    assert.equal((await call('GET', `/api/suggestions/${h}`, undefined, mod.token)).statusCode, 200);
    // the lists, as a moderator and as a reader: the hidden one only for the moderator (and its author)
    const hiddenIn = async (token: string) => (await call('GET', '/api/suggestions?status=all', undefined, token)).json().items.some((x: any) => x.id === h);
    assert.equal(await hiddenIn(mod.token), true);
    assert.equal(await hiddenIn(b.token), false);
    assert.equal(await hiddenIn(a.token), true);

    const d = (await post({ title: `واپس ${run}`, url: '' })).json().id;
    assert.equal((await call('GET', `/api/suggestions/${d}`)).json().target_url, null, 'about no particular page');
    assert.equal((await call('POST', `/api/suggestions/${d}/delete`, {}, b.token)).statusCode, 403);
    assert.equal((await call('POST', `/api/suggestions/${d}/delete`, {}, a.token)).json().ok, true);
  } finally {
    await pool.query('DELETE FROM users WHERE id = ANY($1)', [[a.id, b.id, mod.id]]);
  }
});
