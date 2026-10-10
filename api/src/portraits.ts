// Poets' portraits: a freely licensed picture from Wikimedia for each poet who has one, kept in the file store and
// shown with the same duotone everywhere (web/src/components/PoetAvatar.astro); a generic avatar for the rest.
//   GET /api/poet/:id/portrait                 the stored picture (cached; the page adds ?v=<hash> to its link)
//   GET  /api/mod/portraits                   the portraits this admin or moderator may work on, and the poets they may upload for
//   POST /api/mod/portraits/:id {act}         approve (admins) | focus {x, y, z} (edit: the face's point and a zoom, kept from
//                                              then on) | remove (delete; not fetched again)
//   POST /api/mod/portraits/:id/upload?licence=&source=&artist=   a picture (jpg/png/webp, up to 2 MB) for a poet (create);
//                                              shown once an admin approves it (an admin's own shows at once)
// Moderators need the "تصاویر" (portraits) permission for the poet: edit, delete, create.
//   npm run portraits [-- --force] [-- --focus]   --focus: frame every portrait again on its face (not the hand-framed ones)
//   npm run portraits [-- --force]             fetch or refresh them; the daily sync runs it (only changed pictures download)
// Where a picture comes from, in order: the file divan-data names (Wikisource's author page), else the lead image of the
// poet's Urdu Wikipedia article. Only pictures Wikimedia marks as free are taken, and the artist and licence are kept
// for the credit line under the portrait.
import { spawnSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { createReadStream } from 'node:fs';
import { mkdir, readFile, writeFile } from 'node:fs/promises';
import { dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import type { FastifyInstance, FastifyRequest } from 'fastify';
import { pool } from './db.ts';
import { pathOf, TYPES, imageOf } from './ebooks.ts';
import { audit } from './admin.ts';
import { sessionUser } from './auth.ts';
import { can, MODERATORS } from './permissions.ts';

// the portrait the site shows: a Wikipedia lead image or an upload only once an admin approved it (it may be someone else,
// not a face, or not free to use)
export const SHOWN = `CASE WHEN portrait_credit->>'from' IN ('wikipedia', 'upload') AND portrait_credit->>'reviewed' IS NULL THEN NULL ELSE portrait END`;

export function portraitRoutes(app: FastifyInstance) {
  app.get<{ Params: { id: string } }>('/api/poet/:id/portrait', async (req, reply) => {
    const p = (await pool.query('SELECT portrait FROM poets WHERE id = $1', [Number(req.params.id) || 0])).rows[0];
    if (!p?.portrait) return reply.code(404).send({ error: 'تصویر نہیں' });
    reply.header('content-type', TYPES[p.portrait.split('.').pop()]).header('cache-control', 'public, max-age=31536000, immutable');
    return reply.send(createReadStream(pathOf(p.portrait)));
  });

  const staff = async (req: FastifyRequest) => { const u = await sessionUser(req); return u && (u.role === 'admin' || (MODERATORS as readonly string[]).includes(u.role)) ? u : null; };
  const waiting = (c: any) => ['wikipedia', 'upload'].includes(c?.from) && !c?.reviewed;

  app.get('/api/mod/portraits', async (req, reply) => {
    const u = await staff(req); if (!u) return reply.code(403).send({ error: 'صرف موڈریٹرز کے لیے' });
    const { rows } = await pool.query(`SELECT id, url, nickname, portrait, portrait_credit AS credit, portrait_credit->'focus' AS focus FROM poets
      WHERE portrait IS NOT NULL ORDER BY nickname`);
    const portraits = [];
    for (const p of rows) {
      const may = { edit: await can(u, 'edit', 'portraits', { poetId: p.id }), delete: await can(u, 'delete', 'portraits', { poetId: p.id }), approve: u.role === 'admin' };
      if (may.edit || may.delete) portraits.push({ ...p, waiting: waiting(p.credit), may });
    }
    portraits.sort((a, b) => Number(b.waiting) - Number(a.waiting)); // the ones waiting for approval first
    const all = (await pool.query('SELECT id, nickname FROM poets ORDER BY nickname')).rows;
    const upload = [];
    for (const p of all) if (await can(u, 'create', 'portraits', { poetId: p.id })) upload.push(p);
    return { portraits, upload, admin: u.role === 'admin' };
  });

  app.post<{ Params: { id: string }; Body: { act?: string; x?: number; y?: number; z?: number } }>('/api/mod/portraits/:id', async (req, reply) => {
    const u = await staff(req); if (!u) return reply.code(403).send({ error: 'صرف موڈریٹرز کے لیے' });
    const id = Number(req.params.id) || 0, act = req.body?.act;
    const p = (await pool.query('SELECT id, nickname, portrait_credit FROM poets WHERE id = $1 AND portrait IS NOT NULL', [id])).rows[0];
    if (!p) return reply.code(404).send({ error: 'تصویر نہیں ملی' });
    const need = act === 'approve' ? null : act === 'focus' ? 'edit' : act === 'remove' ? 'delete' : undefined;
    if (need === undefined) return reply.code(400).send({ error: 'نامعلوم عمل' });
    if (need === null ? u.role !== 'admin' : !(await can(u, need, 'portraits', { poetId: id }))) return reply.code(403).send({ error: 'اجازت نہیں' });
    if (act === 'approve') await pool.query(`UPDATE poets SET portrait_credit = portrait_credit || '{"reviewed": true}' WHERE id = $1`, [id]);
    else if (act === 'focus') {
      const n = (v: unknown, lo: number, hi: number) => Math.max(lo, Math.min(hi, Number(v) || 0));
      const b = req.body as any, focus = { x: n(b.x, 0, 1), y: n(b.y, 0, 1), z: n(b.z, 1, 3) || 1 };
      await pool.query(`UPDATE poets SET portrait_credit = portrait_credit || $2 WHERE id = $1`, [id, { focus, focus_manual: true }]);
    } else await pool.query(`UPDATE poets SET portrait = NULL, portrait_credit = $2 WHERE id = $1`, [id, { removed: true, source: p.portrait_credit?.source }]);
    await audit(u, `portrait-${act}`, null, { poet: p.nickname, file: p.portrait_credit?.source });
    return { ok: true };
  });

  app.post<{ Params: { id: string }; Querystring: { licence?: string; source?: string; artist?: string } }>(
    '/api/mod/portraits/:id/upload', { bodyLimit: 2 * 1024 * 1024 }, async (req, reply) => {
      const u = await staff(req); if (!u) return reply.code(403).send({ error: 'صرف موڈریٹرز کے لیے' });
      const id = Number(req.params.id) || 0;
      if (!(await can(u, 'create', 'portraits', { poetId: id }))) return reply.code(403).send({ error: 'اجازت نہیں' });
      const p = (await pool.query('SELECT id, nickname FROM poets WHERE id = $1', [id])).rows[0];
      if (!p) return reply.code(404).send({ error: 'شاعر نہیں ملا' });
      const clean = (v?: string, max = 300) => String(v ?? '').normalize('NFC').trim().slice(0, max);
      const licence = clean(req.query.licence, 100), source = clean(req.query.source), artist = clean(req.query.artist, 100) || null;
      if (!licence || !source) return reply.code(400).send({ error: 'ماخذ اور اجازت (لائسنس) لکھیں' });
      // the picture arrives as a stream (the octet-stream parser in ebooks.ts): read it, at most 2 MB
      const chunks: Buffer[] = []; let size = 0;
      for await (const c of req.body as AsyncIterable<Buffer>) { size += c.length; if (size > 2 * 1024 * 1024) return reply.code(413).send({ error: 'تصویر 2 MB سے بڑی ہے' }); chunks.push(c); }
      const img = Buffer.concat(chunks), ext = imageOf(img);
      if (!ext) return reply.code(400).send({ error: 'تصویر (jpg، png یا webp) چاہیے' });
      const name = `${createHash('sha256').update(img).digest('hex')}.${ext}`;
      await mkdir(dirname(pathOf(name)), { recursive: true });
      await writeFile(pathOf(name), img);
      const focus = faceFocus([pathOf(name)])[pathOf(name)] ?? null;
      const credit = { artist, licence, licence_url: null, page: /^https?:\/\//.test(source) ? source : null, source, from: 'upload', by: Number(u.id),
        focus, ...(u.role === 'admin' ? { reviewed: true } : {}) };
      await pool.query('UPDATE poets SET portrait = $1, portrait_credit = $2 WHERE id = $3', [name, credit, id]);
      await audit(u, 'portrait-upload', null, { poet: p.nickname, licence, source });
      return { ok: true, waiting: u.role !== 'admin' };
    });
}

// where the face is, so the round avatar zooms onto it (tools/face_focus.py, OpenCV): {path: {tx, ty, z} | null}; nothing
// when Python or OpenCV is missing (the default framing then, and admins can frame by hand)
function faceFocus(paths: string[]): Record<string, { x: number; y: number; z: number } | null> {
  if (!paths.length) return {};
  const tool = fileURLToPath(new URL('../tools/face_focus.py', import.meta.url));
  const r = spawnSync(process.env.DIVAN_PYTHON ?? 'python3', [tool, ...paths], { encoding: 'utf8', maxBuffer: 1 << 24 });
  try { return r.status === 0 ? JSON.parse(r.stdout) : {}; } catch { return {}; }
}

const UA = 'DivanBot/1.0 (https://divan.anasrashid.net; poet portraits from Wikimedia)';
const query = async (host: string, params: Record<string, string>) => {
  const url = `https://${host}/w/api.php?` + new URLSearchParams({ action: 'query', format: 'json', formatversion: '2', ...params });
  const r = await fetch(url, { headers: { 'user-agent': UA } });
  if (!r.ok) throw new Error(`${host}: ${r.status}`);
  return (await r.json()).query;
};
// a Wikipedia lead image is not always a portrait: these file names are not (a signature, a tomb, a shrine, a map, a book…)
const NOT_A_FACE = /autograph|signature|dastkhat|sign\b|tomb|grave|mazar|maqbara|dargah|shrine|mausoleum|mosque|masjid|map\b|flag|cover|book|title|logo|coin|stamp|calligraph|manuscript|poster|plaque|house|haveli|building/i;
const text = (html?: string) => (html ?? '').replace(/<[^>]+>/g, '').replace(/\s+/g, ' ').trim();

// the artist for the credit line: Commons often repeats the name (a link and its label) or writes "unknown author" with
// notes; an unknown or anonymous artist is left out, a long note shortened
const artistOf = (html?: string) => {
  let a = text(html);
  const half = a.length / 2;
  if (Number.isInteger(half) && a.slice(0, half) === a.slice(half)) a = a.slice(0, half);
  if (!a || /^(unknown|anonymous|unidentified)/i.test(a)) return null;
  return a.length > 60 ? a.slice(0, 58).trimEnd() + '…' : a;
};

// the lead image of an Urdu Wikipedia article, if Wikipedia marks it free
async function leadImage(title: string) {
  const q = await query('ur.wikipedia.org', { prop: 'pageimages', titles: title, piprop: 'name', pilicense: 'free', redirects: '1' });
  return q?.pages?.[0]?.pageimage as string | undefined;
}

// a file's 320px copy and its credit, from Commons (or Urdu Wikipedia for a local file); null when it is not free
async function fileInfo(file: string) {
  for (const host of ['commons.wikimedia.org', 'ur.wikipedia.org']) {
    const page = (await query(host, { titles: `File:${file}`, prop: 'imageinfo', iiprop: 'url|extmetadata', iiurlwidth: '320' }))?.pages?.[0];
    const info = page?.imageinfo?.[0];
    if (!info) continue;
    const m = info.extmetadata ?? {};
    if (m.NonFree?.value === 'true' || !m.LicenseShortName?.value) return null; // fair use or unknown: not ours to show
    return {
      thumb: info.thumburl ?? info.url as string,
      credit: { artist: artistOf(m.Artist?.value), licence: text(m.LicenseShortName.value), licence_url: m.LicenseUrl?.value ?? null,
        page: info.descriptionurl as string, source: file },
    };
  }
  return null;
}

async function run(force: boolean, focusAll = false) {
  const dataDir = process.env.DIVAN_DATA_DIR ?? fileURLToPath(new URL('../../../divan-data', import.meta.url));
  // each poet's Urdu Wikipedia article, from divan-data's poet list (matched by name)
  const wiki = new Map<string, string>(), named = new Map<string, string>();
  try {
    for (const line of (await readFile(`${dataDir}/export/poets.jsonl`, 'utf8')).split('\n').filter(Boolean)) {
      const p = JSON.parse(line); if (p.wikipedia) wiki.set(p.name, p.wikipedia); if (p.image) named.set(p.name, p.image);
    }
  } catch { console.log(`no ${dataDir}/export/poets.jsonl: pictures named by divan-data only`); }
  // a poet whose portrait an admin removed (portrait_credit.removed) is left alone
  const { rows } = await pool.query(`SELECT id, name, nickname, image_url, portrait, portrait_credit FROM poets WHERE portrait_credit->>'removed' IS NULL
    AND coalesce(portrait_credit->>'from', '') <> 'upload' ORDER BY id`);
  let got = 0, kept = 0, none = 0;
  for (const p of rows) {
    try {
      const fromUrl = p.image_url ? decodeURIComponent(p.image_url.split('/Special:FilePath/')[1] ?? '') : '';
      const given = (fromUrl || named.get(p.name) || named.get(p.nickname) || '').replace(/_/g, ' ');
      const article = wiki.get(p.name) ?? wiki.get(p.nickname);
      const lead = !given && article ? await leadImage(article) : undefined;
      const file = given || (lead && !NOT_A_FACE.test(lead) ? lead : undefined);
      if (!file) { if (p.portrait && force) await pool.query('UPDATE poets SET portrait = NULL, portrait_credit = NULL WHERE id = $1', [p.id]); none++; continue; }
      if (!force && p.portrait && p.portrait_credit?.source === file) { kept++; continue; } // already have this picture
      const info = await fileInfo(file);
      if (!info) { none++; continue; }
      const res = await fetch(info.thumb, { headers: { 'user-agent': UA } });
      const img = Buffer.from(await res.arrayBuffer()), ext = res.ok && imageOf(img);
      if (!ext) { none++; continue; }
      const name = `${createHash('sha256').update(img).digest('hex')}.${ext}`;
      await mkdir(dirname(pathOf(name)), { recursive: true });
      await writeFile(pathOf(name), img);
      // from: 'wikisource' (the picture chosen for the author's page) or 'wikipedia' (the article's lead image: worth a look)
      const same = p.portrait_credit?.source === file, keep = same && p.portrait_credit?.focus_manual; // a hand framing stays
      const focus = keep ? p.portrait_credit.focus : faceFocus([pathOf(name)])[pathOf(name)] ?? null;
      await pool.query('UPDATE poets SET portrait = $1, portrait_credit = $2 WHERE id = $3', [name, { ...info.credit, from: given ? 'wikisource' : 'wikipedia',
        ...(same && p.portrait_credit?.reviewed ? { reviewed: true } : {}), focus, ...(keep ? { focus_manual: true } : {}) }, p.id]);
      got++; console.log(`${p.nickname}: ${file} (${info.credit.licence}${given ? '' : ', Wikipedia lead image'})`);
      await new Promise((r) => setTimeout(r, 200)); // gently: Wikimedia asks bots to go slowly
    } catch (e) { console.log(`${p.nickname}: ${(e as Error).message}`); }
  }
  console.log(`portraits: ${got} new, ${kept} unchanged, ${none} without a free picture`);
  if (focusAll) { // frame the stored portraits on their faces again (not the hand-framed ones)
    const { rows: ps } = await pool.query(`SELECT id, portrait FROM poets WHERE portrait IS NOT NULL AND (portrait_credit->>'focus_manual') IS NULL`);
    const found = faceFocus(ps.map((p) => pathOf(p.portrait)));
    for (const p of ps) await pool.query(`UPDATE poets SET portrait_credit = portrait_credit || $2 WHERE id = $1`, [p.id, { focus: found[pathOf(p.portrait)] ?? null }]);
    console.log(`framed: ${Object.values(found).filter(Boolean).length} of ${ps.length} on a face`);
  }
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
  await run(process.argv.includes('--force'), process.argv.includes('--focus'));
  await pool.end();
}
