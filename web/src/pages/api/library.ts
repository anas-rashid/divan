// Browser -> API for the 🔖 bookmark buttons and the word book: the reader's session cookie becomes the Bearer token.
// JSON posts from other sites are refused: the Origin must be this site (and SameSite cookies are not sent).
import type { APIRoute } from 'astro';
import { asUser, COOKIE } from '../../lib/auth';

const json = (data: unknown, status = 200) => new Response(JSON.stringify(data), { status, headers: { 'content-type': 'application/json' } });

export const POST: APIRoute = async ({ request, cookies, url }) => {
  const origin = request.headers.get('origin');
  if (origin && new URL(origin).host !== url.host) return json({ error: 'forbidden' }, 403);
  const token = cookies.get(COOKIE)?.value;
  if (!token) return json({ error: 'لاگ ان کریں' }, 401);
  const r = await asUser(token, '/api/library/toggle', await request.json().catch(() => ({})));
  return json(r.data, r.status);
};

export const GET: APIRoute = async ({ cookies, url }) => {
  const token = cookies.get(COOKIE)?.value;
  if (!token) return json({ error: 'لاگ ان کریں' }, 401);
  const r = await asUser(token, `/api/library/state?word=${encodeURIComponent(url.searchParams.get('word') ?? '')}`);
  return json(r.data, r.status);
};
