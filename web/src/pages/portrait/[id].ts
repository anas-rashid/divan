// A poet's portrait from the file store (through the API); the page links it with ?v=<hash>, so it can be cached for good
import type { APIRoute } from 'astro';

const API = process.env.API_URL ?? 'http://127.0.0.1:4100';
export const GET: APIRoute = async ({ params }) => {
  const res = await fetch(`${API}/api/poet/${Number(params.id) || 0}/portrait`);
  const headers = new Headers();
  for (const k of ['content-type', 'cache-control']) { const v = res.headers.get(k); if (v) headers.set(k, v); }
  return new Response(res.body, { status: res.status, headers });
};
