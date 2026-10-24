// Privacy-friendly statistics for admins (#86): page views, visitors and where they came from, plus moderation and
// account activity. No cookies and nothing personal is stored: the site reports a page view with the reader's address
// and browser, and only a one-day salted hash of them is kept (to count visitors per day); the salt is never stored.
// Where from: nginx looks the address up (DB-IP's free city database, geoip2 module) and passes the country and city;
// only those names are kept, with the counts.
//   POST /api/stats/hit   {path, ref, ip, ua, country, city, lat, lon}   from the site only (x-site-key); lat, lon: the city's
//   GET  /api/admin/stats?days=30 | from=YYYY-MM-DD&to=YYYY-MM-DD, &by=day|week|month, &country=PK
//                                               the admin dashboard's numbers; country narrows the chart and the cities;
//                                               `places`: views and visitors per day and city, for the map and its timeline
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

const today = () => new Date().toLocaleDateString('en-CA', { timeZone: 'Asia/Karachi' });
const DATE = /^\d{4}-\d{2}-\d{2}$/;
const valid = (d?: string) => !!d && DATE.test(d) && !Number.isNaN(Date.parse(d));
// from-to (to defaults to today), else the last `days` (1-365, default 30) up to today; at most three years
export function period(p: { days?: string; from?: string; to?: string }) {
  const to = valid(p.to) ? p.to! : today();
  if (valid(p.from) && p.from! <= to && Date.parse(to) - Date.parse(p.from!) <= 3 * 366 * 86_400_000) return { from: p.from!, to };
  const days = Math.min(365, Math.max(1, Number(p.days) || 30));
  return { from: new Date(Date.parse(to) - (days - 1) * 86_400_000).toISOString().slice(0, 10), to };
}

export function statsRoutes(app: FastifyInstance) {
  app.post<{ Body: { path?: string; ref?: string; ip?: string; ua?: string; country?: string; city?: string; lat?: unknown; lon?: unknown } }>('/api/stats/hit', async (req, reply) => {
    if (!fromSite(req)) return reply.code(403).send({ error: 'forbidden' });
    const { path = '', ref = '', ip = '', ua = '' } = req.body ?? {};
    // '' = not known (no lookup on this server, or an address the database does not place)
    const country = /^[A-Z]{2}$/.test(req.body?.country ?? '') ? req.body!.country! : '', city = country ? String(req.body?.city ?? '').trim().slice(0, 100) : '';
    if (!path.startsWith('/') || path.length > 300) return reply.code(400).send({ error: 'path' });
    await pool.query(`INSERT INTO stat_views (day, path, views) VALUES (${TODAY}, $1, 1)
                      ON CONFLICT (day, path) DO UPDATE SET views = stat_views.views + 1`, [path]);
    await pool.query(`INSERT INTO stat_places (day, country, city, views) VALUES (${TODAY}, $1, $2, 1)
                      ON CONFLICT (day, country, city) DO UPDATE SET views = stat_places.views + 1`, [country, city]);
    const lat = Number(req.body?.lat), lon = Number(req.body?.lon);
    if (city && req.body?.lat !== '' && Math.abs(lat) <= 90 && Math.abs(lon) <= 180)
      await pool.query(`INSERT INTO stat_cities (country, city, lat, lon) VALUES ($1, $2, $3, $4)
                        ON CONFLICT (country, city) DO UPDATE SET lat = $3, lon = $4`, [country, city, Math.round(lat * 100) / 100, Math.round(lon * 100) / 100]);
    await pool.query(`INSERT INTO stat_visitors (day, h, country, city) VALUES (${TODAY}, $1, $2, $3) ON CONFLICT DO NOTHING`, [visitor(ip, ua), country, city]);
    if (ref) await pool.query(`INSERT INTO stat_referrers (day, host, n) VALUES (${TODAY}, $1, 1)
                               ON CONFLICT (day, host) DO UPDATE SET n = stat_referrers.n + 1`, [ref.slice(0, 200)]);
    return { ok: true };
  });

  app.get<{ Querystring: { days?: string; from?: string; to?: string; by?: string; country?: string } }>('/api/admin/stats', async (req, reply) => {
    if (!(await requireAdmin(req, reply))) return;
    // the period: from-to (both dates), else the last `days` days. Checked strictly, so safe to write into the SQL below
    const { from: f, to: t } = period(req.query);
    const by = ['week', 'month'].includes(req.query.by ?? '') ? req.query.by! : 'day';
    const country = /^[A-Z]{2}$/.test(req.query.country ?? '') ? req.query.country! : null;
    const since = `'${f}'::date`, until = `'${t}'::date`, within = `day BETWEEN ${since} AND ${until}`, inCountry = country ? ` AND country = '${country}'` : '';
    const days = Math.round((Date.parse(t) - Date.parse(f)) / 86_400_000) + 1;
    const bucket = `date_trunc('${by}', day::timestamp)`;
    const q = async (sql: string) => (await pool.query(sql)).rows;
    const [daily, pages, referrers, actions, people, waiting, roles, accounts, library, content, countries, cities, places] = await Promise.all([
      // views and visitors per day, week or month (a week's visitors: the sum of each day's)
      q(`SELECT b::date::text AS day, coalesce(v.views, 0)::int AS views, coalesce(u.visitors, 0)::int AS visitors
         FROM generate_series(date_trunc('${by}', ${since}::timestamp), ${until}::timestamp, interval '1 ${by}') b
         LEFT JOIN (SELECT ${bucket} AS b, sum(views) AS views FROM ${country ? 'stat_places' : 'stat_views'} WHERE ${within}${inCountry} GROUP BY 1) v USING (b)
         LEFT JOIN (SELECT ${bucket} AS b, count(*) AS visitors FROM stat_visitors WHERE ${within}${inCountry} GROUP BY 1) u USING (b) ORDER BY b`),
      q(`SELECT s.path, sum(s.views)::int AS views, coalesce(p.title, c.title, t.nickname) AS title
         FROM stat_views s LEFT JOIN poems p ON p.url = s.path LEFT JOIN categories c ON c.url = s.path LEFT JOIN poets t ON t.url = s.path
         WHERE s.${within} GROUP BY s.path, p.title, c.title, t.nickname ORDER BY views DESC LIMIT 20`),
      q(`SELECT host, sum(n)::int AS n FROM stat_referrers WHERE ${within} GROUP BY host ORDER BY n DESC LIMIT 10`),
      q(`SELECT action, count(*)::int AS n FROM revision_events WHERE at >= ${since} AND at < ${until} + 1 GROUP BY action ORDER BY n DESC`),
      q(`SELECT actor_email AS email, count(*)::int AS n, count(*) FILTER (WHERE action = 'published')::int AS published
         FROM revision_events WHERE at >= ${since} AND at < ${until} + 1 AND actor_email <> 'server' GROUP BY actor_email ORDER BY n DESC LIMIT 10`),
      q(`SELECT status, count(*)::int AS n FROM revisions WHERE status IN ('submitted', 'approved') GROUP BY status`),
      q(`SELECT role, count(*)::int AS n FROM users GROUP BY role`),
      q(`SELECT count(*) FILTER (WHERE created_at >= ${since} AND created_at < ${until} + 1)::int AS new,
           (SELECT count(DISTINCT user_id) FROM sessions WHERE created_at >= ${since} AND created_at < ${until} + 1)::int AS signed_in FROM users`),
      q(`SELECT kind, count(*)::int AS n FROM library GROUP BY kind`),
      q(`SELECT (SELECT count(*) FROM ebooks WHERE published)::int AS ebooks, (SELECT count(*) FROM tags)::int AS tags,
           (SELECT count(*) FROM revisions WHERE status = 'published')::int AS versions`),
      q(`SELECT p.country, sum(p.views)::int AS views, coalesce(min(v.n), 0)::int AS visitors FROM stat_places p
         LEFT JOIN (SELECT country, count(*) AS n FROM stat_visitors WHERE ${within} GROUP BY country) v USING (country)
         WHERE ${within} GROUP BY p.country ORDER BY views DESC LIMIT 40`),
      q(`SELECT p.country, p.city, sum(p.views)::int AS views, coalesce(min(v.n), 0)::int AS visitors FROM stat_places p
         LEFT JOIN (SELECT country, city, count(*) AS n FROM stat_visitors WHERE ${within}${inCountry} GROUP BY country, city) v USING (country, city)
         WHERE ${within}${inCountry.replace('country', 'p.country')} GROUP BY p.country, p.city ORDER BY views DESC LIMIT 40`),
      // ponytail: every day and city of the period in one answer; fine for a young site (thousands of rows), make it
      // weekly for long periods if it grows large
      q(`SELECT p.day::text AS day, p.country, p.city, p.views, coalesce(v.n, 0)::int AS visitors, c.lat, c.lon FROM stat_places p
         LEFT JOIN (SELECT day, country, city, count(*) AS n FROM stat_visitors WHERE ${within} GROUP BY 1, 2, 3) v USING (day, country, city)
         LEFT JOIN stat_cities c USING (country, city) WHERE p.${within} ORDER BY p.day LIMIT 50000`),
    ]);
    return { days, from: f, to: t, by, country, countries, cities, places, daily, pages, referrers, moderation: { actions, people, waiting: Object.fromEntries(waiting.map((r) => [r.status, r.n])) },
      accounts: { ...accounts[0], roles: Object.fromEntries(roles.map((r) => [r.role, r.n])) },
      library: Object.fromEntries(library.map((r) => [r.kind, r.n])), content: content[0] };
  });
}
