// An e-book's file, passed through from the private API (with the reader's session, so moderators can open
// files still under review)
import type { APIRoute } from 'astro';
import { COOKIE } from '../../../lib/auth';

const API = process.env.API_URL ?? 'http://127.0.0.1:4100';

export const GET: APIRoute = async ({ params, cookies, request }) => {
  const token = cookies.get(COOKIE)?.value;
  const range = request.headers.get('range'); // the PDF reader asks for byte ranges
  const res = await fetch(`${API}/api/ebook/${Number(params.id) || 0}/file`, {
    headers: { ...(token && { authorization: `Bearer ${token}` }), ...(range && { range }) },
  });
  const headers = new Headers();
  for (const k of ['content-type', 'content-length', 'content-disposition', 'cache-control', 'accept-ranges', 'content-range']) { const v = res.headers.get(k); if (v) headers.set(k, v); }
  return new Response(res.ok ? res.body : null, { status: res.status, headers });
};
