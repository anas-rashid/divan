// Personal library (#24, #25): a reader's saved poets and works, bookmarked couplets and saved words.
// JSON in and out with Bearer tokens, so the site and mobile apps (#44) use the same endpoints.
//   GET  /api/library                          everything, each item with its path in the site
//   GET  /api/library/state?poet=&poem=&word=  what is saved on one page (poet, poem, couplets) or a word
//   POST /api/library/toggle  {kind, poetId?, poemId?, couplet?, word?}   -> {saved, id?}
//   POST /api/library/:id/note {note}
//   POST /api/library/:id/delete
// kind: poet {poetId} | poem {poemId} | couplet {poemId, couplet} | word {word, poemId?, couplet?}
import type { FastifyInstance, FastifyReply, FastifyRequest } from 'fastify';
import { pool } from './db.ts';
import { sessionUser } from './auth.ts';
import { PUNCT } from './dictionary.ts';

const KINDS = ['poet', 'poem', 'couplet', 'word'] as const;

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

export function libraryRoutes(app: FastifyInstance) {
  app.get('/api/library', async (req, reply) => {
    const u = await reader(req, reply); if (!u) return;
    const { rows } = await pool.query(
      `SELECT l.id, l.kind, l.poet_id, l.poem_id, l.couplet, l.word, l.note, l.created_at,
              pt.nickname AS poet_name, pt.url AS poet_url, pm.title AS poem_title, pm.url AS poem_url, pm.poet_id AS poem_poet
       FROM library l LEFT JOIN poets pt ON pt.id = l.poet_id LEFT JOIN poems pm ON pm.id = l.poem_id
       WHERE l.user_id = $1 ORDER BY l.created_at DESC, l.id DESC`, [u.id]);
    const poemIds = [...new Set(rows.filter((r) => r.poem_id).map((r) => r.poem_id))];
    const [crumbs, verses] = await Promise.all([
      paths(poemIds),
      pool.query(`SELECT poem_id, couplet, text FROM verses WHERE (poem_id, couplet) IN
                  (SELECT poem_id, couplet FROM library WHERE user_id = $1 AND poem_id IS NOT NULL AND couplet IS NOT NULL) ORDER BY vorder`, [u.id]),
    ]);
    const lines = (p: number, c: number) => verses.rows.filter((v) => v.poem_id === p && v.couplet === c).map((v) => v.text);
    const where = (r: any) => r.poem_id && { url: r.poem_url, title: r.poem_title, path: crumbs.get(r.poem_id) ?? [], couplet: r.couplet };
    const item = (r: any) => ({ id: Number(r.id), note: r.note, saved_at: r.created_at });
    return {
      user: { full_name: u.full_name ?? '', bio: u.bio ?? '' },
      poets: rows.filter((r) => r.kind === 'poet').map((r) => ({ ...item(r), poet: r.poet_url && { url: r.poet_url, name: r.poet_name } })),
      poems: rows.filter((r) => r.kind === 'poem').map((r) => ({ ...item(r), poem: r.poem_url && where(r) })),
      couplets: rows.filter((r) => r.kind === 'couplet').map((r) => ({ ...item(r), poem: r.poem_url && where(r), lines: lines(r.poem_id, r.couplet) })),
      words: rows.filter((r) => r.kind === 'word').map((r) => ({
        ...item(r), word: r.word, source: r.poem_url ? { ...where(r), lines: r.couplet != null ? lines(r.poem_id, r.couplet) : [] } : null,
      })),
    };
  });

  app.get<{ Querystring: { poet?: string; poem?: string; word?: string } }>('/api/library/state', async (req, reply) => {
    const u = await reader(req, reply); if (!u) return;
    if (req.query.word != null) {
      const w = cleanWord(req.query.word);
      return { word: !!w && !!(await pool.query(`SELECT 1 FROM library WHERE user_id = $1 AND kind = 'word' AND word = $2`, [u.id, w])).rowCount };
    }
    const poet = Number(req.query.poet) || 0, poem = Number(req.query.poem) || 0;
    const { rows } = await pool.query(
      `SELECT kind, couplet FROM library WHERE user_id = $1 AND ((kind = 'poet' AND poet_id = $2) OR (kind IN ('poem', 'couplet') AND poem_id = $3))`,
      [u.id, poet, poem]);
    return {
      poet: rows.some((r) => r.kind === 'poet'), poem: rows.some((r) => r.kind === 'poem'),
      couplets: rows.filter((r) => r.kind === 'couplet').map((r) => r.couplet),
    };
  });

  app.post<{ Body: { kind?: string; poetId?: number; poemId?: number; couplet?: number; word?: string } }>('/api/library/toggle', async (req, reply) => {
    const u = await reader(req, reply); if (!u) return;
    const b = req.body ?? {}, kind = String(b.kind);
    if (!(KINDS as readonly string[]).includes(kind)) return reply.code(400).send({ error: 'نامعلوم قسم' });
    const poetId = Number(b.poetId) || null, poemId = Number(b.poemId) || null;
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
    // poet, poem or couplet: the target must exist
    const exists = kind === 'poet' ? await pool.query('SELECT 1 FROM poets WHERE id = $1', [poetId])
      : kind === 'poem' ? await pool.query('SELECT 1 FROM poems WHERE id = $1', [poemId])
      : await pool.query('SELECT 1 FROM verses WHERE poem_id = $1 AND couplet = $2 LIMIT 1', [poemId, couplet]);
    if (!exists.rowCount) return reply.code(404).send({ error: 'نہیں ملا' });
    const cols = kind === 'poet' ? { poet_id: poetId, poem_id: null, couplet: null } : { poet_id: null, poem_id: poemId, couplet: kind === 'couplet' ? couplet : null };
    const match = `kind = $2 AND coalesce(poet_id, 0) = coalesce($3::int, 0) AND coalesce(poem_id, 0) = coalesce($4::int, 0) AND coalesce(couplet, -1) = coalesce($5::int, -1)`;
    const args = [u.id, kind, cols.poet_id, cols.poem_id, cols.couplet];
    if ((await pool.query(`DELETE FROM library WHERE user_id = $1 AND ${match} RETURNING id`, args)).rowCount) return { saved: false };
    const { rows } = await pool.query(
      'INSERT INTO library (user_id, kind, poet_id, poem_id, couplet) VALUES ($1, $2, $3, $4, $5) ON CONFLICT DO NOTHING RETURNING id', args);
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
