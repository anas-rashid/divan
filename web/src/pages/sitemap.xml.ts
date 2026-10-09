// sitemap.xml: every public page (poets, books/chapters, works, tags, e-books, and the fixed pages), for search
// engines. A work's date is when its latest Divan version was published. About 13,000 addresses (one file holds 50,000).
import type { APIRoute } from 'astro';
import { api } from '../lib/api';
import { siteUrl, xml } from '../lib/site-url';

export const GET: APIRoute = async ({ url }) => {
  const base = siteUrl(url), s = await api<any>('/api/sitemap');
  const loc = (path: string, updated?: string | null) =>
    `<url><loc>${xml(base + encodeURI(path))}</loc>${updated ? `<lastmod>${new Date(updated).toISOString().slice(0, 10)}</lastmod>` : ''}</url>`;
  const urls = [
    ...['/', '/about', '/tags', '/privacy', '/terms'].map((p) => loc(p)),
    ...s.poets.map((p: string) => loc(p)),
    ...s.poets.map((p: string) => loc(`${p}/ebooks`)),
    ...s.categories.map((c: string) => loc(c)),
    ...s.works.map((w: any) => loc(w.url, w.updated)),
    ...s.tags.map((t: any) => loc(`/tag/${t.type}/${t.name}`)),
    ...s.ebooks.map((b: any) => loc(`/ebook/${b.id}`)),
  ];
  const body = `<?xml version="1.0" encoding="UTF-8"?>\n<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">\n${urls.join('\n')}\n</urlset>\n`;
  return new Response(body, { headers: { 'content-type': 'application/xml; charset=utf-8', 'cache-control': 'public, max-age=3600' } });
};
