// Home page sections (owner request): moderators with the 'site' permission (admins always) rename, add, remove and
// reorder the sections, choose their poets (e.g. مقبول شعرا) and how each is sorted: by hand, alphabetically or by
// timeline. A century section lists every poet born in that Hijri century, so new poets appear on their own.
// Changes apply at once and go to the audit log.
//   GET /api/home        the sections with their poets (public)
//   GET /api/mod/site    the sections as stored, and every poet (for the editor)
//   POST /api/mod/site   {sections: [{title, century, sort, poets}]}: the whole layout, in order
import type { FastifyInstance, FastifyReply, FastifyRequest } from 'fastify';
import { pool } from './db.ts';
import { sessionUser } from './auth.ts';
import { can } from './permissions.ts';
import { audit } from './admin.ts';

type Section = { id?: number; title: string | null; century: number | null; sort: string; poets: number[] };
const SORTS = ['manual', 'alpha', 'timeline'];
const centuryOf = (y: number | null) => (y ? Math.floor(y / 100) + 1 : 0); // 0: year unknown
const urdu = new Intl.Collator('ur');

async function replace(list: Section[]) {
  const client = await pool.connect();
  try {
    await client.query('BEGIN');
    await client.query('DELETE FROM home_sections');
    for (const [i, s] of list.entries())
      await client.query('INSERT INTO home_sections (position, title, century, sort, poets) VALUES ($1, $2, $3, $4, $5)',
        [i, s.title, s.century, s.sort, s.poets]);
    await client.query('COMMIT');
  } catch (e) {
    await client.query('ROLLBACK');
    throw e;
  } finally {
    client.release();
  }
}

// the stored sections; the first time, the layout the home page always had (featured poets, then a section per
// century of birth, each by timeline)
async function sections(): Promise<Section[]> {
  const q = () => pool.query('SELECT id, title, century, sort, poets FROM home_sections ORDER BY position, id');
  let { rows } = await q();
  if (rows.length) return rows;
  const pinned = (await pool.query('SELECT id FROM poets WHERE pin_order IS NOT NULL ORDER BY pin_order')).rows.map((r) => r.id);
  const centuries = (await pool.query('SELECT DISTINCT coalesce(birth_year_ah / 100 + 1, 0) AS c FROM poets')).rows
    .map((r) => r.c as number).sort((a, b) => (a || 99) - (b || 99));
  // ponytail: two first requests at once could both seed; replace() runs in a transaction, the last one wins
  await replace([{ title: 'مقبول شعرا', century: null, sort: 'manual', poets: pinned },
    ...centuries.map((c) => ({ title: null, century: c, sort: 'timeline', poets: [] }))]);
  return (await q()).rows;
}

// each section with its poets in order (sections with nobody in them are left out)
export async function home() {
  const poets = (await pool.query(
    'SELECT id, url, nickname, birth_year_ah, death_year_ah, birth_year_ce, death_year_ce FROM poets')).rows;
  const byId = new Map(poets.map((p) => [p.id, p]));
  const timeline = (a: any, b: any) => (a.birth_year_ah ?? 1e9) - (b.birth_year_ah ?? 1e9) || urdu.compare(a.nickname, b.nickname);
  return (await sections()).map((s) => {
    const chosen = s.poets.map((id) => byId.get(id)).filter(Boolean);
    let list = s.century === null ? chosen : [
      ...chosen.filter((p) => centuryOf(p.birth_year_ah) === s.century),
      ...poets.filter((p) => centuryOf(p.birth_year_ah) === s.century && !s.poets.includes(p.id)).sort(timeline)];
    if (s.sort === 'alpha') list = [...list].sort((a, b) => urdu.compare(a.nickname, b.nickname));
    if (s.sort === 'timeline') list = [...list].sort(timeline);
    return { id: s.id, title: s.title, century: s.century, sort: s.sort, poets: list };
  }).filter((s) => s.poets.length);
}

async function siteEditor(req: FastifyRequest, reply: FastifyReply) {
  const u = await sessionUser(req);
  if (!u) return void reply.code(401).send({ error: 'لاگ ان کریں' });
  if (!(await can(u, 'edit', 'site'))) return void reply.code(403).send({ error: 'سائٹ کے حصے بدلنے کی اجازت نہیں' });
  return u;
}

export function siteRoutes(app: FastifyInstance) {
  app.get('/api/home', async () => home());

  app.get('/api/mod/site', async (req, reply) => {
    if (!(await siteEditor(req, reply))) return;
    const poets = (await pool.query('SELECT id, nickname AS name, birth_year_ah FROM poets ORDER BY nickname')).rows;
    return { sections: await sections(), poets };
  });

  app.post<{ Body: { sections?: unknown } }>('/api/mod/site', async (req, reply) => {
    const u = await siteEditor(req, reply); if (!u) return;
    const raw = Array.isArray(req.body?.sections) ? req.body.sections : null;
    if (!raw || !raw.length || raw.length > 100) return reply.code(400).send({ error: 'حصوں کی فہرست درست نہیں' });
    const known = new Set((await pool.query('SELECT id FROM poets')).rows.map((r) => r.id));
    const list: Section[] = [];
    for (const s of raw as any[]) {
      const century = s?.century === null || s?.century === undefined || s?.century === '' ? null : Number(s.century);
      if (century !== null && !(Number.isInteger(century) && century >= 0 && century <= 20)) return reply.code(400).send({ error: 'صدی درست نہیں' });
      const title = String(s?.title ?? '').trim().slice(0, 100) || null;
      if (!title && century === null) return reply.code(400).send({ error: 'ہر حصے کا نام ضروری ہے' });
      const poets = [...new Set((Array.isArray(s?.poets) ? s.poets : []).map(Number).filter((id: number) => known.has(id)))].slice(0, 500) as number[];
      list.push({ title, century, sort: SORTS.includes(s?.sort) ? s.sort : 'manual', poets });
    }
    await replace(list);
    await audit(u, 'site-sections', null, { sections: list.map((s) => s.title ?? `صدی ${s.century}`) });
    return { sections: await home() };
  });
}
