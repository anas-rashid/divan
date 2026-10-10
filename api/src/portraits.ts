// Poets' portraits: a freely licensed picture from Wikimedia for each poet who has one, kept in the file store and
// shown with the same duotone everywhere (web/src/components/PoetAvatar.astro); a generic avatar for the rest.
//   GET /api/poet/:id/portrait                 the stored picture (cached; the page adds ?v=<hash> to its link)
//   GET  /api/admin/portraits                 every portrait for review (unapproved lead images first)
//   POST /api/admin/portraits/:id {act}       approve | remove (a removed portrait is not fetched again)
//   npm run portraits [-- --force]             fetch or refresh them; the daily sync runs it (only changed pictures download)
// Where a picture comes from, in order: the file divan-data names (Wikisource's author page), else the lead image of the
// poet's Urdu Wikipedia article. Only pictures Wikimedia marks as free are taken, and the artist and licence are kept
// for the credit line under the portrait.
import { createHash } from 'node:crypto';
import { createReadStream } from 'node:fs';
import { mkdir, readFile, writeFile } from 'node:fs/promises';
import { dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import type { FastifyInstance } from 'fastify';
import { pool } from './db.ts';
import { pathOf, TYPES, imageOf } from './ebooks.ts';
import { requireAdmin, audit } from './admin.ts';

// the portrait the site shows: a Wikipedia lead image only once an admin approved it (it may be someone else, or not a face)
export const SHOWN = `CASE WHEN portrait_credit->>'from' = 'wikipedia' AND portrait_credit->>'reviewed' IS NULL THEN NULL ELSE portrait END`;

export function portraitRoutes(app: FastifyInstance) {
  app.get<{ Params: { id: string } }>('/api/poet/:id/portrait', async (req, reply) => {
    const p = (await pool.query('SELECT portrait FROM poets WHERE id = $1', [Number(req.params.id) || 0])).rows[0];
    if (!p?.portrait) return reply.code(404).send({ error: 'تصویر نہیں' });
    reply.header('content-type', TYPES[p.portrait.split('.').pop()]).header('cache-control', 'public, max-age=31536000, immutable');
    return reply.send(createReadStream(pathOf(p.portrait)));
  });

  app.get('/api/admin/portraits', async (req, reply) => {
    if (!(await requireAdmin(req, reply))) return;
    const { rows } = await pool.query(`SELECT id, url, nickname, portrait, portrait_credit AS credit FROM poets WHERE portrait IS NOT NULL
      ORDER BY (portrait_credit->>'from' = 'wikipedia' AND portrait_credit->>'reviewed' IS NULL) DESC, nickname`);
    return { portraits: rows };
  });
  app.post<{ Params: { id: string }; Body: { act?: string } }>('/api/admin/portraits/:id', async (req, reply) => {
    const admin = await requireAdmin(req, reply); if (!admin) return;
    const id = Number(req.params.id) || 0, act = req.body?.act;
    const p = (await pool.query('SELECT id, nickname, portrait_credit FROM poets WHERE id = $1 AND portrait IS NOT NULL', [id])).rows[0];
    if (!p) return reply.code(404).send({ error: 'تصویر نہیں ملی' });
    if (act === 'approve') await pool.query(`UPDATE poets SET portrait_credit = portrait_credit || '{"reviewed": true}' WHERE id = $1`, [id]);
    else if (act === 'remove') await pool.query(`UPDATE poets SET portrait = NULL, portrait_credit = $2 WHERE id = $1`, [id, { removed: true, source: p.portrait_credit?.source }]);
    else return reply.code(400).send({ error: 'نامعلوم عمل' });
    await audit(admin, act === 'approve' ? 'portrait-approve' : 'portrait-remove', null, { poet: p.nickname, file: p.portrait_credit?.source });
    return { ok: true };
  });
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

async function run(force: boolean) {
  const dataDir = process.env.DIVAN_DATA_DIR ?? fileURLToPath(new URL('../../../divan-data', import.meta.url));
  // each poet's Urdu Wikipedia article, from divan-data's poet list (matched by name)
  const wiki = new Map<string, string>(), named = new Map<string, string>();
  try {
    for (const line of (await readFile(`${dataDir}/export/poets.jsonl`, 'utf8')).split('\n').filter(Boolean)) {
      const p = JSON.parse(line); if (p.wikipedia) wiki.set(p.name, p.wikipedia); if (p.image) named.set(p.name, p.image);
    }
  } catch { console.log(`no ${dataDir}/export/poets.jsonl: pictures named by divan-data only`); }
  // a poet whose portrait an admin removed (portrait_credit.removed) is left alone
  const { rows } = await pool.query("SELECT id, name, nickname, image_url, portrait, portrait_credit FROM poets WHERE portrait_credit->>'removed' IS NULL ORDER BY id");
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
      await pool.query('UPDATE poets SET portrait = $1, portrait_credit = $2 WHERE id = $3', [name, { ...info.credit, from: given ? 'wikisource' : 'wikipedia', ...(p.portrait_credit?.source === file && p.portrait_credit?.reviewed ? { reviewed: true } : {}) }, p.id]);
      got++; console.log(`${p.nickname}: ${file} (${info.credit.licence}${given ? '' : ', Wikipedia lead image'})`);
      await new Promise((r) => setTimeout(r, 200)); // gently: Wikimedia asks bots to go slowly
    } catch (e) { console.log(`${p.nickname}: ${(e as Error).message}`); }
  }
  console.log(`portraits: ${got} new, ${kept} unchanged, ${none} without a free picture`);
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
  await run(process.argv.includes('--force'));
  await pool.end();
}
