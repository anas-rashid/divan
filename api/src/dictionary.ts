// Word lookup for the reading sidebar, from Wiktionary (CC BY-SA). For Urdu, Persian and Arabic, in that order:
//   - meanings in the language itself from its own Wiktionary (ur., fa., ar.wiktionary)
//   - English meanings and pronunciation (IPA, transliteration, audio) from en.wiktionary
// With no Urdu meanings, Urdu equivalents are pivoted through English (the English glosses' translation tables).
// Found words are cached in PostgreSQL (dictionary table) for 30 days.
import { pool } from './db.ts';

const UA = 'Divan/2 (https://github.com/anas-rashid/divan)';
const LANGS = { ur: 'Urdu', fa: 'Persian', ar: 'Arabic' } as const;
const DIAC = /[ً-ْٰٔٗ٘]/g;
const plain = (html: string) =>
  html.replace(/<[^>]+>/g, '').replace(/&nbsp;/g, ' ').replace(/&amp;/g, '&').replace(/&#\d+;/g, '').replace(/\s+/g, ' ').trim();
const unlink = (wt: string) => wt.replace(/\[\[(?:[^\]|]*\|)?([^\]]*)\]\]/g, '$1').replace(/'''?/g, '').trim();

async function get(url: string, json = true): Promise<any> {
  const res = await fetch(url, { headers: { 'user-agent': UA }, signal: AbortSignal.timeout(8000) }).catch(() => null);
  if (!res?.ok) return null;
  return json ? res.json() : res.text();
}
const rest = (path: string, title: string) => `https://en.wiktionary.org/api/rest_v1/page/${path}/${encodeURIComponent(title)}`;
const wikitext = async (host: string, title: string): Promise<string> => {
  const d = await get(`https://${host}/w/api.php?action=parse&page=${encodeURIComponent(title)}&prop=wikitext&format=json&formatversion=2&redirects=1`);
  return d?.parse?.wikitext ?? '';
};

// spelling forms to try: as written, without diacritics, final noon ghunna as noon (ناداں -> نادان)
export const forms = (w: string) => {
  const bare = w.replace(DIAC, '').replace(/ۂ/g, 'ہ');
  return [...new Set([w, bare, bare.replace(/ں$/, 'ن')])];
};

// the word in Persian / Arabic spelling (ہ -> ه, ی -> ي, ک -> ك …); Arabic also tries final alef maqsura
const SPELL: Record<string, [RegExp, string][]> = {
  fa: [[/[\u06C1\u06C2\u06BE]/g, '\u0647'], [/[\u06D2\u064A]/g, '\u06CC'], [/\u0643/g, '\u06A9']],
  ar: [[/[\u06C1\u06BE]/g, '\u0647'], [/\u06C2/g, '\u0629'], [/[\u06CC\u06D2]/g, '\u064A'], [/\u06A9/g, '\u0643']],
};
export function formsIn(code: string, w: string) {
  const base = forms(w);
  if (!SPELL[code]) return base;
  const conv = base.map((f) => SPELL[code].reduce((s, [re, to]) => s.replace(re, to), f));
  return [...new Set([...conv, ...(code === 'ar' ? conv.map((f) => f.replace(/\u064A$/, '\u0649')) : [])])];
}

// fa./ar.wiktionary: "# definition" lines outside etymology/translation sections; on ar.wiktionary only
// the Arabic section, and undiacritised pages that just point to entries ("* [[عِشْق]]") give those links
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
    let t = m[1];
    while (/\{\{[^{}]*\}\}/.test(t)) t = t.replace(/\{\{[^{}]*\}\}/g, '');
    t = unlink(t).replace(/^[\s.،:-]+|[\s]+$/g, '');
    if (t.length >= 3 && !/^-+$/.test(t)) defs.push(t);
  }
  return { defs: defs.slice(0, 5), links };
}

// meanings from the language's own Wiktionary: the first spelling with an entry (following one pointer hop)
async function native(code: string, word: string) {
  const host = `${code}.wiktionary.org`;
  for (const f of formsIn(code, word)) {
    const wt = await wikitext(host, f);
    if (!wt) continue;
    if (code === 'ur') {
      const e = urduEntry(wt);
      if (e.meanings.length) return { defs: e.meanings, origin: e.origin, url: `https://${host}/wiki/${encodeURIComponent(f)}` };
      continue;
    }
    let { defs, links } = definitions(wt, code), title = f;
    if (!defs.length && links[0]) ({ defs } = definitions(await wikitext(host, (title = links[0])), code));
    if (defs.length) return { defs, origin: null, url: `https://${host}/wiki/${encodeURIComponent(title)}` };
  }
  return null;
}

// ur.wiktionary: numbered lines under ==معانی==, origin from "(عربی)" on the first line
export function urduEntry(wt: string) {
  const m = wt.split(/==\s*معانی\s*==/)[1]?.split(/\n==/)[0] ?? '';
  const meanings = m.split('\n').map((l) => l.match(/^\s*(?:\d+[.)-]|#)\s*(.+)/)?.[1]).filter(Boolean).map((l) => unlink(l!)).slice(0, 6);
  const origin = unlink(wt.match(/\((\[\[[^\]]+\]\])\)/)?.[1] ?? '') || null;
  return { meanings, origin };
}

// en.wiktionary page HTML: per language, IPA, headword transliteration, audio
export function pronunciations(html: string) {
  const out: Record<string, { ipa: string[]; tr: string | null; audio: string | null }> = {};
  const parts = html.split(/<h2[^>]*id="([^"]+)"/);
  for (let i = 1; i < parts.length; i += 2) {
    const code = Object.entries(LANGS).find(([, n]) => n === parts[i])?.[0];
    if (!code) continue;
    const body = parts[i + 1];
    const ipa = [...new Set([...body.matchAll(/class="IPA[^"]*"[^>]*>([^<]+)/g)].map((m) => m[1]).filter((x) => /^[/[]/.test(x)))].slice(0, 2);
    const tr = body.match(new RegExp(`lang="${code}-Latn" class="headword-tr[^"]*"[^>]*>([^<]+)`))?.[1] ?? null;
    const a = body.match(/"(?:https:)?(\/\/upload\.wikimedia\.org\/[^"]+\.(?:ogg|oga|mp3|wav))"/)?.[1];
    out[code] = { ipa, tr: tr && plain(tr), audio: a ? 'https:' + a : null };
  }
  return out;
}

// Urdu equivalents of single-word English glosses, from their translation tables
async function pivot(word: string, glosses: string[]) {
  const seen = new Set([word.replace(DIAC, '')]); // the word itself, and duplicates differing only in diacritics
  const words = [...new Set(glosses.flatMap((g) => g.split(/[,;]/)).map((s) => s.trim()).filter((s) => /^[a-z]+$/.test(s)))].slice(0, 3); // lowercase: no proper nouns
  const found = await Promise.all(words.map(async (gloss) => {
    const wt = await wikitext('en.wiktionary.org', gloss);
    const lines = [...wt.matchAll(/^\*:? Urdu: (.*)$/gm)].map((m) => m[1]).join(' ');
    const urdu = [...lines.matchAll(/\{\{t\+?\|ur\|([^|}]+)/g)].map((m) => m[1].trim())
      .filter((u) => !seen.has(u.replace(DIAC, '')) && seen.add(u.replace(DIAC, ''))).slice(0, 8);
    return { gloss, urdu };
  }));
  return found.filter((f) => f.urdu.length);
}

async function fetchWord(word: string) {
  let title: string | null = null, defs: any = null;
  for (const f of forms(word)) if ((defs = await get(rest('definition', f)))) { title = f; break; }
  const [pron, ...own] = await Promise.all([
    title ? get(rest('html', title), false).then((h) => pronunciations(h ?? '')) : {},
    ...Object.keys(LANGS).map((c) => native(c, word)),
  ]) as [Record<string, any>, ...(Awaited<ReturnType<typeof native>>)[]];
  const langs = Object.keys(LANGS).map((code, i) => ({
    code, ...(pron[code] ?? { ipa: [], tr: null, audio: null }),
    meanings: own[i]?.defs ?? [], origin: own[i]?.origin ?? null, source: own[i]?.url ?? null,
    senses: (defs?.[code] ?? []).map((e: any) => ({
      pos: e.partOfSpeech, defs: e.definitions.map((d: any) => plain(d.definition)).filter(Boolean).slice(0, 4),
    })).filter((s: any) => s.defs.length).slice(0, 3),
  })).filter((l) => l.meanings.length || l.senses.length || l.ipa.length || l.tr);
  const glosses = langs.find((l) => l.senses.length)?.senses[0].defs.slice(0, 2) ?? []; // the primary meaning only
  const hasUrdu = langs.some((l) => l.code === 'ur' && l.meanings.length);
  return {
    word, langs,
    equivalents: hasUrdu ? [] : await pivot(word, glosses),
    sources: { en: title && `https://en.wiktionary.org/wiki/${encodeURIComponent(title)}` },
    found: langs.length > 0,
  };
}

export async function lookup(word: string) {
  const hit = await pool.query(`SELECT data FROM dictionary WHERE word = $1 AND fetched_at > now() - interval '30 days'`, [word]);
  if (hit.rows[0]) return hit.rows[0].data;
  const data = await fetchWord(word);
  if (!data.found) return data; // not found (or Wiktionary unreachable): don't cache
  await pool.query(
    `INSERT INTO dictionary (word, data) VALUES ($1, $2) ON CONFLICT (word) DO UPDATE SET data = $2, fetched_at = now()`,
    [word, data],
  );
  return data;
}
