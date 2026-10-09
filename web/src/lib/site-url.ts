// The site's public address for links in feeds and the sitemap: SITE_URL if set (e.g. https://divan.pk), else the
// address the request came to (behind Caddy that is the public one).
export const siteUrl = (url: URL) => (process.env.SITE_URL ?? url.origin).replace(/\/$/, '');
export const xml = (s: unknown) => String(s ?? '').replace(/[<>&'"]/g, (c) => ({ '<': '&lt;', '>': '&gt;', '&': '&amp;', "'": '&apos;', '"': '&quot;' })[c]!);
