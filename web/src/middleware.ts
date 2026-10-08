// The signed-in reader (or null) for every page, from the session cookie
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
  return next();
});
