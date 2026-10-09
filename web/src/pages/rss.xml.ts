// rss.xml (RSS 2.0): the latest published changes, to follow or share: new versions of works, new e-books,
// arrangements and tags, each with who made it (public names) and, for a work, its opening lines.
import type { APIRoute } from 'astro';
import { api } from '../lib/api';
import { brand } from '../lib/brand';
import { siteUrl, xml } from '../lib/site-url';
import { ud } from '../lib/urdu';

const KIND: Record<string, string> = { work: 'نیا ورژن', ebook: 'نئی ای بک', order: 'نئی ترتیب', tags: 'ٹیگ' };

export const GET: APIRoute = async ({ url }) => {
  const base = siteUrl(url), items = (await api<any[]>('/api/feed?limit=50')) ?? [];
  const item = (i: any) => {
    const title = `${KIND[i.kind]}: ${i.title}${i.poet ? ` (${i.poet})` : ''}${i.kind === 'work' && i.version ? ` · ورژن ${ud(i.version)}` : ''}`;
    const text = [i.lines.join(' / '), i.summary, i.by && `از ${i.by}`].filter(Boolean).join(' — ');
    return `<item><title>${xml(title)}</title><link>${xml(base + encodeURI(i.url))}</link>` +
      `<guid isPermaLink="false">divan-${i.id}</guid><pubDate>${new Date(i.published).toUTCString()}</pubDate>` +
      `${i.by ? `<dc:creator>${xml(i.by)}</dc:creator>` : ''}<category>${xml(KIND[i.kind])}</category><description>${xml(text)}</description></item>`;
  };
  const body = `<?xml version="1.0" encoding="UTF-8"?>
<rss version="2.0" xmlns:atom="http://www.w3.org/2005/Atom" xmlns:dc="http://purl.org/dc/elements/1.1/">
<channel>
<title>${xml(`${brand.name}: نئی تبدیلیاں`)}</title>
<link>${xml(base)}/</link>
<description>${xml(`${brand.name} (${brand.tagline}) میں شائع ہونے والے نئے ورژن، ای بکس اور تبدیلیاں`)}</description>
<language>ur-PK</language>
<atom:link href="${xml(base)}/rss.xml" rel="self" type="application/rss+xml" />
${items.length ? `<lastBuildDate>${new Date(items[0].published).toUTCString()}</lastBuildDate>\n` : ''}${items.map(item).join('\n')}
</channel>
</rss>
`;
  return new Response(body, { headers: { 'content-type': 'application/rss+xml; charset=utf-8', 'cache-control': 'public, max-age=300' } });
};
