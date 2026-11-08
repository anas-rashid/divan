// Readers' suggestions (#20, #19): any signed-in reader posts a suggestion to fix part of the content (a poet's name,
// picture or intro, a work's text, title, metre, order, tags…), as rich text (richtext.ts) with a link to the page it is
// about; other readers vote for it; moderators pick it up, make the change through the usual pipeline, and set its
// status with a note. Everyone can read them.
//   GET  /api/suggestions?category=&status=&url=&sort=votes|new&page=   a page of 30, with each one's votes and whether
//                                           the reader voted; open ones by default (status=all for every status)
//   GET  /api/suggestions/:id               one, its text as HTML
//   POST /api/suggestions  {category, title, body (editor JSON), url?}   a reader, 10 a day       -> {id}
//   POST /api/suggestions/:id/vote          a vote, or taking it back (not for one's own)        -> {voted, votes}
//   POST /api/suggestions/:id/status  {status, note?}   a moderator                              -> {ok}
//   POST /api/suggestions/:id/delete        its author while still open, or an admin             -> {ok}
// openSuggestions(url) gives a page the number of open suggestions about it.
import type { FastifyInstance } from 'fastify';
import { pool } from './db.ts';
import { limiter, sessionUser } from './auth.ts';
import { cleanRich, plainText, richHtml } from './richtext.ts';

export const CATEGORIES = ['name', 'portrait', 'intro', 'text', 'title', 'metre', 'order', 'tags', 'other'] as const;
export const STATUSES = ['open', 'planned', 'done', 'declined', 'hidden'] as const;
const PAGE = 30;
const isModerator = (u: any) => ['mod-l2', 'mod-l1', 'admin'].includes(u?.role);
const postsByUser = limiter(10, 24 * 3600_000), votesByUser = limiter(300, 3600_000);
const author = `coalesce(nullif(trim(u.full_name), ''), 'قاری ' || u.id)`;

// the page a suggestion is about: a path on this site (a full address is cut to its path) that exists, with its title
export async function pageOf(raw: unknown) {
  let path = String(raw ?? '').trim();
  if (!path) return { url: null, title: null };
  try { path = decodeURI(new URL(path, 'https://divan.pk').pathname); } catch { return null; }
  path = '/' + path.replace(/^\/+|\/+$/g, '');
  const work = (await pool.query(`SELECT p.title || ' · ' || t.nickname AS title FROM poems p JOIN poets t ON t.id = p.poet_id WHERE p.url = $1`, [path])).rows[0];
  if (work) return { url: path, title: work.title };
  const cat = (await pool.query(`SELECT CASE WHEN c.parent_id IS NULL THEN t.nickname ELSE c.title || ' · ' || t.nickname END AS title
    FROM categories c JOIN poets t ON t.id = c.poet_id WHERE c.url = $1`, [path])).rows[0];
  if (cat) return { url: path, title: cat.title };
  const ebook = path.match(/^\/ebook\/(\d+)$/) && (await pool.query('SELECT title FROM ebooks WHERE id = $1 AND published', [Number(path.slice(7))])).rows[0];
  if (ebook) return { url: path, title: ebook.title };
  const named = path.match(/^\/(bahr|tag\/[^/]+)\/([^/]+)$/);
  if (named) return { url: path, title: named[2] };
  return null;
}

export const openSuggestions = async (url: string) =>
  (await pool.query(`SELECT count(*)::int AS n FROM suggestions WHERE target_url = $1 AND status IN ('open', 'planned')`, [url])).rows[0].n;

export function suggestionRoutes(app: FastifyInstance) {
  app.get<{ Querystring: { category?: string; status?: string; url?: string; sort?: string; page?: string } }>('/api/suggestions', async (req) => {
    const u = await sessionUser(req), q = req.query, page = Math.max(1, Number(q.page) || 1);
    const where = ['true'], args: unknown[] = [u?.id ?? 0];
    if ((CATEGORIES as readonly string[]).includes(q.category ?? '')) where.push(`s.category = $${args.push(q.category)}`);
    const status = q.status === 'all' ? null : (STATUSES as readonly string[]).includes(q.status ?? '') ? q.status : 'open';
    if (status) where.push(`s.status = $${args.push(status)}`);
    // hidden ones only for moderators and their authors
    if (!isModerator(u)) where.push(`(s.status <> 'hidden' OR s.user_id = $1)`);
    if (q.url) where.push(`s.target_url = $${args.push(q.url)}`);
    const sql = `FROM suggestions s JOIN users u ON u.id = s.user_id WHERE ${where.join(' AND ')}`;
    const [total, rows] = await Promise.all([
      pool.query(`SELECT count(*)::int AS n ${sql}`, args),
      pool.query(`SELECT s.id, s.category, s.title, left(s.body_text, 240) AS excerpt, s.target_url, s.target_title, s.status, s.created_at,
          ${author} AS author, (SELECT count(*)::int FROM suggestion_votes v WHERE v.suggestion_id = s.id) AS votes,
          EXISTS (SELECT 1 FROM suggestion_votes v WHERE v.suggestion_id = s.id AND v.user_id = $1) AS voted
        ${sql} ORDER BY ${q.sort === 'new' ? 's.created_at DESC' : 'votes DESC, s.created_at DESC'} LIMIT ${PAGE} OFFSET ${(page - 1) * PAGE}`, args),
    ]);
    return { total: total.rows[0].n, page, pageSize: PAGE, items: rows.rows };
  });

  app.get<{ Params: { id: string } }>('/api/suggestions/:id', async (req, reply) => {
    const u = await sessionUser(req);
    const s = (await pool.query(`SELECT s.*, ${author} AS author, (SELECT count(*)::int FROM suggestion_votes v WHERE v.suggestion_id = s.id) AS votes,
        EXISTS (SELECT 1 FROM suggestion_votes v WHERE v.suggestion_id = s.id AND v.user_id = $2) AS voted,
        (SELECT coalesce(nullif(trim(m.full_name), ''), 'موڈریٹر ' || m.id) FROM users m WHERE m.id = s.mod_id) AS moderator
      FROM suggestions s JOIN users u ON u.id = s.user_id WHERE s.id = $1`, [Number(req.params.id) || 0, u?.id ?? 0])).rows[0];
    if (!s || (s.status === 'hidden' && !isModerator(u) && Number(s.user_id) !== Number(u?.id))) return reply.code(404).send({ error: 'تجویز نہیں ملی' });
    const { body, body_text, user_id, mod_id, ...rest } = s;
    return { ...rest, html: richHtml(body), mine: Number(user_id) === Number(u?.id), canModerate: isModerator(u), canDelete: (Number(user_id) === Number(u?.id) && s.status === 'open') || u?.role === 'admin' };
  });

  app.post<{ Body: { category?: string; title?: string; body?: unknown; url?: string } }>('/api/suggestions', async (req, reply) => {
    const u = await sessionUser(req);
    if (!u) return reply.code(401).send({ error: 'پہلے لاگ ان کریں' });
    const b = req.body ?? {}, title = String(b.title ?? '').normalize('NFC').replace(/\s+/g, ' ').trim();
    if (!(CATEGORIES as readonly string[]).includes(b.category ?? '')) return reply.code(400).send({ error: 'قسم چنیں' });
    if (title.length < 3 || title.length > 150) return reply.code(400).send({ error: 'عنوان ۳ سے ۱۵۰ حروف کا ہو' });
    const body = cleanRich(b.body);
    if (!body) return reply.code(400).send({ error: 'تجویز لکھیں' });
    const target = await pageOf(b.url);
    if (!target) return reply.code(400).send({ error: 'یہ صفحہ دیوان پر نہیں ملا؛ کسی شاعر، کتاب یا کلام کا لنک دیں' });
    if (!postsByUser(String(u.id))) return reply.code(429).send({ error: 'آج کے لیے کافی تجاویز ہو گئیں؛ کل پھر لکھیں' });
    const { rows } = await pool.query(
      `INSERT INTO suggestions (user_id, category, title, body, body_text, target_url, target_title) VALUES ($1,$2,$3,$4,$5,$6,$7) RETURNING id`,
      [u.id, b.category, title, body, plainText(body), target.url, target.title]);
    return { id: rows[0].id };
  });

  app.post<{ Params: { id: string } }>('/api/suggestions/:id/vote', async (req, reply) => {
    const u = await sessionUser(req);
    if (!u) return reply.code(401).send({ error: 'پہلے لاگ ان کریں' });
    const s = (await pool.query(`SELECT id, user_id FROM suggestions WHERE id = $1 AND status <> 'hidden'`, [Number(req.params.id) || 0])).rows[0];
    if (!s) return reply.code(404).send({ error: 'تجویز نہیں ملی' });
    if (Number(s.user_id) === Number(u.id)) return reply.code(400).send({ error: 'اپنی تجویز پر ووٹ نہیں' });
    if (!votesByUser(String(u.id))) return reply.code(429).send({ error: 'کچھ دیر بعد' });
    const gone = (await pool.query('DELETE FROM suggestion_votes WHERE suggestion_id = $1 AND user_id = $2', [s.id, u.id])).rowCount;
    if (!gone) await pool.query('INSERT INTO suggestion_votes (suggestion_id, user_id) VALUES ($1, $2) ON CONFLICT DO NOTHING', [s.id, u.id]);
    const votes = (await pool.query('SELECT count(*)::int AS n FROM suggestion_votes WHERE suggestion_id = $1', [s.id])).rows[0].n;
    return { voted: !gone, votes };
  });

  app.post<{ Params: { id: string }; Body: { status?: string; note?: string } }>('/api/suggestions/:id/status', async (req, reply) => {
    const u = await sessionUser(req);
    if (!isModerator(u)) return reply.code(403).send({ error: 'صرف موڈریٹرز کے لیے' });
    const status = req.body?.status ?? '';
    if (!(STATUSES as readonly string[]).includes(status)) return reply.code(400).send({ error: 'حالت چنیں' });
    const note = String(req.body?.note ?? '').normalize('NFC').trim().slice(0, 2000) || null;
    const n = (await pool.query(`UPDATE suggestions SET status = $2, mod_note = $3, mod_id = $4, updated_at = now() WHERE id = $1`,
      [Number(req.params.id) || 0, status, note, u.id])).rowCount;
    return n ? { ok: true } : reply.code(404).send({ error: 'تجویز نہیں ملی' });
  });

  app.post<{ Params: { id: string } }>('/api/suggestions/:id/delete', async (req, reply) => {
    const u = await sessionUser(req);
    if (!u) return reply.code(401).send({ error: 'پہلے لاگ ان کریں' });
    const n = (await pool.query(`DELETE FROM suggestions WHERE id = $1 AND ($3 OR (user_id = $2 AND status = 'open'))`,
      [Number(req.params.id) || 0, u.id, u.role === 'admin'])).rowCount;
    return n ? { ok: true } : reply.code(403).send({ error: 'یہ تجویز آپ نہیں ہٹا سکتے' });
  });
}
