import { test, after } from 'node:test';
import assert from 'node:assert/strict';
import Fastify from 'fastify';
import { authRoutes } from './auth.ts';
import { statsRoutes } from './stats.ts';
import { pool } from './db.ts';

after(() => pool.end());

test('statistics: hits only from the site, counts without addresses, admin-only dashboard', async () => {
  const app = Fastify();
  authRoutes(app); statsRoutes(app);
  const run = Date.now(), path = `/stats-test-${run}`;
  const hit = (body: object, headers: object = {}) => app.inject({ method: 'POST', url: '/api/stats/hit', payload: body, headers: headers as any });
  try {
    process.env.DIVAN_SITE_KEY = 'site-key';
    assert.equal((await hit({ path, ip: '1.2.3.4', ua: 'x' })).statusCode, 403, 'not from the site');
    const site = { 'x-site-key': 'site-key' };
    for (const ip of ['1.2.3.4', '1.2.3.4', '5.6.7.8']) assert.equal((await hit({ path, ip, ua: 'Mozilla', ref: `ref-${run}.pk` }, site)).statusCode, 200);
    const views = (await pool.query('SELECT views FROM stat_views WHERE path = $1', [path])).rows[0].views;
    assert.equal(views, 3);
    const hashes = (await pool.query('SELECT h FROM stat_visitors WHERE day = (now() AT TIME ZONE \'Asia/Karachi\')::date')).rows.map((r) => r.h);
    assert.ok(!hashes.some((h) => h.includes('1.2.3.4')), 'no addresses stored');
    assert.equal((await pool.query('SELECT n FROM stat_referrers WHERE host = $1', [`ref-${run}.pk`])).rows[0].n, 3);

    const s = (await app.inject({ method: 'POST', url: '/api/auth/signup', payload: { email: `stats-${run}@divan.test`, password: 'pass-word-1' }, headers: { 'x-client-ip': `stats-${run}` } })).json();
    const get = () => app.inject({ method: 'GET', url: '/api/admin/stats?days=7', headers: { authorization: `Bearer ${s.token}` } });
    assert.equal((await get()).statusCode, 403, 'readers cannot see statistics');
    await pool.query(`UPDATE users SET role = 'admin' WHERE id = $1`, [s.user.id]);
    const st = (await get()).json();
    assert.equal(st.daily.length, 7);
    assert.ok(st.daily.at(-1).views >= 3 && st.daily.at(-1).visitors >= 2);
    assert.ok(st.pages.some((p: any) => p.path === path && p.views === 3));
    assert.ok(st.accounts.roles.admin >= 1 && st.accounts.new >= 1);
  } finally {
    delete process.env.DIVAN_SITE_KEY;
    await pool.query('DELETE FROM stat_views WHERE path = $1', [path]);
    await pool.query('DELETE FROM stat_referrers WHERE host = $1', [`ref-${run}.pk`]);
    await pool.query('DELETE FROM users WHERE email = $1', [`stats-${run}@divan.test`]);
    await app.close();
  }
});
