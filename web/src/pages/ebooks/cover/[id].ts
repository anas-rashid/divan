// An e-book's cover: shown from the private API, and saved by moderators (the image made in their browser)
import type { APIRoute } from 'astro';
import { COOKIE } from '../../../lib/auth';

const API = process.env.API_URL ?? 'http://127.0.0.1:4100';
const pass = (res: Response) => {
  const headers = new Headers();
  for (const k of ['content-type', 'cache-control']) { const v = res.headers.get(k); if (v) headers.set(k, v); }
  return new Response(res.body, { status: res.status, headers });
};

export const GET: APIRoute = async ({ params, cookies }) => {
  const token = cookies.get(COOKIE)?.value;
  return pass(await fetch(`${API}/api/ebook/${Number(params.id) || 0}/cover`, { headers: token ? { authorization: `Bearer ${token}` } : {} }));
};

export const POST: APIRoute = async ({ params, cookies, request }) => {
  const token = cookies.get(COOKIE)?.value;
  if (!token) return new Response(JSON.stringify({ error: 'لاگ ان کریں' }), { status: 401 });
  return pass(await fetch(`${API}/api/mod/ebooks/${Number(params.id) || 0}/cover`, {
    method: 'POST', headers: { authorization: `Bearer ${token}`, 'content-type': 'application/octet-stream' }, body: await request.arrayBuffer(),
  }));
};
