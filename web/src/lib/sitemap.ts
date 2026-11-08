// Every public page for the sitemap (poets, books/chapters, works, tags, e-books, and the fixed pages), as <url> entries.
// A work's date is when its latest Divan version was published. Split into files of CHUNK (a file may hold 50,000;
// the site has over 40,000 addresses), listed by the sitemap index /sitemap.xml.
import { api } from './api';
import { xml } from './site-url';

export const CHUNK = 10000;
const XML = { headers: { 'content-type': 'application/xml; charset=utf-8', 'cache-control': 'public, max-age=3600' } };
export const xmlResponse = (body: string) => new Response(body, XML);

export async function sitemapUrls(base: string) {
  const s = await api<any>('/api/sitemap');
  const loc = (path: string, updated?: string | null) =>
    `<url><loc>${xml(base + encodeURI(path))}</loc>${updated ? `<lastmod>${new Date(updated).toISOString().slice(0, 10)}</lastmod>` : ''}</url>`;
  return [
    ...['/', '/about', '/tags', '/privacy', '/terms'].map((p) => loc(p)),
    ...s.poets.map((p: string) => loc(p)),
    ...s.poets.map((p: string) => loc(`${p}/ebooks`)),
    ...s.categories.map((c: string) => loc(c)),
    ...s.works.map((w: any) => loc(w.url, w.updated)),
    ...s.tags.map((t: any) => loc(`/tag/${t.type}/${t.name}`)),
    ...s.ebooks.map((b: any) => loc(`/ebook/${b.id}`)),
  ] as string[];
}
