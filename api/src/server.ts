// Divan API (Fastify + PostgreSQL)
//   GET /api/poets              all poets
//   GET /api/page?url=/p238/... poet, category or poem at that URL
//   GET /api/search?q=&poet=&page=
//   GET /health
import Fastify from 'fastify';
import { pool } from './db.ts';
import { likePatterns } from './urdu.ts';

const app = Fastify({ logger: { level: process.env.LOG_LEVEL ?? 'info' } });
const PAGE_SIZE = 20;

app.get('/health', async () => {
  await pool.query('SELECT 1');
  return { ok: true };
});

app.get('/api/poets', async () => {
  const { rows } = await pool.query(
    'SELECT id, url, name, nickname, birth_year_ah, death_year_ah FROM poets ORDER BY birth_year_ah NULLS LAST, nickname',
  );
  return rows;
});

// breadcrumb from a category up to the poet root
async function ancestors(categoryId: number) {
  const { rows } = await pool.query(
    `WITH RECURSIVE up AS (
       SELECT id, parent_id, url, title, 0 AS depth FROM categories WHERE id = $1
       UNION ALL
       SELECT c.id, c.parent_id, c.url, c.title, up.depth + 1 FROM categories c JOIN up ON c.id = up.parent_id)
     SELECT url, title FROM up ORDER BY depth DESC`,
    [categoryId],
  );
  return rows;
}

app.get<{ Querystring: { url?: string } }>('/api/page', async (req, reply) => {
  const url = '/' + (req.query.url ?? '').replace(/^\/+|\/+$/g, '');

  const poem = (await pool.query('SELECT * FROM poems WHERE url = $1', [url])).rows[0];
  if (poem) {
    const [verses, poet, crumbs, siblings] = await Promise.all([
      pool.query('SELECT vorder, position, couplet, text FROM verses WHERE poem_id = $1 ORDER BY vorder', [poem.id]),
      pool.query('SELECT id, url, nickname FROM poets WHERE id = $1', [poem.poet_id]),
      ancestors(poem.category_id),
      pool.query(
        `SELECT (SELECT url FROM poems WHERE category_id = $1 AND position < $2 ORDER BY position DESC LIMIT 1) AS prev,
                (SELECT url FROM poems WHERE category_id = $1 AND position > $2 ORDER BY position LIMIT 1) AS next`,
        [poem.category_id, poem.position],
      ),
    ]);
    const { search_text, ...rest } = poem;
    return { type: 'poem', poem: rest, poet: poet.rows[0], breadcrumbs: crumbs, verses: verses.rows, ...siblings.rows[0] };
  }

  const cat = (await pool.query('SELECT * FROM categories WHERE url = $1', [url])).rows[0];
  if (cat) {
    const [poet, crumbs, children, poems] = await Promise.all([
      pool.query('SELECT * FROM poets WHERE id = $1', [cat.poet_id]),
      ancestors(cat.id),
      // child categories with the number of poems in them and all their sub-categories
      pool.query(
        `WITH RECURSIVE tree AS (
           SELECT id AS root, id FROM categories WHERE parent_id = $1
           UNION ALL
           SELECT tree.root, c.id FROM categories c JOIN tree ON c.parent_id = tree.id)
         SELECT c.url, c.title, count(p.id)::int AS poems
         FROM categories c JOIN tree ON tree.root = c.id LEFT JOIN poems p ON p.category_id = tree.id
         GROUP BY c.id ORDER BY c.position, c.id`,
        [cat.id],
      ),
      pool.query('SELECT url, title FROM poems WHERE category_id = $1 ORDER BY position, id', [cat.id]),
    ]);
    return {
      type: cat.parent_id === null ? 'poet' : 'category',
      category: cat, poet: poet.rows[0], breadcrumbs: crumbs, children: children.rows, poems: poems.rows,
    };
  }
  return reply.code(404).send({ error: 'not found' });
});

app.get<{ Querystring: { q?: string; poet?: string; page?: string } }>('/api/search', async (req) => {
  const patterns = likePatterns(req.query.q ?? '');
  if (!patterns.length) return { total: 0, page: 1, results: [] };
  const page = Math.max(1, Number(req.query.page) || 1);
  const params: unknown[] = [...patterns];
  const where = patterns.map((_, i) => `p.search_text ILIKE $${i + 1}`);
  if (req.query.poet) {
    params.push(Number(req.query.poet));
    where.push(`p.poet_id = $${params.length}`);
  }
  const sql = `FROM poems p JOIN poets t ON t.id = p.poet_id WHERE ${where.join(' AND ')}`;
  const [count, rows] = await Promise.all([
    pool.query(`SELECT count(*)::int AS n ${sql}`, params),
    pool.query(
      `SELECT p.url, p.title, t.nickname AS poet, t.url AS poet_url,
              (SELECT v.text FROM verses v WHERE v.poem_id = p.id ORDER BY v.vorder LIMIT 1) AS first_line
       ${sql} ORDER BY t.birth_year_ah NULLS LAST, p.id LIMIT ${PAGE_SIZE} OFFSET ${(page - 1) * PAGE_SIZE}`,
      params,
    ),
  ]);
  return { total: count.rows[0].n, page, pageSize: PAGE_SIZE, results: rows.rows };
});

const port = Number(process.env.PORT ?? 4100);
await app.listen({ port, host: process.env.HOST ?? '127.0.0.1' });
