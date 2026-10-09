// Permissions (IAM) for content moderation (#29). Roles: reader, mod-l2 (junior moderator), mod-l1
// (senior moderator), admin. Admins grant moderators scoped permissions:
//   scope    all | poet | category (a book or section, with everything under it) | poem (one work)
//   content  poets, books, works, dictionary   (dictionary grants are site-wide: scope 'all')
//   actions  create, edit, delete, arrange
// can() is the one check every moderation endpoint uses. Admins can do everything; readers nothing.
//   GET  /api/admin/users/:id/grants
//   POST /api/admin/users/:id/grants   {scope, target, content[], actions[]}   target: poet id or a page URL
//   POST /api/admin/grants/:id/delete
import type { FastifyInstance } from 'fastify';
import { pool } from './db.ts';
import { audit, requireAdmin } from './admin.ts';

export const MODERATORS = ['mod-l2', 'mod-l1'] as const;
export const SCOPES = ['all', 'poet', 'category', 'poem'] as const;
export const CONTENT = ['poets', 'books', 'works', 'dictionary', 'site', 'tags', 'ebooks'] as const; // site: home page sections (site.ts); tags (tags.ts); ebooks (ebooks.ts)
export const ACTIONS = ['create', 'edit', 'delete', 'arrange'] as const;
export type Action = (typeof ACTIONS)[number];
export type Content = (typeof CONTENT)[number];
// what is being acted on: a poet, a book/section, a work, or nothing for site-wide content (dictionary)
export type Target = { poetId?: number; categoryId?: number; poemId?: number };

// where a target sits: its poet, its book/section chain (itself and all ancestors), the work itself
async function locate(t: Target) {
  let poemId = t.poemId ?? null, categoryId = t.categoryId ?? null, poetId = t.poetId ?? null;
  if (poemId) {
    const p = (await pool.query('SELECT category_id, poet_id FROM poems WHERE id = $1', [poemId])).rows[0];
    if (!p) return null;
    [categoryId, poetId] = [p.category_id, p.poet_id];
  }
  const chain: number[] = [];
  if (categoryId) {
    const { rows } = await pool.query(
      `WITH RECURSIVE up AS (SELECT id, parent_id, poet_id FROM categories WHERE id = $1
         UNION ALL SELECT c.id, c.parent_id, c.poet_id FROM categories c JOIN up ON c.id = up.parent_id)
       SELECT id, poet_id FROM up`, [categoryId]);
    if (!rows.length) return null;
    chain.push(...rows.map((r) => r.id));
    poetId ??= rows[0].poet_id;
  }
  return { poemId, chain, poetId };
}

// does a grant's scope cover the target?
export function covers(g: { scope: string; scope_id: number | null }, at: { poemId: number | null; chain: number[]; poetId: number | null }) {
  return g.scope === 'all'
    || (g.scope === 'poet' && g.scope_id === at.poetId)
    || (g.scope === 'category' && at.chain.includes(g.scope_id!))
    || (g.scope === 'poem' && g.scope_id === at.poemId);
}

export async function can(user: { id: number | string; role: string } | null, action: Action, content: Content, target: Target = {}) {
  if (!user) return false;
  if (user.role === 'admin') return true;
  if (!(MODERATORS as readonly string[]).includes(user.role)) return false;
  const { rows: grants } = await pool.query(
    'SELECT scope, scope_id FROM grants WHERE user_id = $1 AND $2 = ANY(actions) AND $3 = ANY(content)', [user.id, action, content]);
  if (!grants.length) return false;
  const at = await locate(target);
  return !!at && grants.some((g) => covers(g, at));
}

// a grant's target from the admin form: a poet id, or a page URL (/p266, /p266/ghazal, /p266/ghazal/sh7870)
async function resolveTarget(scope: string, target: string) {
  if (scope === 'all') return { id: null, label: 'تمام' };
  const url = '/' + String(target ?? '').trim().replace(/^https?:\/\/[^/]+/, '').replace(/^\/+|\/+$/g, '').replace(/[?#].*$/, '');
  if (scope === 'poet') {
    const r = (await pool.query('SELECT id, nickname FROM poets WHERE id = $1 OR url = $2', [Number(target) || 0, url.split('/').slice(0, 2).join('/')])).rows[0];
    return r && { id: r.id, label: r.nickname };
  }
  if (scope === 'category') {
    const r = (await pool.query('SELECT id, title, url FROM categories WHERE url = $1', [decodeURI(url)])).rows[0];
    return r && { id: r.id, label: r.title };
  }
  const r = (await pool.query('SELECT id, title FROM poems WHERE url = $1', [decodeURI(url)])).rows[0];
  return r && { id: r.id, label: r.title };
}

const pick = <T extends string>(xs: unknown, allowed: readonly T[]) =>
  [...new Set((Array.isArray(xs) ? xs : [xs]).filter((x): x is T => allowed.includes(x as T)))];

export async function grantsOf(userId: number) {
  const { rows } = await pool.query(
    `SELECT g.id, g.scope, g.scope_id, g.content, g.actions, g.created_at,
            coalesce(p.nickname, c.title, w.title) AS label, coalesce(p.url, c.url, w.url) AS url,
            coalesce(c_poet.nickname, w_poet.nickname) AS poet
     FROM grants g
     LEFT JOIN poets p ON g.scope = 'poet' AND p.id = g.scope_id
     LEFT JOIN categories c ON g.scope = 'category' AND c.id = g.scope_id LEFT JOIN poets c_poet ON c_poet.id = c.poet_id
     LEFT JOIN poems w ON g.scope = 'poem' AND w.id = g.scope_id LEFT JOIN poets w_poet ON w_poet.id = w.poet_id
     WHERE g.user_id = $1 ORDER BY g.created_at`, [userId]);
  return rows.map((r) => ({ ...r, id: Number(r.id) }));
}

export function permissionRoutes(app: FastifyInstance) {
  app.get<{ Params: { id: string } }>('/api/admin/users/:id/grants', async (req, reply) => {
    if (!(await requireAdmin(req, reply))) return;
    const u = (await pool.query('SELECT id, email, full_name, role, created_at, disabled_at, (SELECT max(created_at) FROM sessions WHERE user_id = users.id) AS last_sign_in FROM users WHERE id = $1', [Number(req.params.id) || 0])).rows[0];
    if (!u) return reply.code(404).send({ error: 'صارف نہیں ملا' });
    return { user: { ...u, id: Number(u.id) }, grants: await grantsOf(u.id) };
  });

  app.post<{ Params: { id: string }; Body: { scope?: string; target?: string; content?: unknown; actions?: unknown } }>(
    '/api/admin/users/:id/grants', async (req, reply) => {
      const admin = await requireAdmin(req, reply); if (!admin) return;
      const u = (await pool.query('SELECT id, email, role FROM users WHERE id = $1', [Number(req.params.id) || 0])).rows[0];
      if (!u) return reply.code(404).send({ error: 'صارف نہیں ملا' });
      if (!(MODERATORS as readonly string[]).includes(u.role)) return reply.code(400).send({ error: 'اجازتیں صرف موڈریٹرز کو دی جا سکتی ہیں' });
      const scope = String(req.body?.scope ?? '');
      const content = pick(req.body?.content, CONTENT), actions = pick(req.body?.actions, ACTIONS);
      if (!(SCOPES as readonly string[]).includes(scope)) return reply.code(400).send({ error: 'دائرہ منتخب کریں' });
      if (!content.length || !actions.length) return reply.code(400).send({ error: 'کم از کم ایک قسم اور ایک عمل منتخب کریں' });
      if (content.includes('dictionary') && scope !== 'all') return reply.code(400).send({ error: 'لغت کی اجازت صرف "تمام" دائرے میں دی جا سکتی ہے' });
      const t = await resolveTarget(scope, String(req.body?.target ?? ''));
      if (!t) return reply.code(400).send({ error: 'شاعر، کتاب یا کلام نہیں ملا۔ صفحے کا لنک دیکھیں۔' });
      const { rows } = await pool.query(
        'INSERT INTO grants (user_id, scope, scope_id, content, actions, granted_by) VALUES ($1, $2, $3, $4, $5, $6) RETURNING id',
        [u.id, scope, t.id, content, actions, admin.id]);
      await audit(admin, 'grant', u, { scope, target: t.label, content, actions });
      return { id: Number(rows[0].id) };
    });

  app.post<{ Params: { id: string } }>('/api/admin/grants/:id/delete', async (req, reply) => {
    const admin = await requireAdmin(req, reply); if (!admin) return;
    const g = (await pool.query(
      'DELETE FROM grants g USING users u WHERE g.id = $1 AND u.id = g.user_id RETURNING g.scope, g.content, g.actions, u.id, u.email', [Number(req.params.id) || 0])).rows[0];
    if (!g) return reply.code(404).send({ error: 'اجازت نہیں ملی' });
    await audit(admin, 'revoke', g, { scope: g.scope, content: g.content, actions: g.actions });
    return { ok: true };
  });
}
