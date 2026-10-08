// Site side of accounts: the session token lives in an HTTP-only cookie and is sent to the private API.
import type { AstroCookies } from 'astro';

const API = process.env.API_URL ?? 'http://127.0.0.1:4100';
export const COOKIE = 'divan_session';
export type User = { id: number; email: string; role: string; created_at: string };

// call an /api/auth endpoint as the reader (their token, their IP for rate limits)
export async function auth(path: string, opts: { token?: string; body?: object; ip?: string } = {}) {
  const res = await fetch(`${API}/api/auth/${path}`, {
    method: opts.body || path !== 'me' ? 'POST' : 'GET',
    headers: {
      ...(opts.body && { 'content-type': 'application/json' }),
      ...(opts.token && { authorization: `Bearer ${opts.token}` }),
      ...(opts.ip && { 'x-client-ip': opts.ip }),
    },
    body: opts.body ? JSON.stringify(opts.body) : undefined,
  });
  return { ok: res.ok, status: res.status, data: await res.json().catch(() => ({})) };
}

export const setSession = (cookies: AstroCookies, token: string, secure: boolean) =>
  cookies.set(COOKIE, token, { path: '/', httpOnly: true, sameSite: 'lax', secure, maxAge: 30 * 86400 });

// call any API endpoint as the signed-in reader (admin pages)
export async function asUser(token: string, path: string, body?: object) {
  const res = await fetch(`${API}${path}`, {
    method: body ? 'POST' : 'GET',
    headers: { authorization: `Bearer ${token}`, ...(body && { 'content-type': 'application/json' }) },
    body: body ? JSON.stringify(body) : undefined,
  });
  return { ok: res.ok, status: res.status, data: await res.json().catch(() => ({})) };
}
