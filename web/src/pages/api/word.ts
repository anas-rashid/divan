// Browser -> API proxy for the word sidebar (the API itself is not public)
import type { APIRoute } from 'astro';

const API = process.env.API_URL ?? 'http://127.0.0.1:4100';

export const GET: APIRoute = async ({ url }) => {
  const res = await fetch(`${API}/api/word?w=${encodeURIComponent(url.searchParams.get('w') ?? '')}`);
  return new Response(await res.text(), {
    status: res.status,
    headers: { 'content-type': 'application/json', 'cache-control': res.ok ? 'public, max-age=86400' : 'no-store' },
  });
};
