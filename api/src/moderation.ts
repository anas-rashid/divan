// Content moderation (#31, #51): versions of works and the L2 -> L1 -> admin pipeline.
// A revision holds a work's Divan text, a summary and a status; every step taken on it is an event (who, what,
// when, comment), which is both the review thread and the moderation log.
//   L2 moderator: drafts and submits             -> submitted
//   L1 moderator (grant covering the work): approves, returns (with a comment) or rejects; an L1's own draft
//                goes straight to the admin step  -> approved
//   admin: publishes (an admin's own draft publishes directly), returns or rejects
// Admins are super moderators: they can edit, review and publish any work without grants.
// Publishing numbers the version, writes it to divan-data as Divan-owned content (owned.ts), commits it there (git.ts)
// and updates the site.
// If another version was published after the draft started, publishing is refused until the draft is redone.
//   GET  /api/mod/can?poem=                  what the reader may do on a work
//   GET  /api/mod/queue                      my drafts, drafts to review, drafts to publish
//   GET  /api/mod/work/:id                   a work's current text and its history
//   POST /api/mod/work/:id/draft             start (or reopen) my draft
//   GET  /api/mod/revisions/:id              a revision, its diff against the version it started from, its events
//   POST /api/mod/revisions/:id/save         {content, summary}
//   POST /api/mod/revisions/:id/:action      submit | approve | return | reject | publish  {comment}
//   GET  /api/mod/log?page=                  who did what, newest first
import type { FastifyInstance, FastifyReply, FastifyRequest } from 'fastify';
import { pool } from './db.ts';
import { sessionUser } from './auth.ts';
import { can } from './permissions.ts';
import { fromPoem, writeOwned } from './owned.ts';
import { parse, toVerses, toText } from './divantext.ts';
import { normalise } from './urdu.ts';
import { diffLines, changed } from './diff.ts';
import { commit, identity } from './git.ts';
import { publicName } from './auth.ts';

const dataDir = () => process.env.DIVAN_DATA_DIR ?? new URL('../../../divan-data', import.meta.url).pathname;
const isModerator = (u: any) => ['mod-l2', 'mod-l1', 'admin'].includes(u?.role);
const mayEdit = (u: any, poemId: number) => can(u, 'edit', 'works', { poemId });
const mayReview = async (u: any, poemId: number) => ['mod-l1', 'admin'].includes(u?.role) && (await mayEdit(u, poemId));
const OPEN = ['draft', 'returned'];
// a draft whose text is still the text it started from is not shown anywhere (opening the editor is not a change)
const CHANGED = `(r.status <> 'draft' OR r.content <> r.base_content)`;

async function moderator(req: FastifyRequest, reply: FastifyReply) {
  const u = await sessionUser(req);
  if (!u) return void reply.code(401).send({ error: 'لاگ ان کریں' });
  if (!isModerator(u)) return void reply.code(403).send({ error: 'صرف موڈریٹرز کے لیے' });
  return u;
}

const event = (revId: number, u: any, action: string, comment?: string | null) =>
  pool.query('INSERT INTO revision_events (revision_id, actor_id, actor_email, action, comment) VALUES ($1, $2, $3, $4, $5)',
    [revId, u?.id ?? null, u?.email ?? 'server', action, comment || null]);

// a work's current published text: its latest Divan version, or the Wikisource text as Divan text
async function current(poemId: number) {
  const poem = (await pool.query(
    'SELECT p.id, p.url, p.title, p.source_url, t.nickname AS poet FROM poems p JOIN poets t ON t.id = p.poet_id WHERE p.id = $1', [poemId])).rows[0];
  if (!poem) return null;
  const last = (await pool.query(
    `SELECT version, content FROM revisions WHERE entity = 'work' AND entity_id = $1 AND status = 'published' ORDER BY version DESC LIMIT 1`, [poemId])).rows[0];
  if (last) return { poem, version: last.version as number, content: last.content as string };
  const verses = (await pool.query('SELECT position AS "Position", couplet AS "CoupletIndex", text AS "Text" FROM verses WHERE poem_id = $1 ORDER BY vorder', [poemId])).rows;
  return { poem, version: 0, content: fromPoem({ Title: poem.title, Verses: verses, SourceUrl: poem.source_url ?? undefined }, { شاعر: poem.poet }) };
}

async function revision(id: number) {
  return (await pool.query(
    `SELECT r.*, p.title AS work_title, p.url AS work_url FROM revisions r JOIN poems p ON p.id = r.entity_id
     WHERE r.id = $1 AND r.entity = 'work'`, [id])).rows[0];
}

// what this person may do with this revision now
async function actions(u: any, r: any) {
  const mine = Number(r.author_id) === Number(u.id), review = await mayReview(u, r.entity_id), admin = u.role === 'admin';
  return {
    save: mine && OPEN.includes(r.status),
    submit: mine && OPEN.includes(r.status),
    approve: !mine && review && r.status === 'submitted',
    return: (review || admin) && ['submitted', 'approved'].includes(r.status) && !(mine && !admin),
    reject: (review || admin) && ['submitted', 'approved'].includes(r.status) && !(mine && !admin),
    publish: admin && r.status === 'approved',
  };
}

async function publish(r: any, u: any, comment?: string) {
  const cur = await current(r.entity_id);
  if (!cur) throw Object.assign(new Error('کلام نہیں ملا'), { code: 404 });
  if (cur.version !== r.base_version)
    throw Object.assign(new Error('اس دوران اس کلام کا نیا ورژن شائع ہو چکا ہے۔ مسودہ واپس بھیج کر تازہ متن پر دوبارہ بنوائیں۔'), { code: 409 });
  const doc = parse(r.content), verses = toVerses(doc);
  const title = doc.meta['عنوان'] || cur.poem.title, version = cur.version + 1, at = new Date().toISOString();
  // who did it, by public name (divan-data is public: never email addresses)
  const people = async (id: unknown) => id ? (await pool.query('SELECT id, full_name FROM users WHERE id = $1', [id])).rows[0] ?? null : null;
  const [author, reviewer] = await Promise.all([people(r.author_id), people(r.reviewer_id)]);
  const who = (p: any) => p && { id: Number(p.id), name: publicName(p) };
  const credits = { by: who(author)?.name ?? 'موڈریٹر', reviewedBy: who(reviewer)?.name ?? null, publishedBy: publicName(u) };
  // divan-data first (the published record, committed to git), then the site's database
  const files = await writeOwned(dataDir(), cur.poem.url, r.content, { ...credits, at, version, revision: Number(r.id) });
  const trailers = [`Divan-Revision: ${r.id}`, `Divan-Version: ${version}`,
    ...(reviewer ? [`Reviewed-by: ${identityOf(reviewer)}`] : []), `Approved-by: ${identityOf(u)}`];
  const sha = await commit(dataDir(), files.map((f) => f.slice(dataDir().replace(/\/$/, '').length + 1)),
    who(author) ?? { id: 0, name: 'موڈریٹر' }, `${title}: ${r.summary || 'ترمیم'} (ورژن ${version})\n\n${trailers.join('\n')}`);
  const client = await pool.connect();
  try {
    await client.query('BEGIN');
    await client.query(`UPDATE revisions SET status = 'published', version = $2, publisher_email = $3, published_at = $4, commit = $5, credits = $6, updated_at = now()
                        WHERE id = $1`, [r.id, version, u.email, at, sha, credits]);
    await client.query('UPDATE poems SET title = $2, search_text = $3 WHERE id = $1',
      [r.entity_id, title, normalise([title, ...verses.map((v) => v.Text)].join(' '))]);
    await client.query('DELETE FROM verses WHERE poem_id = $1', [r.entity_id]);
    for (const v of verses)
      await client.query('INSERT INTO verses (poem_id, vorder, position, couplet, text) VALUES ($1, $2, $3, $4, $5)',
        [r.entity_id, v.VOrder, v.Position, v.CoupletIndex, v.Text]);
    await client.query('COMMIT');
  } catch (e) {
    await client.query('ROLLBACK');
    throw e;
  } finally {
    client.release();
  }
  // ponytail: radif/matla/maqta and the contents order are recomputed by the next daily export + import
  await event(r.id, u, 'published', comment);
  return version;
}

const identityOf = (p: any) => identity({ id: Number(p.id), name: publicName(p) });

export function moderationRoutes(app: FastifyInstance) {
  app.get<{ Querystring: { poem?: string } }>('/api/mod/can', async (req) => {
    const u = await sessionUser(req), poemId = Number(req.query.poem) || 0;
    if (!isModerator(u)) return { edit: false, review: false, publish: false };
    return { edit: await mayEdit(u, poemId), review: await mayReview(u, poemId), publish: u.role === 'admin' };
  });

  app.get('/api/mod/queue', async (req, reply) => {
    const u = await moderator(req, reply); if (!u) return;
    const { rows } = await pool.query(
      `SELECT r.id, r.entity_id, r.status, r.summary, r.author_id, r.author_email, r.reviewer_email, r.updated_at, p.title, p.url
       FROM revisions r JOIN poems p ON p.id = r.entity_id
       WHERE r.entity = 'work' AND (r.status IN ('submitted', 'approved') OR (r.author_id = $1 AND r.status IN ('draft', 'returned') AND ${CHANGED}))
       ORDER BY r.updated_at DESC LIMIT 300`, [u.id]);
    const strip = ({ author_id, ...r }: any) => ({ ...r, id: Number(r.id) });
    const review = [];
    for (const r of rows) if (r.status === 'submitted' && Number(r.author_id) !== Number(u.id) && (await mayReview(u, r.entity_id))) review.push(strip(r));
    return {
      mine: rows.filter((r) => Number(r.author_id) === Number(u.id)).map(strip),
      review,
      publish: u.role === 'admin' ? rows.filter((r) => r.status === 'approved').map(strip) : [],
    };
  });

  app.get<{ Params: { id: string } }>('/api/mod/work/:id', async (req, reply) => {
    const u = await moderator(req, reply); if (!u) return;
    const cur = await current(Number(req.params.id) || 0);
    if (!cur) return reply.code(404).send({ error: 'کلام نہیں ملا' });
    const { rows } = await pool.query(
      `SELECT id, version, base_version, status, summary, author_email, reviewer_email, publisher_email, created_at, published_at
       FROM revisions r WHERE entity = 'work' AND entity_id = $1 AND ${CHANGED} ORDER BY coalesce(published_at, created_at) DESC`, [cur.poem.id]);
    return { work: cur.poem, version: cur.version, content: cur.content, history: rows.map((r) => ({ ...r, id: Number(r.id) })),
      may: { edit: await mayEdit(u, cur.poem.id) } };
  });

  app.post<{ Params: { id: string } }>('/api/mod/work/:id/draft', async (req, reply) => {
    const u = await moderator(req, reply); if (!u) return;
    const poemId = Number(req.params.id) || 0;
    if (!(await mayEdit(u, poemId))) return reply.code(403).send({ error: 'اس کلام میں ترمیم کی اجازت نہیں' });
    const open = (await pool.query(
      `SELECT id FROM revisions WHERE entity = 'work' AND entity_id = $1 AND author_id = $2 AND status IN ('draft', 'returned') LIMIT 1`, [poemId, u.id])).rows[0];
    if (open) return { id: Number(open.id) };
    const cur = await current(poemId);
    if (!cur) return reply.code(404).send({ error: 'کلام نہیں ملا' });
    const { rows } = await pool.query(
      `INSERT INTO revisions (entity, entity_id, base_version, base_content, content, status, author_id, author_email)
       VALUES ('work', $1, $2, $3, $3, 'draft', $4, $5) RETURNING id`,
      [poemId, cur.version, cur.content, u.id, u.email]);
    await event(rows[0].id, u, 'created');
    return { id: Number(rows[0].id) };
  });

  app.get<{ Params: { id: string } }>('/api/mod/revisions/:id', async (req, reply) => {
    const u = await moderator(req, reply); if (!u) return;
    const r = await revision(Number(req.params.id) || 0);
    if (!r) return reply.code(404).send({ error: 'مسودہ نہیں ملا' });
    const may = await actions(u, r);
    if (Number(r.author_id) !== Number(u.id) && !(await mayReview(u, r.entity_id)) && u.role !== 'admin')
      return reply.code(403).send({ error: 'یہ مسودہ دیکھنے کی اجازت نہیں' });
    const diff = diffLines(r.base_content, r.content); // against the text the draft started from
    const events = (await pool.query('SELECT actor_email, action, comment, at FROM revision_events WHERE revision_id = $1 ORDER BY at, id', [r.id])).rows;
    const { author_id, base_content, ...rest } = r;
    return { revision: { ...rest, id: Number(r.id) }, diff, changes: changed(diff), events, may };
  });

  app.post<{ Params: { id: string }; Body: { content?: string; summary?: string } }>('/api/mod/revisions/:id/save', async (req, reply) => {
    const u = await moderator(req, reply); if (!u) return;
    const r = await revision(Number(req.params.id) || 0);
    if (!r) return reply.code(404).send({ error: 'مسودہ نہیں ملا' });
    if (!(await actions(u, r)).save) return reply.code(403).send({ error: 'یہ مسودہ اب محفوظ نہیں کیا جا سکتا' });
    const content = String(req.body?.content ?? '').replace(/\r\n?/g, '\n');
    if (!toVerses(parse(content)).length) return reply.code(400).send({ error: 'متن میں کوئی شعر یا پیراگراف نہیں' });
    if (content.length > 500_000) return reply.code(400).send({ error: 'متن بہت لمبا ہے' });
    // no change from the text it started from (compared as Divan text, so layout-only differences don't count):
    // a plain draft is dropped rather than kept
    const same = toText(parse(content)) === toText(parse(r.base_content));
    if (same && r.status === 'draft') {
      await pool.query('DELETE FROM revisions WHERE id = $1', [r.id]);
      return { discarded: true };
    }
    if (same) return reply.code(400).send({ error: 'متن میں کوئی تبدیلی نہیں' });
    const summary = String(req.body?.summary ?? '').trim().slice(0, 500) || null;
    await pool.query('UPDATE revisions SET content = $2, summary = $3, updated_at = now() WHERE id = $1', [r.id, content, summary]);
    await event(r.id, u, 'saved');
    return { ok: true };
  });

  app.post<{ Params: { id: string; action: string }; Body: { comment?: string } }>('/api/mod/revisions/:id/:action', async (req, reply) => {
    const u = await moderator(req, reply); if (!u) return;
    const r = await revision(Number(req.params.id) || 0);
    if (!r) return reply.code(404).send({ error: 'مسودہ نہیں ملا' });
    const action = req.params.action as keyof Awaited<ReturnType<typeof actions>>, may = await actions(u, r);
    if (!['submit', 'approve', 'return', 'reject', 'publish'].includes(action)) return reply.code(404).send({ error: 'نامعلوم عمل' });
    if (!may[action]) return reply.code(403).send({ error: 'یہ عمل آپ کے لیے دستیاب نہیں' });
    const comment = String(req.body?.comment ?? '').trim().slice(0, 2000);
    if ((action === 'return' || action === 'reject') && !comment) return reply.code(400).send({ error: 'وجہ لکھیں' });
    const set = (status: string, extra = '') => pool.query(`UPDATE revisions SET status = $2, updated_at = now()${extra} WHERE id = $1`, [r.id, status]);
    try {
      if (action === 'submit') {
        // L2 -> L1 review; an L1's own draft goes to the admin; an admin's own draft publishes
        if (u.role === 'admin') { await set('approved'); await event(r.id, u, 'submitted', comment); return { status: 'published', version: await publish({ ...r, status: 'approved' }, u) }; }
        await set(u.role === 'mod-l1' ? 'approved' : 'submitted');
        await event(r.id, u, 'submitted', comment);
        return { status: u.role === 'mod-l1' ? 'approved' : 'submitted' };
      }
      if (action === 'approve') {
        await pool.query(`UPDATE revisions SET status = 'approved', reviewer_id = $3, reviewer_email = $2, updated_at = now() WHERE id = $1`, [r.id, u.email, u.id]);
        await event(r.id, u, 'approved', comment);
        return { status: 'approved' };
      }
      if (action === 'return' || action === 'reject') {
        await set(action === 'return' ? 'returned' : 'rejected');
        await event(r.id, u, action === 'return' ? 'returned' : 'rejected', comment);
        return { status: action === 'return' ? 'returned' : 'rejected' };
      }
      return { status: 'published', version: await publish(r, u, comment) };
    } catch (e: any) {
      return reply.code(e.code ?? 500).send({ error: e.message });
    }
  });

  app.get<{ Querystring: { page?: string } }>('/api/mod/log', async (req, reply) => {
    const u = await moderator(req, reply); if (!u) return;
    const page = Math.max(1, Number(req.query.page) || 1);
    const { rows } = await pool.query(
      `SELECT e.at, e.actor_email, e.action, e.comment, r.id AS revision, r.version, p.title, p.url
       FROM revision_events e JOIN revisions r ON r.id = e.revision_id JOIN poems p ON p.id = r.entity_id
       WHERE ${CHANGED}
       ORDER BY e.at DESC, e.id DESC LIMIT 50 OFFSET ${(page - 1) * 50}`);
    return { page, entries: rows.map((r) => ({ ...r, revision: Number(r.revision) })) };
  });
}
