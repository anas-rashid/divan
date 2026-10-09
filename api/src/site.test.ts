import { test, after } from 'node:test';
import assert from 'node:assert/strict';
import Fastify from 'fastify';
import { authRoutes } from './auth.ts';
import { siteRoutes } from './site.ts';
import { pool } from './db.ts';

after(() => pool.end());

test('home sections: default layout, the site permission, rename, sort, chosen poets, reorder', async () => {
  const app = Fastify();
  authRoutes(app); siteRoutes(app);
  const run = Date.now();
  const call = (method: string, url: string, body?: object, token?: string) =>
    app.inject({ method: method as any, url, payload: body, headers: { ...(token && { authorization: `Bearer ${token}` }), 'x-client-ip': `site-${run}` } });
  const person = async (name: string, role: string) => {
    const s = (await call('POST', '/api/auth/signup', { email: `${name}-${run}@divan.test`, password: 'pass-word-1' })).json();
    await pool.query('UPDATE users SET role = $2 WHERE id = $1', [s.user.id, role]);
    return { ...s, id: s.user.id };
  };
  const saved = (await pool.query('SELECT position, title, century, sort, poets FROM home_sections ORDER BY position')).rows;
  const mod = await person('mod', 'mod-l2'), other = await person('other', 'mod-l1');
  try {
    // the default layout: featured poets, then centuries of birth
    await pool.query('DELETE FROM home_sections');
    const home = (await call('GET', '/api/home')).json();
    assert.equal(home[0].title, 'مقبول شعرا');
    assert.ok(home.slice(1).every((s: any) => s.century !== null && s.title === null && s.sort === 'timeline'));
    const all = home.slice(1).reduce((n: number, s: any) => n + s.poets.length, 0);
    assert.equal(all, Number((await pool.query('SELECT count(*) FROM poets')).rows[0].count), 'every poet in a century');

    // only with the site permission
    assert.equal((await call('GET', '/api/mod/site', undefined, other.token)).statusCode, 403);
    await pool.query(`INSERT INTO grants (user_id, scope, content, actions) VALUES ($1, 'all', '{site}', '{edit}')`, [mod.id]);
    const ed = (await call('GET', '/api/mod/site', undefined, mod.token)).json();

    // rename the 13th century, sort it alphabetically, put it first; a new chosen section; no nameless list
    const c13 = ed.sections.find((s: any) => s.century === 13);
    const iqbal = ed.poets.find((p: any) => p.name === 'محمد اقبال').id, ghalib = ed.poets.find((p: any) => p.name === 'مرزا غالب').id;
    const layout = [{ ...c13, title: 'تیرھویں صدی کے شعرا', sort: 'alpha' }, { title: 'نئی فہرست', century: null, sort: 'manual', poets: [iqbal, ghalib, iqbal, 999999] },
      ...ed.sections.filter((s: any) => s !== c13)];
    assert.equal((await call('POST', '/api/mod/site', { sections: [{ title: '', century: null, poets: [] }] }, mod.token)).statusCode, 400);
    const res = (await call('POST', '/api/mod/site', { sections: layout }, mod.token)).json().sections;
    assert.equal(res[0].title, 'تیرھویں صدی کے شعرا');
    const names = res[0].poets.map((p: any) => p.nickname);
    assert.deepEqual(names, [...names].sort(new Intl.Collator('ur').compare), 'alphabetical');
    assert.deepEqual(res[1].poets.map((p: any) => p.id), [iqbal, ghalib], 'chosen order; repeats and unknown poets dropped');
    assert.equal((await pool.query(`SELECT count(*) FROM audit_log WHERE action = 'site-sections' AND actor_email = $1`, [`mod-${run}@divan.test`])).rows[0].count, '1');
  } finally {
    await pool.query('DELETE FROM home_sections');
    for (const s of saved) await pool.query('INSERT INTO home_sections (position, title, century, sort, poets) VALUES ($1, $2, $3, $4, $5)', [s.position, s.title, s.century, s.sort, s.poets]);
    await pool.query('DELETE FROM users WHERE email LIKE $1', [`%-${run}@divan.test`]);
    await pool.query('DELETE FROM audit_log WHERE actor_email LIKE $1', [`%-${run}@divan.test`]);
    await app.close();
  }
});
