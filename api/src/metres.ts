// Metres (بحر) and works in the same zameen (ہم زمین), as Ganjoor builds them: a work's metre comes from divan-data
// (Ganjoor's for Persian works; for Urdu works our scanner, later), and its rhyme letters (قافیہ و ردیف, spaces dropped)
// from the export (Ganjoor's FindRhyme, ported). A moderator corrects a metre with a published «بحر» tag on the work
// (tags.ts, through the review pipeline); the tag wins over the data everywhere.
//   GET /api/metres              every metre with its number of works, most used first
//   GET /api/metre?name=&page=   one metre's works, by the poets' Hijri century (oldest first)
// zameen(poem) gives a work page its metre (with where it came from) and the works sharing its zameen.
import type { FastifyInstance } from 'fastify';
import { pool } from './db.ts';

const PAGE = 100;
// a work's metre: a moderator's «بحر» tag on the whole work, else divan-data's
const METRE = `coalesce((SELECT g.name FROM entity_tags e JOIN tags g ON g.id = e.tag_id
  WHERE g.type = 'بحر' AND e.entity = 'work' AND e.entity_id = p.id AND e.couplet = 0 AND e.phrase = '' ORDER BY e.tag_id LIMIT 1), p.metre)`;

export async function zameen(poem: { id: number; metre: string | null; rhyme: string | null; language: string | null }) {
  const m = (await pool.query(`SELECT ${METRE} AS metre, ${METRE} IS DISTINCT FROM p.metre AS corrected FROM poems p WHERE p.id = $1`, [poem.id])).rows[0];
  const metre = m?.metre ?? null;
  const source = !metre ? null : m.corrected ? 'mod' : poem.language?.startsWith('fa') ? 'ganjoor' : 'auto';
  // the same rhyme letters and the same metre; while a work has no metre (Urdu, until the scanner), the same rhyme
  // letters alone, long enough to be a radif or a full qafia (a single «ا» would match thousands)
  if (!poem.rhyme || (!metre && [...poem.rhyme].length < 3)) return { metre, source, rhymeOnly: !metre, works: [] };
  const works = (await pool.query(
    `SELECT p.url, p.title, t.nickname AS poet, t.url AS poet_url, coalesce(t.birth_year_ah / 100 + 1, 0) AS century
     FROM poems p JOIN poets t ON t.id = p.poet_id
     WHERE p.rhyme = $1 AND p.id <> $2 AND ($3::text IS NULL OR ${METRE} = $3)
     ORDER BY t.birth_year_ah NULLS LAST, t.nickname, p.position LIMIT 50`, [poem.rhyme, poem.id, metre])).rows;
  return { metre, source, rhymeOnly: !metre, works };
}

export function metreRoutes(app: FastifyInstance) {
  app.get('/api/metres', async (_req, reply) => {
    reply.header('cache-control', 'public, max-age=3600');
    return (await pool.query(`SELECT metre AS name, count(*)::int AS n FROM (SELECT ${METRE} AS metre FROM poems p) x
      WHERE metre IS NOT NULL GROUP BY metre ORDER BY n DESC, metre`)).rows;
  });

  app.get<{ Querystring: { name?: string; page?: string } }>('/api/metre', async (req, reply) => {
    const name = String(req.query.name ?? '').trim(), page = Math.max(1, Number(req.query.page) || 1);
    const where = `FROM poems p JOIN poets t ON t.id = p.poet_id WHERE ${METRE} = $1`;
    const total = (await pool.query(`SELECT count(*)::int AS n ${where}`, [name])).rows[0].n;
    if (!total) return reply.code(404).send({ error: 'یہ بحر نہیں ملی' });
    const works = (await pool.query(
      `SELECT p.url, p.title, t.nickname AS poet, t.url AS poet_url, coalesce(t.birth_year_ah / 100 + 1, 0) AS century
       ${where} ORDER BY t.birth_year_ah NULLS LAST, t.nickname, p.category_id, p.position LIMIT ${PAGE} OFFSET ${(page - 1) * PAGE}`, [name])).rows;
    return { name, total, page, pageSize: PAGE, works };
  });
}
