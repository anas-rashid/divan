// The signed-in reader (or null) for every page, from the session cookie; and the reader's numerals: pages are
// written with Eastern Arabic (Urdu) digits, and a reader who chose Western digits (cookie divan-digits=latn, set in
// the display settings) gets them in the page as sent, so nothing flashes. Errors are logged as one JSON line each.
import { defineMiddleware } from 'astro:middleware';
import { auth, COOKIE } from './lib/auth';

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
  if (ctx.cookies.get('divan-digits')?.value !== 'latn' || !res.headers.get('content-type')?.includes('text/html')) return res;
  // text only: scripts and styles are left alone (the page's own scripts look for Eastern digits)
  const html = (await res.text()).split(/(<script[\s\S]*?<\/script>|<style[\s\S]*?<\/style>)/)
    .map((part, i) => (i % 2 ? part : part.replace(/[۰-۹]/g, (d) => String(d.charCodeAt(0) - 0x6f0)))).join('');
  const headers = new Headers(res.headers); headers.delete('content-length');
  return new Response(html, { status: res.status, statusText: res.statusText, headers });
});
