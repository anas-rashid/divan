// Tags (#52): typed labels on books/chapters (a poet's page is their root category), works and a work's couplets.
// Tagging is a revision like any other change (entity 'tags-category' or 'tags-work', entity_id = the target), so it
// goes through the pipeline with the 'tags' permission and is published to divan-data as divan/<url>.tags.
// The text is one tag per line, "<type>: <name>", or for one couplet of a work "شعر <n> › <type>: <name>" (n counts
// from 1 as readers do; verses.couplet counts from 0, and couplet 0 here means the whole work):
//   موضوع: عشق
//   شعر 3 › شخصیت: مجنوں
//   GET /api/tags                     every tag with how often it is used, by type
//   GET /api/tag?type=&name=          what carries a tag: books/chapters, works, couplets
import { readdir, readFile, rm, writeFile, mkdir } from 'node:fs/promises';
import { dirname, join } from 'node:path';
import type { FastifyInstance } from 'fastify';
import type { PoolClient } from 'pg';
import { pool } from './db.ts';

export const TAG_TYPES = ['موضوع', 'صنف', 'بحر', 'شخصیت', 'مقام', 'دور', 'ٹیگ'];
export type Tag = { couplet: number; type: string; name: string };
export type TagTarget = 'category' | 'work';
const LINE = /^(?:شعر\s*(\d+)\s*›\s*)?([^:]+):\s*(.+)$/;
const ud2en = (s: string) => s.replace(/[۰-۹]/g, (d) => String('۰۱۲۳۴۵۶۷۸۹'.indexOf(d)));
const key = (t: Tag) => `${t.couplet}|${t.type}|${t.name}`;

// the text as tags; an error message for a bad line
export function parseTags(text: string): Tag[] | string {
  const out: Tag[] = [];
  for (const raw of text.split('\n')) {
    const line = raw.trim().replace(/\s+/g, ' ');
    if (!line) continue;
    const m = ud2en(line).match(LINE);
    if (!m) return `سمجھ نہیں آیا: «${line}»`;
    const [type, name] = [m[2].trim(), m[3].trim()];
    if (!TAG_TYPES.includes(type)) return `ٹیگ کی قسم ${TAG_TYPES.join('، ')} میں سے ہو: «${type}»`;
    if (name.length > 60) return `ٹیگ کا نام بہت لمبا ہے: «${name}»`;
    const t = { couplet: Number(m[1] ?? 0), type, name };
    if (!out.some((x) => key(x) === key(t))) out.push(t);
  }
  return out;
}

// tags in a fixed order (whole first, then by couplet; by type, then name), so equal sets give equal text
export const tagsText = (tags: Tag[]) => [...tags]
  .sort((a, b) => a.couplet - b.couplet || TAG_TYPES.indexOf(a.type) - TAG_TYPES.indexOf(b.type) || a.name.localeCompare(b.name, 'ur'))
  .map((t) => `${t.couplet ? `شعر ${t.couplet} › ` : ''}${t.type}: ${t.name}`).join('\n');

const tagsOf = async (entity: TagTarget, id: number, db: { query: PoolClient['query'] } = pool) => (await db.query(
  `SELECT et.couplet, t.type, t.name FROM entity_tags et JOIN tags t ON t.id = et.tag_id WHERE et.entity = $1 AND et.entity_id = $2`,
  [entity, id])).rows as Tag[];

// a target's published tags as text, and the latest published version of its tags
export async function currentTags(entity: TagTarget, id: number) {
  const target = (await pool.query(entity === 'work'
    ? 'SELECT id, url, title, category_id FROM poems WHERE id = $1' : 'SELECT id, url, title, parent_id FROM categories WHERE id = $1', [id])).rows[0];
  if (!target) return null;
  const last = (await pool.query(
    `SELECT max(version) AS v FROM revisions WHERE entity = $1 AND entity_id = $2 AND status = 'published'`, [`tags-${entity}`, id])).rows[0];
  const couplets = entity === 'work'
    ? Number((await pool.query('SELECT coalesce(max(couplet) + 1, 0) AS n FROM verses WHERE poem_id = $1', [id])).rows[0].n) : 0;
  return { target, couplets, version: Number(last?.v ?? 0), content: tagsText(await tagsOf(entity, id)) };
}

// the text is valid tags, on couplets the work has
export async function checkTags(entity: TagTarget, id: number, text: string) {
  const tags = parseTags(text);
  if (typeof tags === 'string') return tags;
  if (entity === 'category' && tags.some((t) => t.couplet)) return 'کتاب یا باب کے ٹیگ کسی شعر پر نہیں لگتے';
  const cur = await currentTags(entity, id);
  const bad = tags.find((t) => t.couplet > (cur?.couplets ?? 0));
  return bad ? `اس کلام میں شعر ${bad.couplet} نہیں` : null;
}

// the site's tags for the target become the text's
export async function applyTags(client: PoolClient, entity: TagTarget, id: number, text: string) {
  const tags = parseTags(text) as Tag[];
  await client.query('DELETE FROM entity_tags WHERE entity = $1 AND entity_id = $2', [entity, id]);
  for (const t of tags) {
    const tagId = (await client.query(
      `INSERT INTO tags (type, name) VALUES ($1, $2) ON CONFLICT (type, name) DO UPDATE SET name = EXCLUDED.name RETURNING id`, [t.type, t.name])).rows[0].id;
    await client.query('INSERT INTO entity_tags (tag_id, entity, entity_id, couplet) VALUES ($1, $2, $3, $4) ON CONFLICT DO NOTHING',
      [tagId, entity, id, t.couplet]);
  }
  await client.query('DELETE FROM tags t WHERE NOT EXISTS (SELECT 1 FROM entity_tags e WHERE e.tag_id = t.id)');
}

// divan-data/divan/<url>.tags (removed when the last tag goes)
export async function writeTags(dataDir: string, url: string, text: string) {
  const path = join(dataDir, 'divan', url.replace(/^\//, '') + '.tags');
  if (!text.trim()) { await rm(path, { force: true }); return path; }
  await mkdir(dirname(path), { recursive: true });
  await writeFile(path, text.trim() + '\n');
  return path;
}

// the import: every published .tags file in divan-data back into the site's tags (divan-data is the record)
export async function loadTags(dataDir: string) {
  const root = join(dataDir, 'divan');
  const files = await readdir(root, { recursive: true }).catch(() => [] as string[]);
  const client = await pool.connect();
  let n = 0;
  try {
    await client.query('BEGIN');
    await client.query('DELETE FROM entity_tags');
    for (const f of files.filter((f) => f.endsWith('.tags'))) {
      const url = '/' + f.slice(0, -'.tags'.length);
      const work = (await client.query('SELECT id FROM poems WHERE url = $1', [url])).rows[0];
      const cat = work ? null : (await client.query('SELECT id FROM categories WHERE url = $1', [url])).rows[0];
      if (!work && !cat) { console.warn(`tags for an unknown page: ${url}`); continue; }
      const text = await readFile(join(root, f), 'utf8');
      if (typeof parseTags(text) === 'string') { console.warn(`bad tags in ${f}`); continue; }
      await applyTags(client, work ? 'work' : 'category', (work ?? cat).id, text);
      n++;
    }
    await client.query('COMMIT');
  } catch (e) {
    await client.query('ROLLBACK');
    throw e;
  } finally {
    client.release();
  }
  return n;
}

export function tagRoutes(app: FastifyInstance) {
  app.get('/api/tags', async () => (await pool.query(
    `SELECT t.type, t.name, count(*)::int AS n FROM tags t JOIN entity_tags e ON e.tag_id = t.id GROUP BY t.id ORDER BY n DESC, t.name`)).rows);

  app.get<{ Querystring: { type?: string; name?: string } }>('/api/tag', async (req, reply) => {
    const tag = (await pool.query('SELECT id, type, name FROM tags WHERE type = $1 AND name = $2', [req.query.type ?? '', req.query.name ?? ''])).rows[0];
    if (!tag) return reply.code(404).send({ error: 'ٹیگ نہیں ملا' });
    const [cats, works, ebooks] = await Promise.all([
      pool.query(`SELECT c.id, c.url, c.title, t.nickname AS poet, t.url AS poet_url, c.parent_id IS NULL AS is_poet
                  FROM entity_tags e JOIN categories c ON c.id = e.entity_id JOIN poets t ON t.id = c.poet_id
                  WHERE e.tag_id = $1 AND e.entity = 'category' ORDER BY t.birth_year_ah NULLS LAST, c.title`, [tag.id]),
      pool.query(`SELECT p.id, p.url, p.title, t.nickname AS poet, t.url AS poet_url, array_agg(e.couplet ORDER BY e.couplet) AS couplets
                  FROM entity_tags e JOIN poems p ON p.id = e.entity_id JOIN poets t ON t.id = p.poet_id
                  WHERE e.tag_id = $1 AND e.entity = 'work' GROUP BY p.id, t.id ORDER BY t.birth_year_ah NULLS LAST, p.title LIMIT 500`, [tag.id]),
      pool.query(`SELECT b.id, b.title, b.kind, t.nickname AS poet, t.url AS poet_url FROM entity_tags e JOIN ebooks b ON b.id = e.entity_id
                  JOIN poets t ON t.id = b.poet_id WHERE e.tag_id = $1 AND e.entity = 'ebook' AND b.published ORDER BY b.title`, [tag.id]),
    ]);
    // the tagged couplets' lines
    const pairs = works.rows.flatMap((w) => w.couplets.filter((c: number) => c > 0).map((c: number) => [w.id, c]));
    const lines = pairs.length ? (await pool.query(
      `SELECT poem_id, couplet, array_agg(text ORDER BY vorder) AS lines FROM verses
       WHERE (poem_id, couplet + 1) IN (SELECT * FROM unnest($1::int[], $2::int[])) GROUP BY poem_id, couplet`,
      [pairs.map((p) => p[0]), pairs.map((p) => p[1])])).rows : [];
    return {
      tag, categories: cats.rows, ebooks: ebooks.rows,
      works: works.rows.map((w) => ({ ...w, whole: w.couplets.includes(0),
        couplets: lines.filter((l) => l.poem_id === w.id).map((l) => ({ couplet: l.couplet + 1, anchor: l.couplet, lines: l.lines })) })),
    };
  });
}

// tags shown on a page: the target's own, by couplet (0 = the whole page)
export async function pageTags(entity: TagTarget, id: number) {
  return (await tagsOf(entity, id)).sort((a, b) => TAG_TYPES.indexOf(a.type) - TAG_TYPES.indexOf(b.type));
}
