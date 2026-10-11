// Readers send in classical works (the «شامل کریں» page): a work with its poet, book and source. A moderator who may
// create works in the chosen book prepares its Divan text and accepts it, or rejects it with a note to the reader;
// an admin publishes it (an admin may also publish a pending one directly). Publishing writes it to divan-data as a
// Divan-owned work <book url>/s<id> (owned.ts) and commits it there; the site gets it with the next sync
// (export_divan.py add_owned, then the import), like the other works only Divan has.
//   POST /api/submissions                       {poet, book, title, kind, text, source, note}   (signed in)
//   GET  /api/submissions/mine                  my submissions and what became of them
//   GET  /api/mod/submissions?status=           moderators: pending (default), accepted, rejected, published
//   GET  /api/mod/submissions/:id               one submission with its Divan text (prepared, or drafted from it)
//   POST /api/mod/submissions/:id/:action       accept | reject | publish   {category, content, note}
import type { FastifyInstance } from 'fastify';
import { pool } from './db.ts';
import { sessionUser, publicName } from './auth.ts';
import { can } from './permissions.ts';
import { parse, toVerses } from './divantext.ts';
import { writeOwned } from './owned.ts';
import { commit } from './git.ts';

const dataDir = () => process.env.DIVAN_DATA_DIR ?? new URL('../../../divan-data', import.meta.url).pathname;
const MODS = ['mod-l2', 'mod-l1', 'admin'];
const PENDING_MAX = 20; // ponytail: a cap per reader instead of a rate limiter; enough until there are many readers
const isLink = (s: string) => /^https?:\/\/\S+$/.test(s.trim());
const clean = (s: unknown, max: number) => String(s ?? '').replace(/\r\n?/g, '\n').trim().slice(0, max);

// the reader's text as Divan text: verse in <poem> (a blank line between couplets or stanzas), prose as paragraphs
export function draftText(s: { title: string; poet: string; kind: string; text: string; source: string }) {
  const body = s.text.replace(/\r\n?/g, '\n').replace(/\n{3,}/g, '\n\n').trim();
  const head = [`| عنوان = ${s.title}`, `| شاعر = ${s.poet}`, isLink(s.source) ? `| ماخذ_لنک = ${s.source.trim()}` : `| ماخذ = ${s.source}`];
  return `{{دیوان\n${head.join('\n')}\n}}\n${s.kind === 'poetry' ? `<poem>\n${body}\n</poem>` : body}\n`;
}

const err = (code: number, error: string) => Object.assign(new Error(error), { code });

async function category(ref: unknown) {
  const url = '/' + String(ref ?? '').trim().replace(/^https?:\/\/[^/]+/, '').replace(/[?#].*$/, '').replace(/^\/+|\/+$/g, '');
  return (await pool.query('SELECT id, url, title FROM categories WHERE url = $1', [decodeURI(url)])).rows[0] as { id: number; url: string; title: string } | undefined;
}

// a moderator's decision; accepting and publishing need the right to create works in that book
async function decide(u: any, id: number, action: string, b: any) {
  const s = (await pool.query('SELECT * FROM submissions WHERE id = $1', [id])).rows[0];
  if (!s) throw err(404, 'نہیں ملا');
  const note = clean(b.note, 2000) || s.review_note;
  if (action === 'reject') {
    if (!['pending', 'accepted'].includes(s.status)) throw err(409, 'اس پر فیصلہ ہو چکا ہے');
    if (!note) throw err(400, 'پڑھنے والے کے لیے وجہ لکھیں');
    await pool.query(`UPDATE submissions SET status = 'rejected', review_note = $2, reviewer_id = $3, updated_at = now() WHERE id = $1`, [id, note, u.id]);
    return { status: 'rejected' };
  }
  if (action !== 'accept' && action !== 'publish') throw err(404, 'نہیں ملا');
  if (action === 'publish' && u.role !== 'admin') throw err(403, 'صرف ایڈمن شائع کرتے ہیں');
  if (!(action === 'accept' ? ['pending'] : ['pending', 'accepted']).includes(s.status)) throw err(409, 'اس پر فیصلہ ہو چکا ہے');
  const cat = b.category ? await category(b.category) : s.category_id && (await pool.query('SELECT id, url, title FROM categories WHERE id = $1', [s.category_id])).rows[0];
  if (!cat) throw err(400, 'کتاب یا حصہ چنیں (اس کا ربط، جیسے /p238/shaeri/c641)');
  if (!(await can(u, 'create', 'works', { categoryId: cat.id }))) throw err(403, 'اس کتاب میں نیا کلام شامل کرنے کی اجازت نہیں');
  const content = clean(b.content, 200_000) || s.content || draftText(s);
  const doc = parse(content);
  if (!doc.meta['عنوان']) throw err(400, 'متن میں عنوان نہیں');
  if (!toVerses(doc).length) throw err(400, 'متن خالی ہے');
  if (action === 'accept') {
    await pool.query(`UPDATE submissions SET status = 'accepted', category_id = $2, content = $3, review_note = $4, reviewer_id = $5, updated_at = now() WHERE id = $1`,
      [id, cat.id, content, note || null, u.id]);
    return { status: 'accepted' };
  }
  // published: divan-data first (the public record, by public names), then the submission
  const people = async (pid: unknown) => pid ? (await pool.query('SELECT id, full_name FROM users WHERE id = $1', [pid])).rows[0] ?? null : null;
  const [author, reviewer] = await Promise.all([people(s.user_id), people(s.reviewer_id ?? (s.status === 'pending' ? u.id : null))]);
  const by = author?.full_name?.trim() || 'قاری'; // a reader without a name (publicName would say «موڈریٹر»)
  const url = `${cat.url}/s${id}`;
  const files = await writeOwned(dataDir(), url, content, { by, reviewedBy: reviewer ? publicName(reviewer) : null, publishedBy: publicName(u) },
    { SourceName: isLink(s.source) ? null : s.source, SourceUrl: isLink(s.source) ? s.source.trim() : null, ...(url.includes('/farsi/') && { Language: 'fa-IR' }) });
  const sha = await commit(dataDir(), files.map((f) => f.slice(dataDir().replace(/\/$/, '').length + 1)),
    { id: Number(author?.id ?? 0), name: by }, `${doc.meta['عنوان']}: ${cat.title} میں نیا کلام\n\nDivan-Submission: ${id}`);
  await pool.query(`UPDATE submissions SET status = 'published', category_id = $2, content = $3, review_note = $4, reviewer_id = coalesce(reviewer_id, $5),
                    publisher_id = $5, url = $6, commit = $7, updated_at = now() WHERE id = $1`, [id, cat.id, content, note || null, u.id, url, sha]);
  return { status: 'published', url };
}

export function submissionRoutes(app: FastifyInstance) {
  app.post<{ Body: any }>('/api/submissions', async (req, reply) => {
    const u = await sessionUser(req);
    if (!u) return reply.code(401).send({ error: 'لاگ ان کریں' });
    const b = req.body ?? {};
    const s = { poet: clean(b.poet, 200), book: clean(b.book, 300), title: clean(b.title, 300), kind: b.kind === 'prose' ? 'prose' : 'poetry',
      text: clean(b.text, 100_000), source: clean(b.source, 1000), note: clean(b.note, 2000) };
    const missing = [['poet', 'شاعر یا مصنف'], ['title', 'عنوان'], ['text', 'متن'], ['source', 'ماخذ']].find(([k]) => !(s as any)[k]);
    if (missing) return reply.code(400).send({ error: `${missing[1]} لکھیں` });
    const pending = (await pool.query(`SELECT count(*)::int AS n FROM submissions WHERE user_id = $1 AND status = 'pending'`, [u.id])).rows[0].n;
    if (pending >= PENDING_MAX) return reply.code(429).send({ error: `آپ کی ${PENDING_MAX} تحریریں پہلے ہی جائزے کی منتظر ہیں؛ ان کے جائزے کے بعد مزید بھیجیں` });
    const id = (await pool.query(`INSERT INTO submissions (user_id, poet, book, title, kind, text, source, note) VALUES ($1, $2, $3, $4, $5, $6, $7, $8) RETURNING id`,
      [u.id, s.poet, s.book || null, s.title, s.kind, s.text, s.source, s.note || null])).rows[0].id;
    return { id };
  });

  app.get('/api/submissions/mine', async (req, reply) => {
    const u = await sessionUser(req);
    if (!u) return reply.code(401).send({ error: 'لاگ ان کریں' });
    return (await pool.query(`SELECT id, poet, title, status, review_note, url, created_at, updated_at FROM submissions WHERE user_id = $1 ORDER BY created_at DESC LIMIT 200`, [u.id])).rows;
  });

  app.get<{ Querystring: { status?: string } }>('/api/mod/submissions', async (req, reply) => {
    const u = await sessionUser(req);
    if (!u || !MODS.includes(u.role)) return reply.code(u ? 403 : 401).send({ error: 'صرف موڈریٹرز کے لیے' });
    const status = ['pending', 'accepted', 'rejected', 'published'].includes(req.query.status ?? '') ? req.query.status : 'pending';
    return (await pool.query(`SELECT s.id, s.poet, s.book, s.title, s.kind, s.status, s.created_at, s.updated_at, c.title AS category
      FROM submissions s LEFT JOIN categories c ON c.id = s.category_id WHERE s.status = $1 ORDER BY s.created_at LIMIT 500`, [status])).rows;
  });

  app.get<{ Params: { id: string } }>('/api/mod/submissions/:id', async (req, reply) => {
    const u = await sessionUser(req);
    if (!u || !MODS.includes(u.role)) return reply.code(u ? 403 : 401).send({ error: 'صرف موڈریٹرز کے لیے' });
    const s = (await pool.query(`SELECT s.*, c.url AS category_url, c.title AS category, us.full_name AS by_name, us.email AS by_email
      FROM submissions s LEFT JOIN categories c ON c.id = s.category_id LEFT JOIN users us ON us.id = s.user_id WHERE s.id = $1`, [Number(req.params.id) || 0])).rows[0];
    if (!s) return reply.code(404).send({ error: 'نہیں ملا' });
    return { ...s, content: s.content ?? draftText(s), admin: u.role === 'admin' };
  });

  app.post<{ Params: { id: string; action: string }; Body: any }>('/api/mod/submissions/:id/:action', async (req, reply) => {
    const u = await sessionUser(req);
    if (!u || !MODS.includes(u.role)) return reply.code(u ? 403 : 401).send({ error: 'صرف موڈریٹرز کے لیے' });
    try {
      return await decide(u, Number(req.params.id) || 0, req.params.action, req.body ?? {});
    } catch (e: any) {
      if (!e.code || typeof e.code !== 'number') throw e;
      return reply.code(e.code).send({ error: e.message });
    }
  });
}
