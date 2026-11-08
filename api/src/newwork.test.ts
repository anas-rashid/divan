import { test, after } from 'node:test';
import assert from 'node:assert/strict';
import Fastify from 'fastify';
import { authRoutes } from './auth.ts';
import { adminRoutes } from './admin.ts';
import { moderationRoutes } from './moderation.ts';
import { submissionRoutes } from './submissions.ts';
import { pool } from './db.ts';

after(() => pool.end());

test('a moderator\'s new work: the create grant, title/text/source checked, an L2\'s pending and an L1\'s accepted', async () => {
  const app = Fastify();
  authRoutes(app); adminRoutes(app); moderationRoutes(app); submissionRoutes(app);
  const run = Date.now();
  const call = (method: string, url: string, body?: object, token?: string) =>
    app.inject({ method: method as any, url, payload: body, headers: { ...(token && { authorization: `Bearer ${token}` }), 'x-client-ip': `nw-${run}` } });
  const person = async (name: string, role: string) => {
    const s = (await call('POST', '/api/auth/signup', { email: `${name}-${run}@divan.test`, password: 'pass-word-1' })).json();
    await pool.query('UPDATE users SET role = $2 WHERE id = $1', [s.user.id, role]);
    return { ...s, id: s.user.id };
  };
  const l2 = await person('nwl2', 'mod-l2'), l1 = await person('nwl1', 'mod-l1');
  const cat = (await pool.query(`SELECT id, url, title FROM categories WHERE url = '/p266/ghazal'`)).rows[0];
  const text = (src = '| ماخذ = کلیاتِ غالب، صفحہ ۱') => `{{دیوان\n| عنوان = نیا شعر ${run}\n${src}\n}}\n<poem>\nپہلا مصرع\nدوسرا مصرع\n</poem>\n`;
  try {
    assert.equal((await call('GET', `/api/mod/can?category=${cat.id}`, undefined, l2.token)).json().create, false);
    assert.equal((await call('POST', '/api/mod/submissions/new', { category: cat.url, content: text() }, l2.token)).statusCode, 403, 'no grant');
    for (const m of [l1, l2]) await pool.query(`INSERT INTO grants (user_id, scope, scope_id, content, actions) VALUES ($1, 'category', $2, '{works}', '{create}')`, [m.id, cat.id]);
    assert.equal((await call('GET', `/api/mod/can?category=${cat.id}`, undefined, l2.token)).json().create, true);
    assert.match((await call('POST', '/api/mod/submissions/new', { category: cat.id, content: text('') }, l2.token)).json().error, /ماخذ/, 'a source');
    assert.match((await call('POST', '/api/mod/submissions/new', { category: cat.id, content: '<poem>\nمصرع\n</poem>' }, l2.token)).json().error, /عنوان/, 'a title');
    const a = (await call('POST', '/api/mod/submissions/new', { category: cat.id, content: text() }, l2.token)).json();
    assert.equal(a.status, 'pending', 'an L2\'s work waits for an L1');
    const b = (await call('POST', '/api/mod/submissions/new', { category: cat.id, content: text() }, l1.token)).json();
    assert.equal(b.status, 'accepted');
    const s = (await call('GET', `/api/mod/submissions/${b.id}`, undefined, l1.token)).json();
    assert.deepEqual([s.title, s.kind, s.source, s.category_url, s.poet], [`نیا شعر ${run}`, 'poetry', 'کلیاتِ غالب، صفحہ ۱', cat.url, 'مرزا غالب']);
    assert.equal(s.content, text().trim(), 'the moderator\'s Divan text, as written');
  } finally {
    await pool.query('DELETE FROM submissions WHERE title = $1', [`نیا شعر ${run}`]);
    await pool.query('DELETE FROM users WHERE email LIKE $1', [`%-${run}@divan.test`]);
  }
});
