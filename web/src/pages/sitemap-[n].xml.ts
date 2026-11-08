// sitemap-<n>.xml: the n-th CHUNK of the site's addresses (lib/sitemap.ts), listed by the index /sitemap.xml
import type { APIRoute } from 'astro';
import { CHUNK, sitemapUrls, xmlResponse } from '../lib/sitemap';
import { siteUrl } from '../lib/site-url';

export const GET: APIRoute = async ({ url, params }) => {
  const n = Number(params.n), urls = await sitemapUrls(siteUrl(url));
  if (!Number.isInteger(n) || n < 1 || (n - 1) * CHUNK >= urls.length) return new Response('not found', { status: 404 });
  return xmlResponse(`<?xml version="1.0" encoding="UTF-8"?>\n<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">\n${urls.slice((n - 1) * CHUNK, n * CHUNK).join('\n')}\n</urlset>\n`);
};
