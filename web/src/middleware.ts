// The signed-in reader (or null) for every page, from the session cookie; and the reader's numerals: pages are
// written with Eastern Arabic (Urdu) digits, and a reader who chose Western digits (cookie divan-digits=latn, set in
// the display settings) gets them in the page as sent, so nothing flashes. Errors are logged as one JSON line each.
import { defineMiddleware } from 'astro:middleware';
import { auth, COOKIE } from './lib/auth';

// visitor statistics (#86): each reading page shown is reported to the API without waiting; the API keeps only counts
// and a one-day hash. Bots, link previews, private pages (accounts, moderation) and errors are not counted.
const API = process.env.API_URL ?? 'http://127.0.0.1:4100';
const PRIVATE = /^\/(api|mod|admin|account|library|notes|words|writers|signin|signup|_astro|ebooks\/file|health)(\/|$)/;
const BOT = /bot|crawl|spider|slurp|preview|fetch|curl|wget|python|java\/|go-http|headless|lighthouse|monitor/i;
function count(ctx: any, res: Response) {
  const ua = ctx.request.headers.get('user-agent') ?? '', path = ctx.url.pathname;
  if (ctx.request.method !== 'GET' || res.status !== 200 || !res.headers.get('content-type')?.includes('text/html') || !ua || BOT.test(ua) || PRIVATE.test(path)) return;
  let ip = '', ref = '';
  try { ip = ctx.clientAddress; } catch {}
  try { const h = new URL(ctx.request.headers.get('referer') ?? '').hostname; if (h !== ctx.url.hostname) ref = h.replace(/^www\./, ''); } catch {}
  fetch(`${API}/api/stats/hit`, { method: 'POST', headers: { 'content-type': 'application/json', ...(process.env.DIVAN_SITE_KEY && { 'x-site-key': process.env.DIVAN_SITE_KEY }) },
    body: JSON.stringify({ path, ref, ip, ua }) }).catch(() => {});
}

export const onRequest = defineMiddleware(async (ctx, next) => {
  const token = ctx.cookies.get(COOKIE)?.value;
  ctx.locals.user = null;
  if (token) {
    const r = await auth('me', { token });
    if (r.ok) ctx.locals.user = r.data.user;
    else if (r.status === 401) ctx.cookies.delete(COOKIE, { path: '/' }); // expired or signed out elsewhere
  }
  let res: Response;
  try {
    res = await next();
  } catch (err: any) {
    // one JSON line per error (the server's error report reads these); the reader sees the Urdu error page (500.astro)
    console.error(JSON.stringify({ level: 'error', time: new Date().toISOString(), method: ctx.request.method, path: ctx.url.pathname,
      msg: String(err?.message ?? err), stack: String(err?.stack ?? '').split('\n').slice(0, 8).join('\n') }));
    throw err;
  }
  count(ctx, res);
  if (ctx.cookies.get('divan-digits')?.value !== 'latn' || !res.headers.get('content-type')?.includes('text/html')) return res;
  // text only: scripts and styles are left alone (the page's own scripts look for Eastern digits)
  const html = (await res.text()).split(/(<script[\s\S]*?<\/script>|<style[\s\S]*?<\/style>)/)
    .map((part, i) => (i % 2 ? part : part.replace(/[۰-۹]/g, (d) => String(d.charCodeAt(0) - 0x6f0)))).join('');
  const headers = new Headers(res.headers); headers.delete('content-length');
  return new Response(html, { status: res.status, statusText: res.statusText, headers });
});
