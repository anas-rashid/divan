// Requests to join (owner decision 2026-10-17): the public site has no sign-up (DIVAN_SIGNUP=0), so people ask for an
// account on the sign-up page; every admin is emailed, and an admin approves (the account and its emailed invitation,
// admin.ts invite) or declines.
//   POST /api/auth/join                       {email, name, note}: anyone; always {ok: true}, so it tells no one who has
//                                             an account (an email with an account or a pending request is not stored again)
//   GET  /api/admin/requests?status=          pending (default) | approved | declined, with the counts
//   POST /api/admin/requests/:id/approve      -> {id, emailed, link?} as POST /api/admin/users
//   POST /api/admin/requests/:id/decline      (no email: they are not told)
import type { FastifyInstance } from 'fastify';
import { pool } from './db.ts';
import { ip, limiter, normaliseEmail, validEmail } from './auth.ts';
import { audit, invite, requireAdmin } from './admin.ts';
import { sendMail, SITE } from './mail.ts';

const joinByIp = limiter(5, 60 * 60_000);
const line = (s: unknown, max: number) => String(s ?? '').replace(/\s+/g, ' ').trim().slice(0, max);
const STATUSES = ['pending', 'approved', 'declined'];

export function joinRoutes(app: FastifyInstance) {
  app.post<{ Body: { email?: string; name?: string; note?: string } }>('/api/auth/join', async (req, reply) => {
    if (!joinByIp(ip(req))) return reply.code(429).send({ error: 'بہت زیادہ کوششیں۔ کچھ دیر بعد دوبارہ کوشش کریں۔' });
    const email = normaliseEmail(req.body?.email), name = line(req.body?.name, 100);
    const note = String(req.body?.note ?? '').replace(/\r\n?/g, '\n').trim().slice(0, 1000);
    if (!validEmail(email)) return reply.code(400).send({ error: 'درست ای میل پتہ لکھیں' });
    if (!name) return reply.code(400).send({ error: 'اپنا نام لکھیں' });
    const has = (await pool.query('SELECT 1 FROM users WHERE email = $1', [email])).rowCount;
    const made = !has && (await pool.query(`INSERT INTO join_requests (email, name, note) VALUES ($1, $2, $3)
      ON CONFLICT (email) WHERE status = 'pending' DO NOTHING RETURNING id`, [email, name, note || null])).rows[0];
    if (made) {
      const admins = (await pool.query(`SELECT email FROM users WHERE role = 'admin' AND disabled_at IS NULL`)).rows.map((r) => r.email);
      void sendMail(admins, `دیوان: اکاؤنٹ کی نئی درخواست (${name})`, [
        `${name} (${email}) دیوان پر اکاؤنٹ چاہتے ہیں۔`, ...(note ? [`ان کا پیغام:\n${note}`] : []),
        'منظور کرنے پر انھیں اکاؤنٹ اور پاس ورڈ رکھنے کا لنک ای میل ہو جائے گا۔'], { text: 'درخواستیں دیکھیں', url: `${SITE}/admin/requests` });
    }
    return { ok: true };
  });

  app.get<{ Querystring: { status?: string } }>('/api/admin/requests', async (req, reply) => {
    if (!(await requireAdmin(req, reply))) return;
    const status = STATUSES.includes(req.query.status ?? '') ? req.query.status : 'pending';
    const [rows, counts] = await Promise.all([
      pool.query(`SELECT r.id, r.email, r.name, r.note, r.status, r.created_at, r.decided_at, d.full_name AS decided_by_name, d.email AS decided_by_email,
          u.id AS user_id FROM join_requests r LEFT JOIN users d ON d.id = r.decided_by LEFT JOIN users u ON u.email = r.email
        WHERE r.status = $1 ORDER BY ${status === 'pending' ? 'r.created_at' : 'r.decided_at DESC'} LIMIT 200`, [status]),
      pool.query('SELECT status, count(*)::int AS n FROM join_requests GROUP BY status'),
    ]);
    return { status, counts: Object.fromEntries(counts.rows.map((r) => [r.status, r.n])), requests: rows.rows };
  });

  app.post<{ Params: { id: string; action: string } }>('/api/admin/requests/:id/:action', async (req, reply) => {
    const admin = await requireAdmin(req, reply); if (!admin) return;
    const { action } = req.params;
    if (action !== 'approve' && action !== 'decline') return reply.code(404).send({ error: 'نہیں ملا' });
    const r = (await pool.query('SELECT * FROM join_requests WHERE id = $1', [Number(req.params.id) || 0])).rows[0];
    if (!r) return reply.code(404).send({ error: 'درخواست نہیں ملی' });
    if (r.status !== 'pending') return reply.code(409).send({ error: 'اس درخواست پر فیصلہ ہو چکا ہے' });
    const made = action === 'approve' ? await invite(admin, r.email, r.name, { request: r.id }) : null;
    if (action === 'approve' && !made) return reply.code(409).send({ error: 'اس ای میل سے اکاؤنٹ پہلے سے موجود ہے؛ درخواست رد کر دیں' });
    await pool.query(`UPDATE join_requests SET status = $2, decided_by = $3, decided_at = now() WHERE id = $1`,
      [r.id, action === 'approve' ? 'approved' : 'declined', admin.id]);
    if (action === 'decline') await audit(admin, 'decline', { email: r.email }, { request: r.id });
    return made ?? { ok: true };
  });
}
