// robots.txt: search engines may read the reading pages, not accounts, moderation or search results (endless and
// private). DIVAN_NOINDEX=1 (the private preview) keeps everything out.
import type { APIRoute } from 'astro';
import { siteUrl } from '../lib/site-url';

export const GET: APIRoute = ({ url }) => {
  const closed = process.env.DIVAN_NOINDEX === '1';
  const body = closed ? 'User-agent: *\nDisallow: /\n' : [
    'User-agent: *',
    ...['/admin', '/mod', '/account', '/library', '/writers', '/words', '/notes', '/signin', '/signup', '/search', '/api/', '/ebooks/file/', '/health']
      .map((p) => `Disallow: ${p}`),
    '', `Sitemap: ${siteUrl(url)}/sitemap.xml`, '',
  ].join('\n');
  return new Response(body, { headers: { 'content-type': 'text/plain; charset=utf-8', 'cache-control': 'public, max-age=3600' } });
};
