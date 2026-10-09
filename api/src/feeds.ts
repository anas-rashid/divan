// What the site's sitemap.xml and rss.xml are made from (#83, #87).
//   GET /api/sitemap      every public page: poets, books/chapters, works (with the date of their latest Divan
//                         version), tags, e-books
//   GET /api/feed?limit=  the latest published changes, newest first: new versions of works (version, summary, the
//                         opening lines), e-books, arrangements and tags; who made each (public names)
import type { FastifyInstance } from 'fastify';
import { pool } from './db.ts';

export function feedRoutes(app: FastifyInstance) {
  app.get('/api/sitemap', async (_req, reply) => {
    const [poets, cats, works, tags, ebooks] = await Promise.all([
      pool.query('SELECT url FROM poets ORDER BY id'),
      pool.query('SELECT url FROM categories WHERE parent_id IS NOT NULL ORDER BY id'),
      pool.query(`SELECT p.url, max(r.published_at) AS updated FROM poems p
                  LEFT JOIN revisions r ON r.entity = 'work' AND r.entity_id = p.id AND r.status = 'published'
                  GROUP BY p.id ORDER BY p.id`),
      pool.query('SELECT DISTINCT t.type, t.name FROM tags t JOIN entity_tags e ON e.tag_id = t.id ORDER BY t.type, t.name'),
      pool.query('SELECT b.id, t.url AS poet_url FROM ebooks b JOIN poets t ON t.id = b.poet_id WHERE b.published ORDER BY b.id'),
    ]);
    reply.header('cache-control', 'public, max-age=3600');
    return {
      poets: poets.rows.map((r) => r.url),
      categories: cats.rows.map((r) => r.url),
      works: works.rows.map((r) => ({ url: r.url, updated: r.updated })),
      tags: tags.rows,
      ebooks: ebooks.rows,
    };
  });

  app.get<{ Querystring: { limit?: string } }>('/api/feed', async (req, reply) => {
    const limit = Math.min(Math.max(Number(req.query.limit) || 50, 1), 100);
    const { rows } = await pool.query(
      `SELECT r.id, r.entity, r.entity_id, r.version, r.summary, r.published_at, r.credits,
              p.title AS work_title, p.url AS work_url, pp.nickname AS work_poet,
              c.title AS cat_title, c.url AS cat_url, cp.nickname AS cat_poet,
              b.title AS ebook_title, bp.nickname AS ebook_poet
       FROM revisions r
       LEFT JOIN poems p ON r.entity IN ('work', 'tags-work') AND p.id = r.entity_id LEFT JOIN poets pp ON pp.id = p.poet_id
       LEFT JOIN categories c ON r.entity IN ('order', 'tags-category') AND c.id = r.entity_id LEFT JOIN poets cp ON cp.id = c.poet_id
       LEFT JOIN ebooks b ON r.entity = 'ebook' AND b.id = r.entity_id LEFT JOIN poets bp ON bp.id = b.poet_id
       WHERE r.status = 'published' AND r.published_at IS NOT NULL
       ORDER BY r.published_at DESC, r.id DESC LIMIT $1`, [limit]);
    // the opening lines of each new version of a work, for the feed's description
    const workIds = rows.filter((r) => r.entity === 'work').map((r) => r.entity_id);
    const lines = workIds.length ? (await pool.query(
      `SELECT poem_id, array_agg(text ORDER BY vorder) AS lines FROM verses WHERE poem_id = ANY($1) AND vorder <= 2 GROUP BY poem_id`,
      [workIds])).rows : [];
    reply.header('cache-control', 'public, max-age=300');
    return rows.map((r) => {
      const kind = r.entity === 'work' ? 'work' : r.entity === 'ebook' ? 'ebook' : r.entity === 'order' ? 'order' : 'tags';
      const title = r.work_title ?? r.cat_title ?? r.ebook_title ?? '';
      const url = r.work_url ?? r.cat_url ?? (r.entity === 'ebook' ? `/ebook/${r.entity_id}` : '/');
      return {
        id: Number(r.id), kind, version: r.version, title, url, poet: r.work_poet ?? r.cat_poet ?? r.ebook_poet ?? null,
        summary: r.summary ?? null, published: r.published_at, by: r.credits?.by ?? null,
        lines: lines.find((l) => l.poem_id === r.entity_id)?.lines ?? [],
      };
    });
  });
}
