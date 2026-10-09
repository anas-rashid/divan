// Accounts: email address and password only (no email is sent; owner decision 2026-10-08).
// Passwords: scrypt with a per-user salt. Sessions: a random token held by the site in an HTTP-only
// cookie; the database stores only its SHA-256, so a database leak does not give working sessions.
// Rate limits go by the reader's address. The site passes it in x-client-ip, and only the site may: it proves itself
// with DIVAN_SITE_KEY in x-site-key (set the same key for the API and the site in production); with no key set
// (local development) only requests from this machine may. Anyone else is limited by their own address, so a
// made-up x-client-ip cannot get round the limits.
//   POST   /api/auth/signup    {email, password}            -> {token, user}
//   POST   /api/auth/signin    {email, password}            -> {token, user}
//   GET    /api/auth/me        Bearer token                 -> {user}
//   POST   /api/auth/signout   Bearer token
//   POST   /api/auth/password  Bearer token {current, next} -> other sessions signed out
//   POST   /api/auth/delete    Bearer token {password}      -> account and its data deleted
//   POST   /api/auth/profile   Bearer token {full_name, bio} (Urdu or any text; trimmed, length-limited)
import { createHash, randomBytes, scrypt, timingSafeEqual } from 'node:crypto';
import type { FastifyInstance, FastifyRequest } from 'fastify';
import { pool } from './db.ts';

const SESSION_DAYS = 30;
export { sha };
const KDF = { N: 32768, r: 8, p: 1, maxmem: 64 * 1024 * 1024 };

const derive = (password: string, salt: Buffer) =>
  new Promise<Buffer>((ok, fail) => scrypt(password.normalize('NFC'), salt, 32, KDF, (e, k) => (e ? fail(e) : ok(k))));

// "scrypt$N$r$p$salt$hash" (base64url), so the cost can be raised later without breaking old hashes
export async function hashPassword(password: string) {
  const salt = randomBytes(16);
  return `scrypt$${KDF.N}$${KDF.r}$${KDF.p}$${salt.toString('base64url')}$${(await derive(password, salt)).toString('base64url')}`;
}

export async function verifyPassword(password: string, stored: string) {
  const [alg, N, r, p, salt, hash] = stored.split('$');
  if (alg !== 'scrypt') return false;
  const key = await new Promise<Buffer>((ok, fail) =>
    scrypt(password.normalize('NFC'), Buffer.from(salt, 'base64url'), 32, { N: +N, r: +r, p: +p, maxmem: KDF.maxmem }, (e, k) => (e ? fail(e) : ok(k))));
  return timingSafeEqual(key, Buffer.from(hash, 'base64url'));
}
// compared against when the email is unknown, so a wrong email takes as long as a wrong password
const DUMMY = await hashPassword(randomBytes(16).toString('hex'));

export const normaliseEmail = (e: unknown) => (typeof e === 'string' ? e.trim().toLowerCase() : '');
export const validEmail = (e: string) => e.length <= 254 && /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(e);
export const passwordProblem = (p: unknown) =>
  typeof p !== 'string' ? 'پاس ورڈ درکار ہے' : p.length < 8 ? 'پاس ورڈ کم از کم ۸ حروف کا ہو' : p.length > 200 ? 'پاس ورڈ بہت لمبا ہے' : null;

// fixed-window limits per key (IP or email); ponytail: in-process memory, fine for one API process;
// move to PostgreSQL or Redis if the API ever runs as several processes
export function limiter(max: number, windowMs: number) {
  const hits = new Map<string, { n: number; until: number }>();
  return (key: string, now = Date.now()) => {
    const h = hits.get(key);
    if (!h || h.until <= now) {
      hits.set(key, { n: 1, until: now + windowMs });
      if (hits.size > 10_000) for (const [k, v] of hits) if (v.until <= now) hits.delete(k);
      return true;
    }
    return ++h.n <= max;
  };
}
const signinByIp = limiter(20, 15 * 60_000), signinByEmail = limiter(8, 15 * 60_000), signupByIp = limiter(5, 60 * 60_000);

function sha(t: string) {
  return createHash('sha256').update(t).digest('hex');
}
// a person's public name (shown on published content and in divan-data's git history): never their email
export const publicName = (u: { id: unknown; full_name?: string | null }) => u.full_name?.trim() || `موڈریٹر ${u.id}`;

export const publicUser = (u: any) => ({
  id: Number(u.id), email: u.email, role: u.role as string, created_at: u.created_at, full_name: u.full_name ?? '', bio: u.bio ?? '',
});

async function newSession(userId: number) {
  const token = randomBytes(32).toString('base64url');
  await pool.query(`INSERT INTO sessions (id, user_id, expires_at) VALUES ($1, $2, now() + interval '${SESSION_DAYS} days')`, [sha(token), userId]);
  return token;
}

// the signed-in user for a Bearer token; sliding expiry (renewed when less than half is left)
export async function sessionUser(req: FastifyRequest) {
  const token = req.headers.authorization?.match(/^Bearer (\S+)$/)?.[1];
  if (!token) return null;
  const { rows } = await pool.query(
    `SELECT u.*, s.id AS sid, s.expires_at < now() + interval '${SESSION_DAYS / 2} days' AS renew
     FROM sessions s JOIN users u ON u.id = s.user_id WHERE s.id = $1 AND s.expires_at > now() AND u.disabled_at IS NULL`, [sha(token)]);
  const u = rows[0];
  if (u?.renew) await pool.query(`UPDATE sessions SET expires_at = now() + interval '${SESSION_DAYS} days' WHERE id = $1`, [u.sid]);
  return u ?? null;
}

const LOOPBACK = ['127.0.0.1', '::1', '::ffff:127.0.0.1'];
const fromSite = (req: FastifyRequest) => {
  const key = process.env.DIVAN_SITE_KEY;
  if (!key) return LOOPBACK.includes(req.ip);
  const given = Buffer.from(String(req.headers['x-site-key'] ?? '')), want = Buffer.from(key);
  return given.length === want.length && timingSafeEqual(given, want);
};
export const ip = (req: FastifyRequest) => (fromSite(req) && (req.headers['x-client-ip'] as string)) || req.ip;

export function authRoutes(app: FastifyInstance) {
  app.post<{ Body: { email?: string; password?: string } }>('/api/auth/signup', async (req, reply) => {
    if (!signupByIp(ip(req))) return reply.code(429).send({ error: 'بہت زیادہ کوششیں۔ کچھ دیر بعد دوبارہ کوشش کریں۔' });
    const email = normaliseEmail(req.body?.email), problem = passwordProblem(req.body?.password);
    if (!validEmail(email)) return reply.code(400).send({ error: 'درست ای میل پتہ لکھیں' });
    if (problem) return reply.code(400).send({ error: problem });
    const { rows } = await pool.query(
      'INSERT INTO users (email, password_hash) VALUES ($1, $2) ON CONFLICT DO NOTHING RETURNING *', [email, await hashPassword(req.body!.password!)]);
    if (!rows[0]) return reply.code(409).send({ error: 'اس ای میل سے اکاؤنٹ پہلے سے موجود ہے' });
    return { token: await newSession(rows[0].id), user: publicUser(rows[0]) };
  });

  app.post<{ Body: { email?: string; password?: string } }>('/api/auth/signin', async (req, reply) => {
    const email = normaliseEmail(req.body?.email), password = String(req.body?.password ?? '');
    if (!signinByIp(ip(req)) || !signinByEmail(email)) return reply.code(429).send({ error: 'بہت زیادہ کوششیں۔ کچھ دیر بعد دوبارہ کوشش کریں۔' });
    const u = (await pool.query('SELECT * FROM users WHERE email = $1', [email])).rows[0];
    const ok = await verifyPassword(password, u?.password_hash ?? DUMMY);
    if (!u || !ok) return reply.code(401).send({ error: 'ای میل یا پاس ورڈ درست نہیں' });
    if (u.disabled_at) return reply.code(403).send({ error: 'یہ اکاؤنٹ معطل ہے۔ ایڈمن سے رابطہ کریں۔' });
    return { token: await newSession(u.id), user: publicUser(u) };
  });

  app.get('/api/auth/me', async (req, reply) => {
    const u = await sessionUser(req);
    return u ? { user: publicUser(u) } : reply.code(401).send({ error: 'signed out' });
  });

  app.post('/api/auth/signout', async (req) => {
    const u = await sessionUser(req);
    if (u) await pool.query('DELETE FROM sessions WHERE id = $1', [u.sid]);
    return { ok: true };
  });

  app.post<{ Body: { current?: string; next?: string } }>('/api/auth/password', async (req, reply) => {
    const u = await sessionUser(req);
    if (!u) return reply.code(401).send({ error: 'دوبارہ لاگ ان کریں' });
    if (!(await verifyPassword(String(req.body?.current ?? ''), u.password_hash))) return reply.code(403).send({ error: 'موجودہ پاس ورڈ درست نہیں' });
    const problem = passwordProblem(req.body?.next);
    if (problem) return reply.code(400).send({ error: problem });
    await pool.query('UPDATE users SET password_hash = $1 WHERE id = $2', [await hashPassword(req.body!.next!), u.id]);
    await pool.query('DELETE FROM sessions WHERE user_id = $1 AND id <> $2', [u.id, u.sid]); // sign out other devices
    return { ok: true };
  });

  app.post<{ Body: { full_name?: string; bio?: string } }>('/api/auth/profile', async (req, reply) => {
    const u = await sessionUser(req);
    if (!u) return reply.code(401).send({ error: 'دوبارہ لاگ ان کریں' });
    const text = (v: unknown, max: number) => String(v ?? '').normalize('NFC').replace(/[\u0000-\u0008\u000B-\u001F\u007F]/g, '').trim().slice(0, max);
    const full_name = text(req.body?.full_name, 100).replace(/\s+/g, ' '), bio = text(req.body?.bio, 1000);
    const { rows } = await pool.query('UPDATE users SET full_name = $1, bio = $2 WHERE id = $3 RETURNING *', [full_name || null, bio || null, u.id]);
    return { user: publicUser(rows[0]) };
  });

  app.post<{ Body: { password?: string } }>('/api/auth/delete', async (req, reply) => {
    const u = await sessionUser(req);
    if (!u) return reply.code(401).send({ error: 'دوبارہ لاگ ان کریں' });
    if (!(await verifyPassword(String(req.body?.password ?? ''), u.password_hash))) return reply.code(403).send({ error: 'پاس ورڈ درست نہیں' });
    await pool.query('DELETE FROM users WHERE id = $1', [u.id]); // sessions (and later the reader's library) cascade
    return { ok: true };
  });
}
