// /health for an uptime monitor (divan-deploy README): 200 when the site, the API and the database all answer,
// 503 otherwise, with which part is down. Never cached.
import type { APIRoute } from 'astro';

const API = process.env.API_URL ?? 'http://127.0.0.1:4100';

export const GET: APIRoute = async () => {
  const started = Date.now();
  let api = 'down', db = 'down';
  try {
    const r = await fetch(`${API}/api/health`, { signal: AbortSignal.timeout(3000) });
    api = 'ok';
    db = (await r.json().catch(() => ({}))).db ?? 'down';
  } catch {}
  const ok = api === 'ok' && db === 'ok';
  if (!ok) console.error(JSON.stringify({ level: 'error', time: new Date().toISOString(), msg: 'health check failed', api, db }));
  return new Response(JSON.stringify({ ok, site: 'ok', api, db, ms: Date.now() - started }), {
    status: ok ? 200 : 503, headers: { 'content-type': 'application/json', 'cache-control': 'no-store' },
  });
};
