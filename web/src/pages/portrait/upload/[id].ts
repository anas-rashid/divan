// A moderator's portrait upload (the picture shrunk in their browser): passed to the API with their session
import type { APIRoute } from 'astro';
import { COOKIE } from '../../../lib/auth';

const API = process.env.API_URL ?? 'http://127.0.0.1:4100';
export const POST: APIRoute = async ({ params, cookies, request, url }) => {
  const token = cookies.get(COOKIE)?.value;
  if (!token) return new Response(JSON.stringify({ error: 'لاگ ان کریں' }), { status: 401 });
  const res = await fetch(`${API}/api/mod/portraits/${Number(params.id) || 0}/upload${url.search}`, {
    method: 'POST', headers: { authorization: `Bearer ${token}`, 'content-type': 'application/octet-stream' }, body: await request.arrayBuffer(),
  });
  return new Response(res.body, { status: res.status, headers: { 'content-type': 'application/json' } });
};
