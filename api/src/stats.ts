// Privacy-friendly statistics for admins (#86): page views, visitors and where they came from, plus moderation and
// account activity. No cookies and nothing personal is stored: the site reports a page view with the reader's address
// and browser, and only a one-day salted hash of them is kept (to count visitors per day); the salt is never stored.
//   POST /api/stats/hit   {path, ref, ip, ua}   from the site only (x-site-key)
//   GET  /api/admin/stats?days=30               the admin dashboard's numbers
import { createHash, randomBytes } from 'node:crypto';
import type { FastifyInstance } from 'fastify';
import { pool } from './db.ts';
import { fromSite } from './auth.ts';
import { requireAdmin } from './admin.ts';

const TODAY = `(now() AT TIME ZONE 'Asia/Karachi')::date`;
// ponytail: the salt is per process; a restart mid-day counts a returning visitor once more that day
let salt = { day: '', key: randomBytes(32) };
const visitor = (ip: string, ua: string) => {
  const day = new Date().toLocaleDateString('en-CA', { timeZone: 'Asia/Karachi' });
  if (salt.day !== day) salt = { day, key: randomBytes(32) };
  return createHash('sha256').update(salt.key).update(ip).update('\n').update(ua).digest('base64url').slice(0, 22);
};

export function statsRoutes(app: FastifyInstance) {
  app.post<{ Body: { path?: string; ref?: string; ip?: string; ua?: string } }>('/api/stats/hit', async (req, reply) => {
    if (!fromSite(req)) return reply.code(403).send({ error: 'forbidden' });
    const { path = '', ref = '', ip = '', ua = '' } = req.body ?? {};
    if (!path.startsWith('/') || path.length > 300) return reply.code(400).send({ error: 'path' });
    await pool.query(`INSERT INTO stat_views (day, path, views) VALUES (${TODAY}, $1, 1)
                      ON CONFLICT (day, path) DO UPDATE SET views = stat_views.views + 1`, [path]);
    await pool.query(`INSERT INTO stat_visitors (day, h) VALUES (${TODAY}, $1) ON CONFLICT DO NOTHING`, [visitor(ip, ua)]);
    if (ref) await pool.query(`INSERT INTO stat_referrers (day, host, n) VALUES (${TODAY}, $1, 1)
                               ON CONFLICT (day, host) DO UPDATE SET n = stat_referrers.n + 1`, [ref.slice(0, 200)]);
    return { ok: true };
  });

  app.get<{ Querystring: { days?: string } }>('/api/admin/stats', async (req, reply) => {
    if (!(await requireAdmin(req, reply))) return;
    const days = Math.min(365, Math.max(1, Number(req.query.days) || 30));
    const since = `${TODAY} - ${days - 1}`;
    const q = async (sql: string) => (await pool.query(sql)).rows;
    const [daily, pages, referrers, actions, people, waiting, roles, accounts, library, content] = await Promise.all([
      q(`SELECT d::date::text AS day, coalesce(v.views, 0)::int AS views, coalesce(u.visitors, 0)::int AS visitors
         FROM generate_series(${since}, ${TODAY}, interval '1 day') d
         LEFT JOIN (SELECT day, sum(views) AS views FROM stat_views GROUP BY day) v ON v.day = d::date
         LEFT JOIN (SELECT day, count(*) AS visitors FROM stat_visitors GROUP BY day) u ON u.day = d::date ORDER BY d`),
      q(`SELECT s.path, sum(s.views)::int AS views, coalesce(p.title, c.title, t.nickname) AS title
         FROM stat_views s LEFT JOIN poems p ON p.url = s.path LEFT JOIN categories c ON c.url = s.path LEFT JOIN poets t ON t.url = s.path
         WHERE s.day >= ${since} GROUP BY s.path, p.title, c.title, t.nickname ORDER BY views DESC LIMIT 20`),
      q(`SELECT host, sum(n)::int AS n FROM stat_referrers WHERE day >= ${since} GROUP BY host ORDER BY n DESC LIMIT 10`),
      q(`SELECT action, count(*)::int AS n FROM revision_events WHERE at >= ${since} GROUP BY action ORDER BY n DESC`),
      q(`SELECT actor_email AS email, count(*)::int AS n, count(*) FILTER (WHERE action = 'published')::int AS published
         FROM revision_events WHERE at >= ${since} AND actor_email <> 'server' GROUP BY actor_email ORDER BY n DESC LIMIT 10`),
      q(`SELECT status, count(*)::int AS n FROM revisions WHERE status IN ('submitted', 'approved') GROUP BY status`),
      q(`SELECT role, count(*)::int AS n FROM users GROUP BY role`),
      q(`SELECT count(*) FILTER (WHERE created_at >= ${since})::int AS new,
           (SELECT count(DISTINCT user_id) FROM sessions WHERE created_at >= ${since})::int AS signed_in FROM users`),
      q(`SELECT kind, count(*)::int AS n FROM library GROUP BY kind`),
      q(`SELECT (SELECT count(*) FROM ebooks WHERE published)::int AS ebooks, (SELECT count(*) FROM tags)::int AS tags,
           (SELECT count(*) FROM revisions WHERE status = 'published')::int AS versions`),
    ]);
    return { days, daily, pages, referrers, moderation: { actions, people, waiting: Object.fromEntries(waiting.map((r) => [r.status, r.n])) },
      accounts: { ...accounts[0], roles: Object.fromEntries(roles.map((r) => [r.role, r.n])) },
      library: Object.fromEntries(library.map((r) => [r.kind, r.n])), content: content[0] };
  });
}
