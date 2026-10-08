// Admin panel API (admins only). No email server yet, so password resets are done here on a reader's
// request: a temporary password is generated, shown to the admin once, and the reader's sessions end.
// Every action is written to audit_log. Moderators and their grants come with IAM (#29).
//   GET  /api/admin/users?q=&page=           users (search by email), newest first
//   POST /api/admin/users/:id/password       -> {password} (temporary, shown once)
//   POST /api/admin/users/:id/disable        {disabled: boolean}
//   POST /api/admin/users/:id/role           {role: 'reader' | 'admin'}
//   POST /api/admin/users/:id/delete
//   GET  /api/admin/audit?page=
import { randomInt } from 'node:crypto';
import type { FastifyInstance, FastifyReply, FastifyRequest } from 'fastify';
import { pool } from './db.ts';
import { hashPassword, sessionUser } from './auth.ts';

export const ROLES = ['reader', 'admin'] as const;
const PAGE = 50;

// readable temporary password: 12 characters without look-alikes (0/O, 1/l/I)
export function temporaryPassword() {
  const chars = 'abcdefghjkmnpqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789';
  return Array.from({ length: 12 }, () => chars[randomInt(chars.length)]).join('');
}

async function requireAdmin(req: FastifyRequest, reply: FastifyReply) {
  const u = await sessionUser(req);
  if (!u) return void reply.code(401).send({ error: 'دوبارہ لاگ ان کریں' });
  if (u.role !== 'admin') return void reply.code(403).send({ error: 'صرف ایڈمن کے لیے' });
  return u;
}

export const audit = (actor: any, action: string, target: any, detail?: object) =>
  pool.query('INSERT INTO audit_log (actor_id, actor_email, action, target_id, target_email, detail) VALUES ($1, $2, $3, $4, $5, $6)',
    [actor?.id ?? null, actor?.email ?? 'server', action, target?.id ?? null, target?.email ?? null, detail ?? null]);

export function adminRoutes(app: FastifyInstance) {
  app.get<{ Querystring: { q?: string; page?: string } }>('/api/admin/users', async (req, reply) => {
    if (!(await requireAdmin(req, reply))) return;
    const q = (req.query.q ?? '').trim().toLowerCase(), page = Math.max(1, Number(req.query.page) || 1);
    const like = '%' + q.replace(/[\\%_]/g, (c) => '\\' + c) + '%';
    const [count, rows] = await Promise.all([
      pool.query('SELECT count(*)::int AS n FROM users WHERE email LIKE $1', [like]),
      pool.query(
        `SELECT u.id, u.email, u.role, u.created_at, u.disabled_at, max(s.created_at) AS last_sign_in
         FROM users u LEFT JOIN sessions s ON s.user_id = u.id WHERE u.email LIKE $1
         GROUP BY u.id ORDER BY u.created_at DESC LIMIT ${PAGE} OFFSET ${(page - 1) * PAGE}`, [like]),
    ]);
    return { total: count.rows[0].n, page, pageSize: PAGE, users: rows.rows.map((r) => ({ ...r, id: Number(r.id) })) };
  });

  // one target user, never the acting admin themself for actions that could lock them out
  const target = async (req: FastifyRequest<{ Params: { id: string } }>, reply: FastifyReply, admin: any, notSelf: boolean) => {
    const u = (await pool.query('SELECT * FROM users WHERE id = $1', [Number(req.params.id) || 0])).rows[0];
    if (!u) return void reply.code(404).send({ error: 'صارف نہیں ملا' });
    if (notSelf && Number(u.id) === Number(admin.id)) return void reply.code(400).send({ error: 'یہ اپنے اکاؤنٹ پر نہیں ہو سکتا' });
    return u;
  };

  app.post<{ Params: { id: string } }>('/api/admin/users/:id/password', async (req, reply) => {
    const admin = await requireAdmin(req, reply); if (!admin) return;
    const u = await target(req, reply, admin, false); if (!u) return;
    const password = temporaryPassword();
    await pool.query('UPDATE users SET password_hash = $1 WHERE id = $2', [await hashPassword(password), u.id]);
    await pool.query('DELETE FROM sessions WHERE user_id = $1', [u.id]);
    await audit(admin, 'password-reset', u);
    return { password };
  });

  app.post<{ Params: { id: string }; Body: { disabled?: boolean } }>('/api/admin/users/:id/disable', async (req, reply) => {
    const admin = await requireAdmin(req, reply); if (!admin) return;
    const u = await target(req, reply, admin, true); if (!u) return;
    const disabled = req.body?.disabled !== false;
    await pool.query('UPDATE users SET disabled_at = $1 WHERE id = $2', [disabled ? new Date() : null, u.id]);
    if (disabled) await pool.query('DELETE FROM sessions WHERE user_id = $1', [u.id]);
    await audit(admin, disabled ? 'disable' : 'enable', u);
    return { ok: true };
  });

  app.post<{ Params: { id: string }; Body: { role?: string } }>('/api/admin/users/:id/role', async (req, reply) => {
    const admin = await requireAdmin(req, reply); if (!admin) return;
    const u = await target(req, reply, admin, true); if (!u) return;
    const role = req.body?.role ?? '';
    if (!(ROLES as readonly string[]).includes(role)) return reply.code(400).send({ error: 'نامعلوم کردار' });
    await pool.query('UPDATE users SET role = $1 WHERE id = $2', [role, u.id]);
    await audit(admin, 'role', u, { from: u.role, to: role });
    return { ok: true };
  });

  app.post<{ Params: { id: string } }>('/api/admin/users/:id/delete', async (req, reply) => {
    const admin = await requireAdmin(req, reply); if (!admin) return;
    const u = await target(req, reply, admin, true); if (!u) return;
    await pool.query('DELETE FROM users WHERE id = $1', [u.id]);
    await audit(admin, 'delete', u);
    return { ok: true };
  });

  app.get<{ Querystring: { page?: string } }>('/api/admin/audit', async (req, reply) => {
    if (!(await requireAdmin(req, reply))) return;
    const page = Math.max(1, Number(req.query.page) || 1);
    const [count, rows] = await Promise.all([
      pool.query('SELECT count(*)::int AS n FROM audit_log'),
      pool.query(`SELECT at, actor_email, action, target_email, detail FROM audit_log ORDER BY at DESC, id DESC LIMIT ${PAGE} OFFSET ${(page - 1) * PAGE}`),
    ]);
    return { total: count.rows[0].n, page, pageSize: PAGE, entries: rows.rows };
  });
}
