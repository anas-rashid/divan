// sitemap.xml: the sitemap index, one /sitemap-<n>.xml per CHUNK addresses (lib/sitemap.ts), for search engines
import type { APIRoute } from 'astro';
import { CHUNK, sitemapUrls, xmlResponse } from '../lib/sitemap';
import { siteUrl } from '../lib/site-url';

export const GET: APIRoute = async ({ url }) => {
  const base = siteUrl(url), n = Math.ceil((await sitemapUrls(base)).length / CHUNK);
  const maps = Array.from({ length: n }, (_, i) => `<sitemap><loc>${base}/sitemap-${i + 1}.xml</loc></sitemap>`);
  return xmlResponse(`<?xml version="1.0" encoding="UTF-8"?>\n<sitemapindex xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">\n${maps.join('\n')}\n</sitemapindex>\n`);
};
