// Personal library (#24, #25): a reader's saved poets and works, bookmarked couplets and phrases, saved words.
// JSON in and out with Bearer tokens, so the site and mobile apps (#44) use the same endpoints.
//   GET  /api/library                          everything, each item with its path in the site
//   GET  /api/library/state?poet=&category=&poem=&word=  what is saved on one page, or whether a word is in the word book;
//                                              ?ebook= the bookmarked pages of an e-book
//   GET  /api/library/marks                    which works, books/sections and poets hold the reader's saved items
//   POST /api/library/toggle  {kind, poetId?, categoryId?, poemId?, couplet?, phrase?, word?}   -> {saved, id?}
//   POST /api/library/:id/note {note}
//   POST /api/library/:id/delete
// kind: poet {poetId} | category {categoryId} (a book or chapter) | poem {poemId} | couplet {poemId, couplet} | phrase {poemId, couplet, phrase} (part of a
//       couplet or paragraph) | word {word, poemId?, couplet?} (dictionary word) | page {ebookId, page} (a page of an e-book)
import type { FastifyInstance, FastifyReply, FastifyRequest } from 'fastify';
import { pool } from './db.ts';
import { sessionUser } from './auth.ts';
import { PUNCT } from './dictionary.ts';

const KINDS = ['poet', 'category', 'poem', 'couplet', 'phrase', 'word', 'page'] as const;

// a bookmarked phrase: spaces collapsed, 2 to 300 characters
export const cleanPhrase = (p: unknown) => {
  const s = String(p ?? '').normalize('NFC').replace(/\u0614/g, '').replace(/\s+/g, ' ').trim();
  return s.length >= 2 && s.length <= 300 ? s : null;
};

async function reader(req: FastifyRequest, reply: FastifyReply) {
  const u = await sessionUser(req);
  if (!u) reply.code(401).send({ error: 'لاگ ان کریں' });
  return u;
}

// one saved word: punctuation and spaces dropped (as in the word sidebar); Arabic script only
export const cleanWord = (w: unknown) => {
  const s = String(w ?? '').normalize('NFC').replace(PUNCT, '').replace(/ؔ/g, '');
  return /^[\p{Script=Arabic}\p{M}‌]{1,40}$/u.test(s) ? s : null;
};

// the full path of each work: poet » book » section » … (the poet's root category carries the poet's name)
export async function paths(poemIds: number[]) {
  if (!poemIds.length) return new Map<number, { title: string; url: string }[]>();
  const { rows } = await pool.query(
    `WITH RECURSIVE up AS (
       SELECT p.id AS poem_id, c.id, c.parent_id, c.title, c.url, 0 AS depth
       FROM poems p JOIN categories c ON c.id = p.category_id WHERE p.id = ANY($1)
       UNION ALL
       SELECT up.poem_id, c.id, c.parent_id, c.title, c.url, up.depth + 1 FROM categories c JOIN up ON c.id = up.parent_id)
     SELECT poem_id, title, url FROM up ORDER BY poem_id, depth DESC`, [poemIds]);
  const out = new Map<number, { title: string; url: string }[]>();
  for (const r of rows) (out.get(r.poem_id) ?? out.set(r.poem_id, []).get(r.poem_id)!).push({ title: r.title, url: r.url });
  return out;
}

// the path of each book/chapter: poet » book » … » itself
async function categoryPaths(ids: number[]) {
  if (!ids.length) return new Map<number, { title: string; url: string }[]>();
  const { rows } = await pool.query(
    `WITH RECURSIVE up AS (
       SELECT c.id AS start, c.id, c.parent_id, c.title, c.url, 0 AS depth FROM categories c WHERE c.id = ANY($1)
       UNION ALL SELECT up.start, c.id, c.parent_id, c.title, c.url, up.depth + 1 FROM categories c JOIN up ON c.id = up.parent_id)
     SELECT start, title, url FROM up ORDER BY start, depth DESC`, [ids]);
  const out = new Map<number, { title: string; url: string }[]>();
  for (const r of rows) (out.get(r.start) ?? out.set(r.start, []).get(r.start)!).push({ title: r.title, url: r.url });
  return out;
}

export function libraryRoutes(app: FastifyInstance) {
  app.get('/api/library', async (req, reply) => {
    const u = await reader(req, reply); if (!u) return;
    const { rows } = await pool.query(
      `SELECT l.id, l.kind, l.poet_id, l.category_id, l.poem_id, l.couplet, l.word, l.phrase, l.note, l.created_at, l.ebook_id, l.page,
              pt.nickname AS poet_name, pt.url AS poet_url, pm.title AS poem_title, pm.url AS poem_url, pm.poet_id AS poem_poet,
              eb.title AS ebook_title, ep.nickname AS ebook_poet, ep.url AS ebook_poet_url
       FROM library l LEFT JOIN poets pt ON pt.id = l.poet_id LEFT JOIN poems pm ON pm.id = l.poem_id
         LEFT JOIN ebooks eb ON eb.id = l.ebook_id LEFT JOIN poets ep ON ep.id = eb.poet_id
       WHERE l.user_id = $1 ORDER BY l.created_at DESC, l.id DESC`, [u.id]);
    const poemIds = [...new Set(rows.filter((r) => r.poem_id).map((r) => r.poem_id))];
    const [crumbs, verses, chapters] = await Promise.all([
      paths(poemIds),
      pool.query(`SELECT poem_id, couplet, text FROM verses WHERE (poem_id, couplet) IN
                  (SELECT poem_id, couplet FROM library WHERE user_id = $1 AND poem_id IS NOT NULL AND couplet IS NOT NULL) ORDER BY vorder`, [u.id]),
      categoryPaths(rows.filter((r) => r.kind === 'category').map((r) => r.category_id)),
    ]);
    const lines = (p: number, c: number) => verses.rows.filter((v) => v.poem_id === p && v.couplet === c).map((v) => v.text);
    const where = (r: any) => r.poem_id && { url: r.poem_url, title: r.poem_title, path: crumbs.get(r.poem_id) ?? [], couplet: r.couplet };
    const item = (r: any) => ({ id: Number(r.id), note: r.note, saved_at: r.created_at });
    return {
      user: { full_name: u.full_name ?? '', bio: u.bio ?? '' },
      poets: rows.filter((r) => r.kind === 'poet').map((r) => ({ ...item(r), poet: r.poet_url && { url: r.poet_url, name: r.poet_name } })),
      categories: rows.filter((r) => r.kind === 'category').map((r) => ({ ...item(r), path: chapters.get(r.category_id) ?? [] })),
      poems: rows.filter((r) => r.kind === 'poem').map((r) => ({ ...item(r), poem: r.poem_url && where(r) })),
      couplets: rows.filter((r) => r.kind === 'couplet').map((r) => ({ ...item(r), poem: r.poem_url && where(r), lines: lines(r.poem_id, r.couplet) })),
      phrases: rows.filter((r) => r.kind === 'phrase').map((r) => ({ ...item(r), phrase: r.phrase, poem: r.poem_url && where(r), lines: lines(r.poem_id, r.couplet) })),
      pages: rows.filter((r) => r.kind === 'page' && r.ebook_title).map((r) => ({
        ...item(r), page: r.page, ebook: { id: r.ebook_id, title: r.ebook_title, poet: r.ebook_poet, poet_url: r.ebook_poet_url } })),
      words: rows.filter((r) => r.kind === 'word').map((r) => ({
        ...item(r), word: r.word, source: r.poem_url ? { ...where(r), lines: r.couplet != null ? lines(r.poem_id, r.couplet) : [] } : null,
      })),
    };
  });

  app.get<{ Querystring: { poet?: string; category?: string; poem?: string; word?: string; ebook?: string } }>('/api/library/state', async (req, reply) => {
    const u = await reader(req, reply); if (!u) return;
    if (req.query.ebook != null) return { pages: (await pool.query(
      `SELECT page FROM library WHERE user_id = $1 AND kind = 'page' AND ebook_id = $2 ORDER BY page`, [u.id, Number(req.query.ebook) || 0])).rows.map((r) => r.page) };
    if (req.query.word != null) {
      const w = cleanWord(req.query.word);
      return { word: !!w && !!(await pool.query(`SELECT 1 FROM library WHERE user_id = $1 AND kind = 'word' AND word = $2`, [u.id, w])).rowCount };
    }
    const poet = Number(req.query.poet) || 0, category = Number(req.query.category) || 0, poem = Number(req.query.poem) || 0;
    const { rows } = await pool.query(
      `SELECT kind, couplet, phrase FROM library WHERE user_id = $1 AND ((kind = 'poet' AND poet_id = $2)
         OR (kind = 'category' AND category_id = $3) OR (kind IN ('poem', 'couplet', 'phrase') AND poem_id = $4))`,
      [u.id, poet, category, poem]);
    return {
      poet: rows.some((r) => r.kind === 'poet'), category: rows.some((r) => r.kind === 'category'), poem: rows.some((r) => r.kind === 'poem'),
      couplets: rows.filter((r) => r.kind === 'couplet').map((r) => r.couplet),
      phrases: rows.filter((r) => r.kind === 'phrase').map((r) => ({ couplet: r.couplet, phrase: r.phrase })),
    };
  });

  // for contents lists and cards: works that are bookmarked (fav) or hold bookmarked couplets/phrases (bm), and
  // the books, chapters and poets that are bookmarked or contain them
  app.get('/api/library/marks', async (req, reply) => {
    const u = await reader(req, reply); if (!u) return;
    const { rows } = await pool.query(
      `SELECT poem_id, bool_or(kind = 'poem') AS fav, count(*) FILTER (WHERE kind IN ('couplet', 'phrase'))::int AS bm
       FROM library WHERE user_id = $1 AND kind IN ('poem', 'couplet', 'phrase') GROUP BY poem_id`, [u.id]);
    const ids = rows.map((r) => r.poem_id);
    const [cats, poets] = await Promise.all([
      pool.query(`WITH RECURSIVE up AS (
                    SELECT c.id, c.parent_id FROM poems p JOIN categories c ON c.id = p.category_id WHERE p.id = ANY($1)
                    UNION SELECT c.id, c.parent_id FROM library l JOIN categories c ON c.id = l.category_id WHERE l.user_id = $2 AND l.kind = 'category'
                    UNION SELECT c.id, c.parent_id FROM categories c JOIN up ON c.id = up.parent_id) SELECT id FROM up`, [ids, u.id]),
      pool.query(`SELECT poet_id FROM poems WHERE id = ANY($1) UNION SELECT poet_id FROM library WHERE user_id = $2 AND kind = 'poet'
                  UNION SELECT c.poet_id FROM library l JOIN categories c ON c.id = l.category_id WHERE l.user_id = $2 AND l.kind = 'category'`, [ids, u.id]),
    ]);
    return {
      poems: Object.fromEntries(rows.map((r) => [r.poem_id, { fav: r.fav, bm: r.bm }])),
      categories: cats.rows.map((r) => r.id), poets: poets.rows.map((r) => r.poet_id),
    };
  });

  app.post<{ Body: { kind?: string; poetId?: number; categoryId?: number; poemId?: number; couplet?: number; phrase?: string; word?: string } }>('/api/library/toggle', async (req, reply) => {
    const u = await reader(req, reply); if (!u) return;
    const b = req.body ?? {}, kind = String(b.kind);
    if (!(KINDS as readonly string[]).includes(kind)) return reply.code(400).send({ error: 'نامعلوم قسم' });
    const poetId = Number(b.poetId) || null, categoryId = Number(b.categoryId) || null, poemId = Number(b.poemId) || null;
    const couplet = Number.isInteger(b.couplet) ? b.couplet! : null;
    let key: [string, unknown[]];
    if (kind === 'word') {
      const word = cleanWord(b.word);
      if (!word) return reply.code(400).send({ error: 'ایک لفظ منتخب کریں' });
      key = [`kind = 'word' AND word = $2`, [word]];
      const hit = (await pool.query(`DELETE FROM library WHERE user_id = $1 AND ${key[0]} RETURNING id`, [u.id, ...key[1]])).rows[0];
      if (hit) return { saved: false };
      // the source couplet is kept only if it exists
      const src = poemId && couplet != null && (await pool.query('SELECT 1 FROM verses WHERE poem_id = $1 AND couplet = $2 LIMIT 1', [poemId, couplet])).rowCount ? [poemId, couplet] : [null, null];
      const { rows } = await pool.query(`INSERT INTO library (user_id, kind, word, poem_id, couplet) VALUES ($1, 'word', $2, $3, $4) RETURNING id`, [u.id, word, ...src]);
      return { saved: true, id: Number(rows[0].id) };
    }
    if (kind === 'page') {
      const ebookId = Number((b as any).ebookId) || 0, page = Number((b as any).page);
      if (!Number.isInteger(page) || page < 1 || page > 100000) return reply.code(400).send({ error: 'صفحہ نمبر درست نہیں' });
      if (!(await pool.query('SELECT 1 FROM ebooks WHERE id = $1 AND published', [ebookId])).rowCount) return reply.code(404).send({ error: 'کتاب نہیں ملی' });
      if ((await pool.query(`DELETE FROM library WHERE user_id = $1 AND kind = 'page' AND ebook_id = $2 AND page = $3 RETURNING id`, [u.id, ebookId, page])).rowCount)
        return { saved: false };
      const { rows } = await pool.query(`INSERT INTO library (user_id, kind, ebook_id, page) VALUES ($1, 'page', $2, $3) RETURNING id`, [u.id, ebookId, page]);
      return { saved: true, id: Number(rows[0].id) };
    }
    if (kind === 'phrase') {
      const phrase = cleanPhrase(b.phrase);
      if (!phrase || !poemId || couplet == null) return reply.code(400).send({ error: 'عبارت منتخب کریں' });
      // the phrase must be in that couplet or paragraph (its lines, or across its two misras)
      const text = (await pool.query('SELECT string_agg(text, $3 ORDER BY vorder) AS t FROM verses WHERE poem_id = $1 AND couplet = $2', [poemId, couplet, ' '])).rows[0]?.t;
      if (!text) return reply.code(404).send({ error: 'نہیں ملا' });
      if (!text.normalize('NFC').replace(/\u0614/g, '').replace(/\s+/g, ' ').includes(phrase)) return reply.code(400).send({ error: 'یہ عبارت اس شعر میں نہیں' });
      const args = [u.id, poemId, couplet, phrase];
      if ((await pool.query(`DELETE FROM library WHERE user_id = $1 AND kind = 'phrase' AND poem_id = $2 AND couplet = $3 AND phrase = $4 RETURNING id`, args)).rowCount)
        return { saved: false };
      const { rows } = await pool.query(`INSERT INTO library (user_id, kind, poem_id, couplet, phrase) VALUES ($1, 'phrase', $2, $3, $4) RETURNING id`, args);
      return { saved: true, id: Number(rows[0].id) };
    }
    // poet, book/chapter, poem or couplet: the target must exist
    const exists = kind === 'poet' ? await pool.query('SELECT 1 FROM poets WHERE id = $1', [poetId])
      : kind === 'category' ? await pool.query('SELECT 1 FROM categories WHERE id = $1', [categoryId])
      : kind === 'poem' ? await pool.query('SELECT 1 FROM poems WHERE id = $1', [poemId])
      : await pool.query('SELECT 1 FROM verses WHERE poem_id = $1 AND couplet = $2 LIMIT 1', [poemId, couplet]);
    if (!exists.rowCount) return reply.code(404).send({ error: 'نہیں ملا' });
    const cols = kind === 'poet' ? [poetId, null, null, null] : kind === 'category' ? [null, categoryId, null, null]
      : [null, null, poemId, kind === 'couplet' ? couplet : null];
    const match = `kind = $2 AND coalesce(poet_id, 0) = coalesce($3::int, 0) AND coalesce(category_id, 0) = coalesce($4::int, 0)
      AND coalesce(poem_id, 0) = coalesce($5::int, 0) AND coalesce(couplet, -1) = coalesce($6::int, -1)`;
    const args = [u.id, kind, ...cols];
    if ((await pool.query(`DELETE FROM library WHERE user_id = $1 AND ${match} RETURNING id`, args)).rowCount) return { saved: false };
    const { rows } = await pool.query(
      'INSERT INTO library (user_id, kind, poet_id, category_id, poem_id, couplet) VALUES ($1, $2, $3, $4, $5, $6) ON CONFLICT DO NOTHING RETURNING id', args);
    return { saved: true, id: rows[0] ? Number(rows[0].id) : undefined };
  });

  app.post<{ Params: { id: string }; Body: { note?: string } }>('/api/library/:id/note', async (req, reply) => {
    const u = await reader(req, reply); if (!u) return;
    const note = String(req.body?.note ?? '').normalize('NFC').trim().slice(0, 1000) || null;
    const r = await pool.query('UPDATE library SET note = $1 WHERE id = $2 AND user_id = $3', [note, Number(req.params.id) || 0, u.id]);
    return r.rowCount ? { ok: true } : reply.code(404).send({ error: 'نہیں ملا' });
  });

  app.post<{ Params: { id: string } }>('/api/library/:id/delete', async (req, reply) => {
    const u = await reader(req, reply); if (!u) return;
    const r = await pool.query('DELETE FROM library WHERE id = $1 AND user_id = $2', [Number(req.params.id) || 0, u.id]);
    return r.rowCount ? { ok: true } : reply.code(404).send({ error: 'نہیں ملا' });
  });
}
