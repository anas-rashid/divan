// Word dictionary for the reading sidebar: the full Wiktionary data for Urdu, Persian and Arabic, in PostgreSQL.
//   source 'en':  en.wiktionary entries in full (all senses with usage examples, every IPA with dialect labels, every
//                 recording, transliteration, vowelled form, etymology, synonyms, derived and related words;
//                 not inflection tables),
//                 from the kaikki.org Wiktextract extracts (weekly)
//   source 'own': each language's own Wiktionary (ur., fa., ar.wiktionary): meanings in that language,
//                 from the Wikimedia dumps (twice a month) plus recent changes (daily)
// Urdu equivalents for words with no Urdu meaning are pivoted through English: ur_glosses maps English words to
// Urdu words, from the English glosses of en.wiktionary's Urdu entries and ur.wiktionary's English entries. Data: CC BY-SA, Wiktionary contributors.
import { spawn } from 'node:child_process';
import { createInterface } from 'node:readline';
import { Readable } from 'node:stream';
import { pool } from './db.ts';

export const LANGS = ['ur', 'fa', 'ar'] as const;
const NAME = { ur: 'Urdu', fa: 'Persian', ar: 'Arabic' } as const;
const UA = 'Divan/2 (https://github.com/anas-rashid/divan)';
const KAIKKI = (l: string) => `https://kaikki.org/dictionary/${NAME[l as 'ur']}/kaikki.org-dictionary-${NAME[l as 'ur']}.jsonl`;
const DUMP = (l: string) => `https://dumps.wikimedia.org/${l}wiktionary/latest/${l}wiktionary-latest-pages-articles.xml.bz2`;
const MARKS = /[ؐ-ًؚ-ٰٟۖ-ۭـ‌‍‎‏]/g;

// punctuation, dashes and spaces: dropped from words (بے ثبوت = بےثبوت, دل، = دل)
export const PUNCT = /[\s\-‐-―_.,،۔؛؟!?:;"'«»()[\]{}/\\*]+/g;

// spelling-insensitive key shared by Urdu, Persian and Arabic: no diacritics/tatweel/punctuation/spaces, one heh,
// one yeh, one kaf, plain alef, noon ghunna as noon (ناداں = نادان, قسمت = قِسْمَت, نگاہ = نگاه, معنی = معنى).
// ے and ھ stay distinct.
export const key = (w: string) =>
  w.normalize('NFC').replace(MARKS, '').replace(PUNCT, '')
    .replace(/[ہۂۃةه]/g, 'ه').replace(/[يىی]/g, 'ی')
    .replace(/ك/g, 'ک').replace(/[أإٱ]/g, 'ا').replace(/ں/g, 'ن');

const unlink = (wt: string) => wt.replace(/\[\[(?:[^\]|]*\|)?([^\]]*)\]\]/g, '$1').replace(/'''?/g, '').trim();
const upload = (u?: string) => (u && u.startsWith('https://upload.wikimedia.org/') ? u : null);

// ---- parsing ----

// a kaikki.org (Wiktextract) line -> the fields the sidebar shows
export function kaikkiEntry(d: any) {
  const senses = (d.senses ?? []).filter((s: any) => s.glosses?.length);
  const sounds = d.sounds ?? [];
  const words = (list: any[] | undefined) => [...new Set((list ?? []).map((x: any) => x.word).filter(Boolean))] as string[];
  const ipa = new Map<string, string[]>();
  for (const s of sounds) if (s.ipa && !ipa.has(s.ipa)) ipa.set(s.ipa, s.tags ?? []);
  return {
    pos: d.pos as string,
    formOf: senses.length > 0 && senses.every((s: any) => s.form_of || s.tags?.includes('form-of')),
    tr: d.forms?.find((f: any) => f.tags?.includes('romanization'))?.form ?? null,
    form: d.forms?.find((f: any) => f.tags?.includes('canonical'))?.form ?? null, // with short vowels: مُلْک
    // every sense, with its labels (archaic, figurative …) and usage examples
    senses: senses.map((s: any) => ({
      gloss: s.glosses.join('; ') as string,
      tags: (s.tags ?? []).filter((t: string) => t !== 'form-of') as string[],
      examples: (s.examples ?? []).filter((e: any) => e.text).map((e: any) => ({ text: e.text, roman: e.roman ?? null, english: e.english ?? e.translation ?? null })),
    })),
    // every pronunciation with its dialect labels (Iran, Classical Persian …) and every recording
    ipa: [...ipa].map(([v, tags]) => ({ ipa: v, tags })),
    audio: sounds.map((s: any) => ({ url: upload(s.mp3_url) ?? upload(s.ogg_url), tags: s.tags ?? [] })).filter((a: any) => a.url),
    ety: d.etymology_text ? etymology(String(d.etymology_text)) : null,
    synonyms: words(d.synonyms), derived: words(d.derived), related: words(d.related),
  };
}

// etymology text without Wiktionary's "Etymology tree …" diagram summary (no half surrogate pairs)
const etymology = (t: string) =>
  (t.startsWith('Etymology tree') ? t.slice(Math.max(0, t.search(/\b(Borrowed|Inherited|From|Learned|Derived|Semi-learned|Calque|Compound)\b/))) : t)
    .toWellFormed();

// English glosses of an Urdu entry as pivot keys: "fate, destiny" -> fate, destiny (short, lowercase)
export const glossKeys = (glosses: string[]) => [...new Set(glosses.flatMap((g) => g.replace(/\([^)]*\)/g, '').split(/[,;]/))
  .map((s) => s.trim().toLowerCase().replace(/^(to|a|an|the) /, '')).filter((s) => /^[a-z][a-z' -]{1,30}$/.test(s) && s.split(' ').length <= 3 && !FILLER.has(s)))];
const FILLER = new Set(['especially', 'usually', 'often', 'also', 'etc', 'something', 'someone', 'chiefly', 'mainly', 'figuratively', 'literally', 'rare', 'archaic', 'obsolete', 'dated', 'poetic', 'colloquial', 'informal', 'formal', 'slang', 'by extension']);

// ur.wiktionary Urdu entries: numbered lines under ==معانی==, else "# " lines, else the opening prose;
// origin from "(عربی)" on the first line
export function urduEntry(wt: string) {
  const m = wt.split(/==\s*معانی\s*==/)[1]?.split(/\n==/)[0] ?? '';
  let defs = m.split('\n').map((l) => l.match(/^\s*(?:\d+[.)-]|#)\s*(.+)/)?.[1]).filter(Boolean).map((l) => unlink(l!));
  if (!defs.length) defs = definitions(wt, 'ur').defs;
  if (!defs.length) {
    const prose = wt.split('\n').find((l) => l.trim().startsWith("'''") && l.length > 20);
    if (prose) defs = [unlink(stripTemplates(prose)).slice(0, 300)];
  }
  const origin = unlink(wt.match(/\((\[\[[^\]]+\]\])\)/)?.[1] ?? '') || null;
  const english = wt.match(/^\s*انگریزی\s*:\s*([A-Za-z].+)$/m)?.[1].trim() ?? null; // "== تراجم ==" pages
  return { defs: defs.slice(0, 20), origin, links: [] as string[], ...(english && { english }) };
}

// ur.wiktionary English entries ("north": "# [[شمالی]]۔"): the Urdu words, for the pivot through English
export const isEnglish = (title: string) => /^[A-Za-z][A-Za-z' -]*$/.test(title);
export function englishToUrdu(wt: string) {
  const lines = wt.split('\n').filter((l) => /^#(?![:*])/.test(l)).join(' ');
  return [...new Set([...lines.matchAll(/\[\[(?:[^\]|]*\|)?([^\]]+)\]\]/g)].map((m) => m[1].trim())
    .filter((w) => /^[\p{Script=Arabic}\s]+$/u.test(w)))].slice(0, 10);
}
const stripTemplates = (t: string) => {
  while (/\{\{[^{}]*\}\}/.test(t)) t = t.replace(/\{\{[^{}]*\}\}/g, '');
  return t;
};

// fa./ar.wiktionary: "# definition" lines outside etymology/translation sections; on ar.wiktionary only the
// Arabic section, and undiacritised pages that just point to entries ("* [[عِشْق]]") give those links
export function definitions(wt: string, code: string) {
  if (code === 'ar' && wt.includes('{{اللغة|')) wt = wt.split(/==\s*\{\{اللغة\|/).find((x) => x.startsWith('عربية')) ?? '';
  const defs: string[] = [], links: string[] = [];
  let skip = false;
  for (const line of wt.split('\n')) {
    const h = line.match(/^=+\s*(.*?)\s*=+\s*$/);
    if (h) { skip = /ریشه|ترجم|برگردان|تصريف|تصریف|منابع|مشتق|نفس الجذر/.test(h[1]); continue; }
    if (skip) continue;
    const only = line.match(/^[#*]\s*'*\[\[([^\]|]+)\]\]'*\s*(?:\([^)]*\))?\.?\s*$/);
    if (only) { links.push(only[1]); continue; }
    const m = line.match(/^#(?![:*])\s*(.+)/);
    if (!m) continue;
    const t = unlink(stripTemplates(m[1])).replace(/^[\s.،:-]+|\s+$/g, '');
    if (t.length >= 3 && !/^-+$/.test(t)) defs.push(t);
  }
  return { defs: defs.slice(0, 20), origin: null as string | null, links: links.slice(0, 5) };
}

export const ownEntry = (code: string, wt: string) => (code === 'ur' ? urduEntry(wt) : definitions(wt, code));

// pages of a MediaWiki XML dump (articles only, no redirects)
export function* dumpPages(xml: string) {
  for (const m of xml.matchAll(/<page>([\s\S]*?)<\/page>/g)) {
    const p = m[1];
    if (!/<ns>0<\/ns>/.test(p) || /<redirect /.test(p)) continue;
    const title = p.match(/<title>([^<]*)<\/title>/)?.[1];
    const text = p.match(/<text[^>]*>([\s\S]*?)<\/text>/)?.[1];
    if (title && text) yield { title: xmlText(title), text: xmlText(text) };
  }
}
const xmlText = (s: string) => s.replace(/&lt;/g, '<').replace(/&gt;/g, '>').replace(/&quot;/g, '"').replace(/&#039;/g, "'").replace(/&amp;/g, '&');

// ---- import and sync ----

const meta = async (k: string) => (await pool.query('SELECT value FROM dict_meta WHERE name = $1', [k])).rows[0]?.value ?? null;
const setMeta = (k: string, v: string) =>
  pool.query('INSERT INTO dict_meta (name, value) VALUES ($1, $2) ON CONFLICT (name) DO UPDATE SET value = $2', [k, v]);

async function insertRows(client: any, rows: unknown[][]) {
  for (let i = 0; i < rows.length; i += 500) {
    const chunk = rows.slice(i, i + 500), values: unknown[] = [];
    const sql = chunk.map((r, j) => { values.push(...r); const o = j * 5; return `($${o + 1},$${o + 2},$${o + 3},$${o + 4},$${o + 5})`; });
    await client.query(`INSERT INTO wiktionary (lang, source, title, key, data) VALUES ${sql.join(',')}`, values);
  }
}

// replace one source's rows in a transaction (readers see the old data until it commits)
async function replace(lang: string, source: string, fill: (add: (title: string, data: object) => Promise<void>, client: any) => Promise<void>) {
  const client = await pool.connect();
  let n = 0;
  try {
    await client.query('BEGIN');
    await client.query('DELETE FROM wiktionary WHERE lang = $1 AND source = $2', [lang, source]);
    if (lang === 'ur') await client.query('DELETE FROM ur_glosses WHERE source = $1', [source]);
    let batch: unknown[][] = [];
    await fill(async (title, data) => {
      batch.push([lang, source, title, key(title), data]); n++;
      if (batch.length >= 2000) { await insertRows(client, batch); batch = []; }
    }, client);
    await insertRows(client, batch);
    await client.query('COMMIT');
  } catch (e) {
    await client.query('ROLLBACK');
    throw e;
  } finally {
    client.release();
  }
  return n;
}

async function lastModified(url: string) {
  const res = await fetch(url, { method: 'HEAD', headers: { 'user-agent': UA } });
  if (!res.ok) throw new Error(`${res.status} ${url}`);
  return res.headers.get('last-modified') ?? '';
}

async function importKaikki(lang: string) {
  const res = await fetch(KAIKKI(lang), { headers: { 'user-agent': UA } });
  if (!res.ok || !res.body) throw new Error(`${res.status} ${KAIKKI(lang)}`);
  return replace(lang, 'en', async (add, client) => {
    const glossRows: string[][] = [];
    for await (const line of createInterface({ input: Readable.fromWeb(res.body as any), crlfDelay: Infinity })) {
      if (!line) continue;
      const d = JSON.parse(line), e = kaikkiEntry(d);
      if (!e.senses.length && !e.ipa.length) continue;
      await add(d.word, e);
      if (lang === 'ur' && !e.formOf) for (const g of glossKeys(e.senses.slice(0, 3).map((s) => s.gloss))) glossRows.push([g, d.word]);
    }
    await insertGlosses(client, 'en', glossRows);
  });
}

async function insertGlosses(client: any, source: string, rows: string[][]) {
  for (let i = 0; i < rows.length; i += 1000) {
    const chunk = rows.slice(i, i + 1000);
    await client.query(`INSERT INTO ur_glosses (gloss, word, source) VALUES ${chunk.map((_, j) => `($${j * 2 + 1},$${j * 2 + 2},'${source}')`).join(',')}`, chunk.flat());
  }
}

async function importDump(lang: string) {
  const bz = spawn('sh', ['-c', `curl -sfL -A '${UA}' '${DUMP(lang)}' | bzcat`]);
  return replace(lang, 'own', async (add, client) => {
    let buf = '';
    const glossRows: string[][] = []; // ur.wiktionary English entries -> the pivot table
    for await (const chunk of bz.stdout.setEncoding('utf8')) {
      buf += chunk;
      const end = buf.lastIndexOf('</page>');
      if (end < 0) continue;
      for (const p of dumpPages(buf.slice(0, end + 7))) {
        if (lang === 'ur' && isEnglish(p.title)) {
          for (const w of englishToUrdu(p.text)) glossRows.push([p.title.toLowerCase(), w]);
          continue;
        }
        const e = ownEntry(lang, p.text);
        if (e.defs.length || e.links.length || 'english' in e) await add(p.title, e);
      }
      buf = buf.slice(end + 7);
    }
    const code: number = await new Promise((r) => (bz.exitCode !== null ? r(bz.exitCode) : bz.on('close', r)));
    if (code !== 0) throw new Error(`dump download/decompress failed for ${lang} (exit ${code})`);
    await insertGlosses(client, 'own', glossRows);
  });
}

// changes on ur./fa./ar.wiktionary since the last run, re-read from the API (titles in batches of 50)
async function recentChanges(lang: string) {
  const api = `https://${lang}.wiktionary.org/w/api.php`;
  const since = await meta(`rc:${lang}`), now = new Date().toISOString();
  if (!since) return setMeta(`rc:${lang}`, now).then(() => 0); // first run: the dump is the baseline
  const titles = new Set<string>();
  let cont: Record<string, string> = {};
  do {
    const q = new URLSearchParams({ action: 'query', list: 'recentchanges', rcnamespace: '0', rctype: 'edit|new', rcprop: 'title',
      rclimit: '500', rcdir: 'newer', rcstart: since, rcend: now, format: 'json', formatversion: '2', ...cont });
    const d: any = await (await fetch(`${api}?${q}`, { headers: { 'user-agent': UA } })).json();
    for (const c of d.query?.recentchanges ?? []) titles.add(c.title);
    cont = d.continue ?? {};
  } while (cont.rccontinue);
  const list = [...titles];
  for (let i = 0; i < list.length; i += 50) {
    const q = new URLSearchParams({ action: 'query', prop: 'revisions', rvprop: 'content', rvslots: 'main',
      titles: list.slice(i, i + 50).join('|'), format: 'json', formatversion: '2' });
    const d: any = await (await fetch(api, { method: 'POST', body: q, headers: { 'user-agent': UA } })).json();
    for (const p of d.query?.pages ?? []) {
      const wt = p.revisions?.[0]?.slots?.main?.content;
      if (lang === 'ur' && isEnglish(p.title)) {
        const g = p.title.toLowerCase();
        await pool.query(`DELETE FROM ur_glosses WHERE source = 'own' AND gloss = $1`, [g]);
        for (const w of wt ? englishToUrdu(wt) : []) await pool.query(`INSERT INTO ur_glosses (gloss, word, source) VALUES ($1, $2, 'own')`, [g, w]);
        continue;
      }
      await pool.query(`DELETE FROM wiktionary WHERE lang = $1 AND source = 'own' AND title = $2`, [lang, p.title]);
      const e = wt && !/^#(REDIRECT|تحويل|تغییر)/i.test(wt) ? ownEntry(lang, wt) : null;
      if (e && (e.defs.length || e.links.length || 'english' in e))
        await pool.query(`INSERT INTO wiktionary (lang, source, title, key, data) VALUES ($1, 'own', $2, $3, $4)`, [lang, p.title, key(p.title), e]);
    }
  }
  await setMeta(`rc:${lang}`, now);
  return list.length;
}

// full re-import of each source whose upstream file changed, then the daily recent changes
export async function sync(log = console.log) {
  for (const lang of LANGS) {
    for (const [source, url, run] of [['en', KAIKKI(lang), importKaikki], ['own', DUMP(lang), importDump]] as const) {
      const lm = await lastModified(url), k = `file:${lang}:${source}`;
      if (lm && lm === (await meta(k))) continue;
      const t = Date.now(), n = await run(lang);
      await setMeta(k, lm);
      if (source === 'own') await setMeta(`rc:${lang}`, new Date(lm).toISOString()); // changes after the dump
      log(`${lang} ${source}: ${n} entries (${Math.round((Date.now() - t) / 1000)} s)`);
    }
    log(`${lang} recent changes: ${await recentChanges(lang)} pages`);
  }
}

// ---- lookup ----

const page = (lang: string, source: string, title: string) =>
  source === 'en' ? `https://en.wiktionary.org/wiki/${encodeURIComponent(title)}#${NAME[lang as 'ur']}` : `https://${lang}.wiktionary.org/wiki/${encodeURIComponent(title)}`;

export async function lookup(word: string) {
  const k = key(word);
  const { rows } = await pool.query('SELECT lang, source, title, data FROM wiktionary WHERE key = $1 ORDER BY id', [k]);
  // ar.wiktionary pointer pages: follow to the diacritised entries
  const pointers = rows.filter((r) => r.source === 'own' && !r.data.defs.length).flatMap((r) => r.data.links.map((l: string) => [r.lang, l]));
  if (pointers.length) {
    const more = await pool.query(
      `SELECT lang, source, title, data FROM wiktionary WHERE source = 'own' AND (lang, title) IN (SELECT * FROM unnest($1::text[], $2::text[]))`,
      [pointers.map((p) => p[0]), pointers.map((p) => p[1])]);
    rows.push(...more.rows);
  }
  const langs = LANGS.map((code) => {
    const en = rows.filter((r) => r.lang === code && r.source === 'en');
    const own = rows.filter((r) => r.lang === code && r.source === 'own' && r.data.defs.length);
    const lemmas = en.filter((r) => !r.data.formOf), use = lemmas.length ? lemmas : en;
    // readings: one spelling, different (unwritten) short vowels, e.g. ملک = مُلْک mulk, مَلِک malik, مِلْک milk
    const readings: any[] = [];
    for (const r of use) {
      const id = r.data.tr ?? r.data.form ?? r.title;
      let g = readings.find((x) => x.id === id);
      if (!g) readings.push((g = { id, form: r.data.form ?? r.title, tr: r.data.tr, ipa: [], audio: [], senses: [] }));
      for (const x of r.data.ipa) if (!g.ipa.some((y: any) => y.ipa === x.ipa)) g.ipa.push(x);
      for (const x of r.data.audio) if (!g.audio.some((y: any) => y.url === x.url)) g.audio.push(x);
      if (r.data.senses.length) g.senses.push({ pos: r.data.pos, defs: r.data.senses });
    }
    return {
      code, readings: readings.map(({ id, ...x }) => x),
      etymologies: [...new Set(use.map((r) => r.data.ety).filter(Boolean))],
      synonyms: [...new Set(use.flatMap((r) => r.data.synonyms))],
      derived: [...new Set(use.flatMap((r) => r.data.derived))],
      related: [...new Set(use.flatMap((r) => r.data.related))],
      meanings: own.flatMap((r) => r.data.defs), origin: own.find((r) => r.data.origin)?.data.origin ?? null,
      source: own[0] ? page(code, 'own', own[0].title) : null,
      // ur.wiktionary's English translation lines ("انگریزی : …")
      translation: rows.filter((r) => r.lang === code && r.source === 'own' && r.data.english).map((r) => r.data.english),
      en: en[0] ? page(code, 'en', en[0].title) : null,
    };
  }).filter((l) => l.meanings.length || l.readings.length || l.translation.length);
  // Urdu translations through English (the primary English meaning's Urdu words): for the Urdu entry when it
  // has no Urdu-language meaning, and for the Persian and Arabic entries when the word is not in Urdu at all
  const inUrdu = langs.some((l) => l.code === 'ur');
  await Promise.all(langs.map(async (l: any) => {
    l.urdu = (l.code === 'ur' ? !l.meanings.length : !inUrdu) ? await viaEnglish(l.readings[0]?.senses[0]?.defs.slice(0, 2).map((d: any) => d.gloss) ?? [], k) : [];
  }));
  const found = langs.length > 0;
  // always: the same consonants with other long vowels (دل: دال، دول، دیل); not found: also stems and near spellings
  const [variants, near] = await Promise.all([vowelVariants(k), found ? [] : similar(k)]);
  return { word, langs, found, variants, similar: near.filter((n) => !variants.some((v) => v.title === n.title)) };
}

async function viaEnglish(glosses: string[], k: string) {
  const g = glossKeys(glosses).slice(0, 4);
  if (!g.length) return [];
  const { rows } = await pool.query('SELECT gloss, word FROM ur_glosses WHERE gloss = ANY($1)', [g]);
  const seen = new Set([k]);
  return g.map((gloss) => ({
    gloss, urdu: rows.filter((r) => r.gloss === gloss && !seen.has(key(r.word)) && seen.add(key(r.word))).map((r) => r.word).slice(0, 8),
  })).filter((e) => e.urdu.length);
}

// ---- not found: similar words ----

// Urdu inflection endings (in key form) and the base forms to try: آنکھوں -> آنکھ, دیوانے -> دیوانه, جاتے -> جانا
const ENDINGS = ['یاں', 'ئیں', 'ؤں', 'وں', 'یں', 'گی', 'گا', 'گے', 'تا', 'تے', 'تی', 'نا', 'نے', 'نی', 'ے', 'ی', 'ا', 'و', 'ه'].map(key); // in key form (ں -> ن)
export function stems(k: string) {
  const out = new Set<string>();
  for (const e of ENDINGS) {
    if (!k.endsWith(e) || k.length - e.length < 2) continue;
    const base = k.slice(0, -e.length);
    for (const s of [base, base + 'ه', base + 'ا', base + 'نا']) if (s !== k) out.add(s);
  }
  return [...out];
}

// consonant skeleton as a regex: long vowels, heh and hamza optional (spellings add or drop them)
const SOFT = 'اوی\u0647ےءئ';
const esc = (c: string) => c.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
export function skeleton(k: string) {
  const cs = [...k].filter((c) => !SOFT.includes(c));
  if (cs.length < 2) return null;
  const v = `[${SOFT}]*`;
  return `^${v}${cs.map(esc).join(v)}${v}$`;
}

// shown titles without punctuation, dashes, spaces, tatweel or zero-width joiners
export const clean = (t: string) => t.replace(PUNCT, '').replace(/[\u0640\u200C\u200D]/g, '');

async function vowelVariants(k: string) {
  const sk = skeleton(k);
  if (!sk) return [];
  const { rows } = await pool.query(
    `SELECT key, (array_agg(title ORDER BY lang <> 'ur', lang <> 'fa', title))[1] AS title, array_agg(DISTINCT lang) AS langs
     FROM wiktionary WHERE key ~ $1 AND key <> $2 GROUP BY key ORDER BY bool_or(lang = 'ur') DESC, similarity(key, $2) DESC LIMIT 10`, [sk, k]);
  return rows.map((r) => ({ title: clean(r.title), langs: LANGS.filter((l) => r.langs.includes(l)) }));
}

async function similar(k: string) {
  const st = stems(k), sk = skeleton(k), pre = [k, ...st].filter((s) => s.length >= 3).sort((a, b) => b.length - a.length)[0];
  // one row per key: the Urdu spelling when there is one; Urdu entries first within each tier
  const pick = `SELECT key, (array_agg(title ORDER BY lang <> 'ur', lang <> 'fa', title))[1] AS title, array_agg(DISTINCT lang) AS langs FROM wiktionary`;
  const ur = `bool_or(lang = 'ur') DESC`;
  const [a, b, c, d] = await Promise.all([
    st.length ? pool.query(`${pick} WHERE key = ANY($1) GROUP BY key ORDER BY ${ur}`, [st]) : { rows: [] },
    sk ? pool.query(`${pick} WHERE key ~ $1 AND key <> $2 GROUP BY key ORDER BY ${ur}, similarity(key, $2) DESC LIMIT 8`, [sk, k]) : { rows: [] },
    pre ? pool.query(`${pick} WHERE key ~ $1 AND key <> $2 GROUP BY key ORDER BY ${ur}, length(key) LIMIT 8`, ['^' + [...pre].map(esc).join(''), k]) : { rows: [] },
    pool.query(`${pick} WHERE key % $1 AND key <> $1 GROUP BY key ORDER BY ${ur}, similarity(key, $1) DESC LIMIT 8`, [k]),
  ]);
  const seen = new Set<string>(), out: { title: string; langs: string[] }[] = [];
  for (const r of [...a.rows, ...b.rows, ...c.rows, ...d.rows])
    if (!seen.has(r.key) && seen.add(r.key)) out.push({ title: clean(r.title), langs: LANGS.filter((l) => r.langs.includes(l)) });
  return out.slice(0, 12);
}
