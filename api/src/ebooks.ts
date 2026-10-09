// E-books (owner request): a poet's books as files (PDF, EPUB, text; a .docx becomes text) or archive.org items,
// uploaded by moderators with the 'ebooks' permission and listed under ای بکس on the poet's page once approved.
// Anyone with the permission may upload any book; reviewers (L1, admin) confirm it, as with every other change.
// Files live in the file store (DIVAN_FILES_DIR) under their content hash, ab/cd/<sha256>.<ext>, so a file uploaded
// twice is kept once; the database holds the details, indexed by poet and (trigram) by title and the text of text
// books, so lists and search stay fast. An approved e-book's details (not the file) are committed to divan-data.
// The details are a revision (entity 'ebook'), one per line: "عنوان: …", "ماخذ: …", "اجازت: …", "تفصیل: …".
//   POST /api/mod/ebooks/upload?poet=&name=   the file as the request body (up to 100 MB): {file, kind, size}
//   POST /api/mod/ebooks                      {poet, title, file | archive, source, licence, note}: the e-book and its draft
//   GET  /api/ebooks?poet=                    a poet's published e-books
//   GET  /api/ebook/:id                       one e-book (unpublished: moderators only); the text of a text book
//   GET  /api/ebook/:id/file                  its file
import { createHash, randomUUID } from 'node:crypto';
import { createReadStream, createWriteStream } from 'node:fs';
import { mkdir, open, readFile, rename, rm, stat, writeFile } from 'node:fs/promises';
import { dirname, join } from 'node:path';
import type { FastifyInstance, FastifyReply, FastifyRequest } from 'fastify';
import type { PoolClient } from 'pg';
import mammoth from 'mammoth';
import { pool } from './db.ts';
import { sessionUser } from './auth.ts';
import { can } from './permissions.ts';
import { normalise } from './urdu.ts';

const MAX = 100 * 1024 * 1024;
const store = () => process.env.DIVAN_FILES_DIR ?? new URL('../../../divan-files', import.meta.url).pathname;
const pathOf = (file: string) => join(store(), file.slice(0, 2), file.slice(2, 4), file);
const TYPES: Record<string, string> = { pdf: 'application/pdf', epub: 'application/epub+zip', txt: 'text/plain; charset=utf-8' };
const KEYS = { title: 'عنوان', source: 'ماخذ', licence: 'اجازت', note: 'تفصیل' } as const;
type Meta = { title: string; source: string; licence: string; note: string };
const isModerator = (u: any) => ['mod-l2', 'mod-l1', 'admin'].includes(u?.role);
export const mayUpload = (u: any, poetId: number) => can(u, 'create', 'ebooks', { poetId });

// the details as revision text, and back
export const metaText = (m: Partial<Meta>) =>
  (Object.keys(KEYS) as (keyof Meta)[]).map((k) => `${KEYS[k]}: ${String(m[k] ?? '').replace(/\s+/g, ' ').trim()}`).join('\n');
export function parseMeta(text: string): Meta | string {
  const m: any = { title: '', source: '', licence: '', note: '' };
  for (const line of text.split('\n')) {
    const i = line.indexOf(':'); if (i < 0) continue;
    const key = (Object.keys(KEYS) as (keyof Meta)[]).find((k) => KEYS[k] === line.slice(0, i).trim());
    if (key) m[key] = line.slice(i + 1).trim().slice(0, key === 'note' ? 2000 : 300);
  }
  return m.title ? m : 'کتاب کا عنوان ضروری ہے';
}

// archive.org: an item id, or a link to it
export const archiveId = (s: string) => {
  const id = String(s ?? '').trim().replace(/^https?:\/\/(www\.)?archive\.org\/(details|embed)\//, '').split(/[/?#]/)[0];
  return /^[A-Za-z0-9._-]{3,100}$/.test(id) ? id : null;
};

// what the bytes are: PDF, EPUB (a zip whose first entry is "mimetype"), a .docx (made into text), or UTF-8 text
async function kindOf(path: string, name: string) {
  const fh = await open(path); const head = Buffer.alloc(64);
  await fh.read(head, 0, 64, 0); await fh.close();
  if (head.subarray(0, 5).toString() === '%PDF-') return 'pdf';
  if (head.subarray(0, 4).toString('binary') === 'PK\x03\x04') {
    if (head.subarray(30, 58).toString() === 'mimetypeapplication/epub+zip') return 'epub';
    if (/\.docx$/i.test(name)) return 'docx';
    return null;
  }
  if (/\.txt$/i.test(name)) {
    try { new TextDecoder('utf-8', { fatal: true }).decode(await readFile(path)); return 'txt'; } catch { return null; }
  }
  return null;
}

// keep a file in the store under its hash; returns its name there
async function keep(tmp: string, ext: string) {
  const sha = createHash('sha256').update(await readFile(tmp)).digest('hex');
  const file = `${sha}.${ext}`, dest = pathOf(file);
  await mkdir(dirname(dest), { recursive: true });
  if (await stat(dest).catch(() => null)) await rm(tmp); else await rename(tmp, dest);
  return file;
}

const textOf = async (file: string) => (await readFile(pathOf(file), 'utf8')).replace(/^﻿/, '');
const searchText = async (title: string, kind: string, file: string | null) =>
  normalise([title, kind === 'text' && file ? (await textOf(file)).slice(0, 2_000_000) : ''].join(' '));

// an e-book's published details (empty until approved, so its first draft is a change), and its version
export async function currentEbook(id: number) {
  const ebook = (await pool.query(
    'SELECT b.*, t.url AS poet_url, t.nickname AS poet FROM ebooks b JOIN poets t ON t.id = b.poet_id WHERE b.id = $1', [id])).rows[0];
  if (!ebook) return null;
  const last = (await pool.query(`SELECT max(version) AS v FROM revisions WHERE entity = 'ebook' AND entity_id = $1 AND status = 'published'`, [id])).rows[0];
  return { ebook, version: Number(last?.v ?? 0), content: ebook.published ? metaText(ebook) : '' };
}

export const checkEbook = (text: string) => { const m = parseMeta(text); return typeof m === 'string' ? m : null; };

// publishing: the details become the e-book's and it is listed
export async function applyEbook(client: PoolClient, id: number, text: string) {
  const m = parseMeta(text) as Meta;
  const b = (await client.query('SELECT kind, file FROM ebooks WHERE id = $1', [id])).rows[0];
  await client.query('UPDATE ebooks SET title = $2, source = $3, licence = $4, note = $5, search_text = $6, published = true WHERE id = $1',
    [id, m.title, m.source || null, m.licence || null, m.note || null, await searchText(m.title, b.kind, b.file)]);
}

// divan-data/divan/<poet>/ebooks/<id>.txt: the details and where the file is (the file itself stays in the store)
export async function writeEbook(dataDir: string, ebook: any, text: string) {
  const path = join(dataDir, 'divan', ebook.poet_url.replace(/^\//, ''), 'ebooks', `${ebook.id}.txt`);
  const where = ebook.kind === 'archive' ? `آرکائیو: https://archive.org/details/${ebook.archive_id}`
    : `فائل: ${ebook.file} (${ebook.kind}، ${ebook.size} بائٹ)`;
  await mkdir(dirname(path), { recursive: true });
  await writeFile(path, `${text.trim()}\n${where}\n`);
  return path;
}

const viewer = async (req: FastifyRequest) => { const u = await sessionUser(req); return isModerator(u) ? u : null; };

export function ebookRoutes(app: FastifyInstance) {
  // uploads arrive as the raw request body
  app.addContentTypeParser('application/octet-stream', (_req, payload, done) => done(null, payload));

  app.post<{ Querystring: { poet?: string; name?: string } }>('/api/mod/ebooks/upload', async (req, reply) => {
    const u = await sessionUser(req);
    if (!u) return reply.code(401).send({ error: 'لاگ ان کریں' });
    if (!(await mayUpload(u, Number(req.query.poet) || 0))) return reply.code(403).send({ error: 'اس شاعر کی ای بکس شامل کرنے کی اجازت نہیں' });
    const tmp = join(store(), 'tmp', randomUUID());
    await mkdir(dirname(tmp), { recursive: true });
    let size = 0;
    try {
      await new Promise<void>((resolve, reject) => {
        const out = createWriteStream(tmp);
        const body = req.body as NodeJS.ReadableStream;
        body.on('data', (c: Buffer) => { size += c.length; if (size > MAX) { body.removeAllListeners('data'); out.destroy(); reject(Object.assign(new Error('فائل ۱۰۰ میگا بائٹ سے بڑی ہے'), { code: 413 })); } else out.write(c); });
        body.on('end', () => out.end(() => resolve()));
        body.on('error', reject);
      });
      if (!size) throw Object.assign(new Error('فائل خالی ہے'), { code: 400 });
      const kind = await kindOf(tmp, String(req.query.name ?? ''));
      if (!kind) throw Object.assign(new Error('صرف PDF، EPUB، TXT یا DOCX فائلیں'), { code: 415 });
      if (kind === 'docx') {
        const { value } = await mammoth.extractRawText({ path: tmp });
        await writeFile(tmp, value.replace(/\n{3,}/g, '\n\n').trim() + '\n');
        size = (await stat(tmp)).size;
      }
      const ext = kind === 'docx' ? 'txt' : kind;
      return { file: await keep(tmp, ext), kind: ext === 'txt' ? 'text' : ext, size };
    } catch (e: any) {
      await rm(tmp, { force: true });
      return reply.code(e.code ?? 500).send({ error: e.message });
    }
  });

  app.post<{ Body: any }>('/api/mod/ebooks', async (req, reply) => {
    const u = await sessionUser(req);
    if (!u) return reply.code(401).send({ error: 'لاگ ان کریں' });
    const b = req.body ?? {}, poetId = Number(b.poet) || 0;
    if (!(await mayUpload(u, poetId))) return reply.code(403).send({ error: 'اس شاعر کی ای بکس شامل کرنے کی اجازت نہیں' });
    const meta = parseMeta(metaText(b));
    if (typeof meta === 'string') return reply.code(400).send({ error: meta });
    let kind: string, file: string | null = null, archive: string | null = null, size: number | null = null;
    if (b.archive) {
      archive = archiveId(b.archive);
      if (!archive) return reply.code(400).send({ error: 'آرکائیو کا ربط درست نہیں (archive.org/details/…)' });
      kind = 'archive';
    } else {
      file = /^[0-9a-f]{64}\.(pdf|epub|txt)$/.test(b.file ?? '') ? b.file : null;
      const st = file && (await stat(pathOf(file)).catch(() => null));
      if (!file || !st) return reply.code(400).send({ error: 'پہلے فائل اپ لوڈ کریں' });
      kind = file.endsWith('.txt') ? 'text' : file.slice(-4).replace('.', '');
      size = st.size;
    }
    const content = metaText(meta);
    const client = await pool.connect();
    try {
      await client.query('BEGIN');
      const id = (await client.query(
        `INSERT INTO ebooks (poet_id, title, kind, file, archive_id, size, source, licence, note, created_by)
         VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10) RETURNING id`,
        [poetId, meta.title, kind, file, archive, size, meta.source || null, meta.licence || null, meta.note || null, u.id])).rows[0].id;
      const rev = (await client.query(
        `INSERT INTO revisions (entity, entity_id, base_version, base_content, content, status, author_id, author_email, summary)
         VALUES ('ebook', $1, 0, '', $2, 'draft', $3, $4, $5) RETURNING id`, [id, content, u.id, u.email, 'نئی ای بک'])).rows[0].id;
      await client.query(`INSERT INTO revision_events (revision_id, actor_id, actor_email, action) VALUES ($1, $2, $3, 'created')`, [rev, u.id, u.email]);
      await client.query('COMMIT');
      return { id, revision: Number(rev) };
    } catch (e) {
      await client.query('ROLLBACK');
      throw e;
    } finally {
      client.release();
    }
  });

  app.get<{ Querystring: { poet?: string } }>('/api/ebooks', async (req) => (await pool.query(
    `SELECT id, title, kind, size, source FROM ebooks WHERE poet_id = $1 AND published ORDER BY title`, [Number(req.query.poet) || 0])).rows);

  const ebook = async (req: FastifyRequest<{ Params: { id: string } }>, reply: FastifyReply) => {
    const b = (await pool.query(
      'SELECT b.*, t.url AS poet_url, t.nickname AS poet FROM ebooks b JOIN poets t ON t.id = b.poet_id WHERE b.id = $1', [Number(req.params.id) || 0])).rows[0];
    if (!b || (!b.published && !(await viewer(req)))) return void reply.code(404).send({ error: 'کتاب نہیں ملی' });
    return b;
  };

  app.get<{ Params: { id: string } }>('/api/ebook/:id', async (req, reply) => {
    const b = await ebook(req, reply); if (!b) return;
    const { search_text, created_by, ...rest } = b;
    return { ...rest, text: b.kind === 'text' ? (await textOf(b.file)).slice(0, 3_000_000) : undefined };
  });

  app.get<{ Params: { id: string } }>('/api/ebook/:id/file', async (req, reply) => {
    const b = await ebook(req, reply); if (!b) return;
    if (!b.file) return reply.code(404).send({ error: 'اس کتاب کی فائل نہیں' });
    const ext = b.file.split('.').pop();
    reply.header('content-type', TYPES[ext]).header('content-length', String((await stat(pathOf(b.file))).size))
      .header('content-disposition', `inline; filename="divan-${b.id}.${ext}"`)
      .header('cache-control', b.published ? 'public, max-age=86400' : 'private, no-store');
    return reply.send(createReadStream(pathOf(b.file)));
  });
}
