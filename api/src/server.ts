// Divan API (Fastify + PostgreSQL)
//   GET /api/poets              all poets
//   GET /api/page?url=/p238/... poet, category or poem at that URL
//   GET /api/search?q=&poet=1,2&page=  content results (paged, optionally only these poets/writers), the poets/writers
//                                    the results come from (with counts), poets by name and books/chapters by title
//   GET /api/word?w=            Wiktionary meanings and pronunciation (sidebar)
//   /api/auth/*                  accounts (see auth.ts)
//   /api/admin/*                 admin panel (see admin.ts)
//   /api/library/*               a reader's saved poets, works, couplets and words (see library.ts)
//   /api/mod/*                   content moderation: drafts, review, publishing, history (see moderation.ts)
//   GET /health
import Fastify from 'fastify';
import { pool } from './db.ts';
import { likePatterns, normalise, terms } from './urdu.ts';
import { nameMatches } from './search.ts';
import { lookup, PUNCT } from './dictionary.ts';
import { authRoutes } from './auth.ts';
import { adminRoutes } from './admin.ts';
import { statsRoutes } from './stats.ts';
import { disk } from './health.ts';
import { permissionRoutes } from './permissions.ts';
import { libraryRoutes } from './library.ts';
import { moderationRoutes } from './moderation.ts';
import { siteRoutes } from './site.ts';
import { tagRoutes, pageTags } from './tags.ts';
import { ebookRoutes } from './ebooks.ts';
import { feedRoutes } from './feeds.ts';

const app = Fastify({ logger: { level: process.env.LOG_LEVEL ?? 'info' } });

// Errors: readers get a short message, never internal details; the log gets the whole error (one JSON line each,
// with the method and address), which is what the server's error report reads (divan-deploy scripts/errors.sh).
app.setErrorHandler((err: any, req, reply) => {
  const status = err.statusCode >= 400 && err.statusCode < 500 ? err.statusCode : 500;
  if (status === 500) req.log.error({ err, method: req.method, url: req.url }, 'request failed');
  reply.code(status).send({ error: status === 500 ? 'سرور میں خرابی۔ تھوڑی دیر بعد کوشش کریں۔' : err.message });
});

// for the site's /health (uptime monitoring): the API answers and the database too
app.get('/api/health', async (req, reply) => {
  // the database answers, and the disk is not (nearly) full; the percentage goes to the log, not to the public
  const db = await pool.query('SELECT 1').then(() => 'ok', (err) => (req.log.error({ err }, 'health: database unreachable'), 'down'));
  const space = await disk().catch(() => null);
  if (space?.full) req.log.error({ used: space.used }, 'health: disk nearly full');
  const ok = db === 'ok' && !space?.full;
  return reply.code(ok ? 200 : 503).send({ ok, db, disk: space?.full ? 'full' : 'ok' });
});
const PAGE_SIZE = 20;

app.get('/health', async () => {
  await pool.query('SELECT 1');
  return { ok: true };
});

app.get('/api/poets', async () => {
  const { rows } = await pool.query(
    'SELECT id, url, name, nickname, birth_year_ah, death_year_ah, birth_year_ce, death_year_ce, pin_order FROM poets ORDER BY birth_year_ah NULLS LAST, nickname',
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
    const [verses, poet, crumbs, siblings, edited] = await Promise.all([
      pool.query('SELECT vorder, position, couplet, text FROM verses WHERE poem_id = $1 ORDER BY vorder', [poem.id]),
      pool.query('SELECT id, url, nickname FROM poets WHERE id = $1', [poem.poet_id]),
      ancestors(poem.category_id),
      pool.query(
        `SELECT (SELECT url FROM poems WHERE category_id = $1 AND position < $2 ORDER BY position DESC LIMIT 1) AS prev,
                (SELECT url FROM poems WHERE category_id = $1 AND position > $2 ORDER BY position LIMIT 1) AS next`,
        [poem.category_id, poem.position],
      ),
      // the latest Divan version (public names only) and the work's history in the public divan-data on GitHub (by file,
    // not by commit: the public copy is published at releases with its own commit ids)
      pool.query(`SELECT version, published_at, credits, commit FROM revisions WHERE entity = 'work' AND entity_id = $1 AND status = 'published'
                  ORDER BY version DESC LIMIT 1`, [poem.id]),
    ]);
    const e = edited.rows[0];
    const divan = e && { version: e.version, at: e.published_at, ...e.credits,
      history_url: (process.env.DIVAN_DATA_HISTORY_URL ?? 'https://github.com/anas-rashid/divan-data/commits/main/divan{path}.dtx').replace('{path}', poem.url) };
    const { search_text, ...rest } = poem;
    return { type: 'poem', poem: rest, poet: poet.rows[0], breadcrumbs: crumbs, verses: verses.rows, ...siblings.rows[0], divan,
      tags: await pageTags('work', poem.id) };
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
         SELECT c.id, c.url, c.title, count(p.id)::int AS poems
         FROM categories c JOIN tree ON tree.root = c.id LEFT JOIN poems p ON p.category_id = tree.id
         GROUP BY c.id ORDER BY c.position, c.id`,
        [cat.id],
      ),
      // a title that is only a number («شمارهٔ ۸۴», many Persian works from Ganjoor): the first line goes with it, so the
      // contents say what each poem is
      pool.query(`SELECT id, url, title, radif_letter, CASE WHEN title ~ '^[^0-9۰-۹]{0,20}[0-9۰-۹]+$'
                    THEN (SELECT left(text, 100) FROM verses v WHERE v.poem_id = p.id ORDER BY vorder LIMIT 1) END AS first_line
                  FROM poems p WHERE category_id = $1 ORDER BY position, id`, [cat.id]),
    ]);
    return {
      type: cat.parent_id === null ? 'poet' : 'category',
      category: cat, poet: poet.rows[0], breadcrumbs: crumbs, children: children.rows, poems: poems.rows, tags: await pageTags('category', cat.id),
    };
  }
  return reply.code(404).send({ error: 'not found' });
});

app.get<{ Querystring: { q?: string; poet?: string; tag?: string; page?: string } }>('/api/search', async (req) => {
  // tag:عشق or ٹیگ:عشق (quotes for names with spaces) keeps works carrying the tag: on the work, one of its couplets,
  // or its book/chapter; the rest of the query is text as before
  const TAG = /(?:tag|ٹیگ):(?:"([^"]+)"|(\S+))/g;
  const tagNames = [...new Set([...(req.query.q ?? '').matchAll(TAG)].map((m) => (m[1] ?? m[2]).trim()).filter(Boolean))].slice(0, 5);
  const q = (req.query.q ?? '').replace(TAG, ' ').trim();
  const patterns = likePatterns(q);
  if (!patterns.length && !tagNames.length) return { total: 0, page: 1, pageSize: PAGE_SIZE, results: [], poets: [], books: [], tags: [] };
  const page = Math.max(1, Number(req.query.page) || 1);
  const params: unknown[] = [...patterns];
  const where = patterns.map((_, i) => `p.search_text ILIKE $${i + 1}`);
  for (const name of tagNames) {
    params.push(name);
    where.push(`EXISTS (SELECT 1 FROM entity_tags e JOIN tags g ON g.id = e.tag_id WHERE g.name = $${params.length}
      AND ((e.entity = 'work' AND e.entity_id = p.id) OR (e.entity = 'category' AND e.entity_id = p.category_id)))`);
  }
  // ?tag=3,7 (#52): works carrying every chosen tag (on the work, a couplet, or its book/chapter), like tag: but chosen
  // from the tags of the results
  const tagIds = [...new Set(String(req.query.tag ?? '').split(',').map(Number).filter((n) => Number.isInteger(n) && n > 0))].slice(0, 10);
  for (const id of tagIds) {
    params.push(id);
    where.push(`EXISTS (SELECT 1 FROM entity_tags e WHERE e.tag_id = $${params.length}
      AND ((e.entity = 'work' AND e.entity_id = p.id) OR (e.entity = 'category' AND e.entity_id = p.category_id)))`);
  }
  const filteredWhere = where.join(' AND '), filteredParams = [...params];
  const poetIds = [...new Set(String(req.query.poet ?? '').split(',').map(Number).filter((n) => Number.isInteger(n) && n > 0))].slice(0, 50);
  if (poetIds.length) {
    params.push(poetIds);
    where.push(`p.poet_id = ANY($${params.length})`);
  }
  const sql = `FROM poems p JOIN poets t ON t.id = p.poet_id WHERE ${where.join(' AND ')}`;
  // several words: poems with them together as a phrase come first
  const phrase = likePatterns(`"${q}"`)[0] ?? '%';
  const [count, rows] = await Promise.all([
    pool.query(`SELECT count(*)::int AS n ${sql}`, params),
    pool.query(
      `SELECT p.id, p.url, p.title, t.nickname AS poet, t.url AS poet_url
       ${sql} ORDER BY p.search_text ILIKE $${params.length + 1} DESC, t.birth_year_ah NULLS LAST, p.id
       LIMIT ${PAGE_SIZE} OFFSET ${(page - 1) * PAGE_SIZE}`,
      [...params, phrase],
    ),
  ]);
  // snippet: the verse holding most of the terms, best the whole phrase (with its couplet partner), else the first line
  const ts = terms(q), whole = ts.join(' '), top = ts.length + (ts.length > 1 ? 1 : 0);
  const verses = await pool.query(
    'SELECT poem_id, position, couplet, text FROM verses WHERE poem_id = ANY($1) ORDER BY poem_id, vorder',
    [rows.rows.map((r) => r.id)],
  );
  const byPoem = Map.groupBy(verses.rows, (v) => v.poem_id);
  // with tags and no words, a work's snippet is its tagged couplet
  const tagged = tagNames.length && !ts.length ? (await pool.query(
    `SELECT e.entity_id AS poem_id, min(e.couplet) AS couplet FROM entity_tags e JOIN tags g ON g.id = e.tag_id
     WHERE e.entity = 'work' AND e.couplet > 0 AND g.name = ANY($1) AND e.entity_id = ANY($2) GROUP BY e.entity_id`,
    [tagNames, rows.rows.map((r) => r.id)])).rows : [];
  const results = rows.rows.map(({ id, ...r }) => {
    const vs = byPoem.get(id) ?? [];
    const tc = tagged.find((t) => t.poem_id === id)?.couplet;
    let best = (tc && vs.find((v) => v.couplet === tc - 1)) || vs[0], score = 0; // tags count couplets from 1
    for (const v of vs) {
      const n = normalise(v.text), k = ts.filter((t) => n.includes(t)).length + (ts.length > 1 && n.includes(whole) ? 1 : 0);
      if (k > score) [best, score] = [v, k];
      if (score === top) break;
    }
    const lines = !best ? [] : best.position === 'Paragraph' || best.position === 'Single' ? [best.text]
      : vs.filter((v) => v.couplet === best.couplet && (v.position === 'Right' || v.position === 'Left')).map((v) => v.text);
    return { ...r, snippet: lines, prose: best?.position === 'Paragraph' };
  });
  // names and titles on the first page: poets/writers whose name has every word, books/chapters likewise
  const names = page === 1 && !poetIds.length && ts.length ? await nameMatches(ts) : { poets: [], books: [] };
  // tags whose name has the words (first page), and the tags searched for
  const tagRows = page === 1 && (ts.length || tagNames.length) ? (await pool.query(
    `SELECT g.type, g.name, count(*)::int AS n FROM tags g JOIN entity_tags e ON e.tag_id = g.id GROUP BY g.id ORDER BY n DESC`)).rows
    .filter((t) => tagNames.includes(t.name) || (ts.length && ts.every((w) => normalise(t.name).includes(w)))).slice(0, 20) : [];
  // which poets/writers the matching content comes from (with the chosen tags, whatever the poet filter); and which tags it
  // carries (within the poet filter), most used first, with the chosen ones
  const tagFilterWhere = poetIds.length ? `${filteredWhere} AND p.poet_id = ANY($${filteredParams.length + 1})` : filteredWhere;
  const [authors, selected, tagFacets, chosenTags] = await Promise.all([
    pool.query(`SELECT t.id, t.url, t.nickname, count(*)::int AS n FROM poems p JOIN poets t ON t.id = p.poet_id
                WHERE ${filteredWhere} GROUP BY t.id ORDER BY n DESC, t.nickname LIMIT 40`, filteredParams),
    poetIds.length ? pool.query('SELECT id, url, nickname FROM poets WHERE id = ANY($1) ORDER BY nickname', [poetIds]) : { rows: [] },
    pool.query(`SELECT g.id, g.type, g.name, count(DISTINCT p.id)::int AS n FROM poems p
                JOIN entity_tags e ON (e.entity = 'work' AND e.entity_id = p.id) OR (e.entity = 'category' AND e.entity_id = p.category_id)
                JOIN tags g ON g.id = e.tag_id WHERE ${tagFilterWhere} GROUP BY g.id ORDER BY n DESC, g.name LIMIT 30`,
      poetIds.length ? [...filteredParams, poetIds] : filteredParams),
    tagIds.length ? pool.query('SELECT id, type, name FROM tags WHERE id = ANY($1) ORDER BY name', [tagIds]) : { rows: [] },
  ]);
  // e-books whose title (or text, for text books) has the words, on the first page (trigram index)
  const ebooks = page === 1 && patterns.length && !tagNames.length ? (await pool.query(
    `SELECT b.id, b.title, b.kind, t.nickname AS poet, t.url AS poet_url FROM ebooks b JOIN poets t ON t.id = b.poet_id
     WHERE b.published AND ${patterns.map((_, i) => `b.search_text ILIKE $${i + 1}`).join(' AND ')}
     ${poetIds.length ? `AND b.poet_id = ANY($${patterns.length + 1})` : ''} ORDER BY b.title LIMIT 20`,
    poetIds.length ? [...patterns, poetIds] : patterns)).rows : [];
  return { total: count.rows[0].n, page, pageSize: PAGE_SIZE, results, ...names, tags: tagRows, searchedTags: tagNames, ebooks,
    authors: authors.rows, selected: selected.rows, tagFacets: tagFacets.rows, chosenTags: chosenTags.rows };
});

// one word in Arabic script (Urdu, Persian, Arabic), as selected by a reader
app.get<{ Querystring: { w?: string } }>('/api/word', async (req, reply) => {
  const w = (req.query.w ?? '').replace(PUNCT, ''); // commas, dots, dashes, spaces
  if (!/^[\p{Script=Arabic}\p{M}\u200C]{1,40}$/u.test(w)) return reply.code(400).send({ error: 'one Urdu, Persian or Arabic word' });
  return lookup(w);
});

authRoutes(app);
adminRoutes(app);
permissionRoutes(app);
libraryRoutes(app);
moderationRoutes(app);
siteRoutes(app);
tagRoutes(app);
ebookRoutes(app);
feedRoutes(app);
statsRoutes(app);

const port = Number(process.env.PORT ?? 4100);
await app.listen({ port, host: process.env.HOST ?? '127.0.0.1' });
