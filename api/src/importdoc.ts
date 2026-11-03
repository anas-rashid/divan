// Document import for the Urdu editor (#33): .docx, .odt, .txt and pasted text become Divan text, which the editor loads
// for the moderator to review before saving a draft. Each format is read into the same simple blocks (headings, and
// paragraphs with their line breaks); toDivanText then finds the structure:
//   heading                     == … == (Word's Heading 1 / Title, LibreOffice's outline level 1), === … === below it
//   a long line                 a prose paragraph
//   short lines                 verse; a run of them (until a blank line, heading or prose) is one unit:
//                               2 lines a شعر; runs that are all the same size above 2 (a musaddas's 6) are بند;
//                               one run with no blank lines in the document (a ghazal typed straight through) is split into
//                               شعر; a tab or two table cells between misras put a couplet on one line
//   footnotes                   Word's and LibreOffice's notes become <ref>…</ref>
// Arabic letter forms are made Urdu (ي→ی ك→ک ى→ی, Arabic digits →Urdu digits) and invisible marks removed. No dependency:
// .docx and .odt are zip archives, read with the platform's DecompressionStream (browsers and Node).
export type Block = { heading: number; text: string } | { lines: string[] };  // lines: a paragraph's lines; [] = an empty one

const PROSE = 80;  // characters: a line longer than this is prose (a misra is rarely longer than ~60)

const ARABIC = { 'ي': 'ی', 'ى': 'ی', 'ك': 'ک', '٠': '۰', '١': '۱', '٢': '۲', '٣': '۳', '٤': '۴', '٥': '۵', '٦': '۶', '٧': '۷', '٨': '۸', '٩': '۹' } as Record<string, string>;
export const clean = (s: string) => s.normalize('NFC').replace(/[يىك٠-٩]/g, (c) => ARABIC[c])
  .replace(/[​‎‏‪-‮⁦-⁩﻿­]/g, '').replace(/[   ]/g, ' ')
  .replace(/[ \t]+/g, (m) => (m.includes('\t') ? '\t' : ' ')).trim();

// ---- reading ----

export function fromText(text: string): Block[] {
  return text.replace(/\r\n?/g, '\n').split('\n').map((l) => ({ lines: clean(l) ? [clean(l)] : [] }));
}

// bytes of a .txt in any Unicode encoding (a BOM says which), or the old Windows Arabic/Urdu code page
export function decodeText(bytes: Uint8Array) {
  if (bytes[0] === 0xff && bytes[1] === 0xfe) return new TextDecoder('utf-16le').decode(bytes);
  if (bytes[0] === 0xfe && bytes[1] === 0xff) return new TextDecoder('utf-16be').decode(bytes);
  try { return new TextDecoder('utf-8', { fatal: true }).decode(bytes); } catch { return new TextDecoder('windows-1256').decode(bytes); }
}

const xmlText = (s: string) => s.replace(/&(lt|gt|quot|apos|amp|#x[0-9a-f]+|#\d+);/gi, (_, e: string) =>
  e[0] === '#' ? String.fromCodePoint(e[1].toLowerCase() === 'x' ? parseInt(e.slice(2), 16) : +e.slice(1)) : ({ lt: '<', gt: '>', quot: '"', apos: "'", amp: '&' } as any)[e.toLowerCase()]);

// a paragraph's text pieces -> its lines (a line break starts a new one; a tab is kept, toDivanText splits misras on it)
const linesOf = (s: string) => s.split('\n').map(clean).filter(Boolean);

export async function fromDocx(data: ArrayBuffer | Uint8Array): Promise<Block[]> {
  const files = await unzip(data, ['word/document.xml', 'word/footnotes.xml']);
  const notes = new Map<string, string>();
  for (const [, id, body] of (files['word/footnotes.xml'] ?? '').matchAll(/<w:footnote\b[^>]*w:id="(-?\d+)"[^>]*>([\s\S]*?)<\/w:footnote>/g))
    if (+id > 0) notes.set(id, clean([...body.matchAll(/<w:t\b[^>]*>([^<]*)<\/w:t>/g)].map((m) => xmlText(m[1])).join('')));
  const body = files['word/document.xml'] ?? '';
  const blocks: Block[] = [];
  for (const [p] of body.matchAll(/<w:p\b[^>]*?(?:\/>|>[\s\S]*?<\/w:p>)/g)) {
    const style = p.match(/<w:pStyle w:val="([^"]+)"/)?.[1] ?? '', outline = p.match(/<w:outlineLvl w:val="(\d)"/)?.[1];
    let text = '';
    for (const [tok, t, note] of p.matchAll(/<w:t\b[^>]*>([^<]*)<\/w:t>|<w:footnoteReference\b[^>]*w:id="(\d+)"[^>]*\/>|<w:(?:br|cr)\b(?![^>]*w:type="page")[^>]*\/>|<w:tab\/>/g))
      text += t !== undefined ? xmlText(t) : note ? `<ref>${notes.get(note) ?? ''}</ref>` : tok.startsWith('<w:tab') ? '\t' : '\n';
    const level = /^(Title|Heading ?1)$/i.test(style) || outline === '0' ? 2 : /^(Subtitle|Heading ?[2-9])$/i.test(style) || (outline && +outline > 0) ? 3 : 0;
    blocks.push(level && clean(text) ? { heading: level, text: clean(text.replace(/\n/g, ' ')) } : { lines: linesOf(text) });
  }
  return blocks;
}

export async function fromOdt(data: ArrayBuffer | Uint8Array): Promise<Block[]> {
  let xml = (await unzip(data, ['content.xml']))['content.xml'] ?? '';
  // notes first: their bodies hold paragraphs of their own
  xml = xml.replace(/<text:note\b[\s\S]*?<text:note-body>([\s\S]*?)<\/text:note-body>\s*<\/text:note>/g,
    (_, b: string) => `<ref>${clean(xmlText(b.replace(/<[^>]+>/g, ' ')))}</ref>`.replace(/</g, '\u0001').replace(/>/g, '\u0002'));
  const blocks: Block[] = [];
  for (const [, tag, attrs, inner = ''] of xml.matchAll(/<text:(h|p)\b([^>]*?)(?:\/>|>([\s\S]*?)<\/text:\1>)/g)) {
    const text = xmlText(inner.replace(/<text:line-break\/>/g, '\n').replace(/<text:tab\/>/g, '\t')
      .replace(/<text:s(?: text:c="(\d+)")?\/>/g, (_, n) => ' '.repeat(+(n ?? 1))).replace(/<[^>]+>/g, ''))
      .replace(/\u0001/g, '<').replace(/\u0002/g, '>');
    const level = tag === 'h' ? (+(attrs.match(/text:outline-level="(\d)"/)?.[1] ?? 1) > 1 ? 3 : 2) : 0;
    blocks.push(level && clean(text) ? { heading: level, text: clean(text.replace(/\n/g, ' ')) } : { lines: linesOf(text) });
  }
  return blocks;
}

// the named entries of a zip archive, as text (stored or deflated entries; enough for .docx and .odt)
async function unzip(data: ArrayBuffer | Uint8Array, names: string[]) {
  const b = data instanceof Uint8Array ? data : new Uint8Array(data), v = new DataView(b.buffer, b.byteOffset, b.byteLength);
  let end = b.length - 22;
  while (end >= 0 && v.getUint32(end, true) !== 0x06054b50) end--;
  if (end < 0) throw new Error('یہ فائل .docx یا .odt نہیں لگتی');
  const out: Record<string, string> = {};
  for (let at = v.getUint32(end + 16, true), n = v.getUint16(end + 10, true); n-- > 0;) {
    const method = v.getUint16(at + 10, true), size = v.getUint32(at + 20, true), nameLen = v.getUint16(at + 28, true);
    const extra = v.getUint16(at + 30, true), comment = v.getUint16(at + 32, true), local = v.getUint32(at + 42, true);
    const name = new TextDecoder().decode(b.subarray(at + 46, at + 46 + nameLen));
    at += 46 + nameLen + extra + comment;
    if (!names.includes(name)) continue;
    const start = local + 30 + v.getUint16(local + 26, true) + v.getUint16(local + 28, true), raw = b.subarray(start, start + size);
    const bytes = method === 0 ? raw : new Uint8Array(await new Response(new Blob([raw]).stream().pipeThrough(new DecompressionStream('deflate-raw'))).arrayBuffer());
    out[name] = new TextDecoder().decode(bytes);
  }
  return out;
}

// ---- structure ----

export function toDivanText(blocks: Block[], meta: Record<string, string> = {}) {
  // one item per line: a heading, a verse line, a prose paragraph, or a gap (an empty paragraph)
  type Item = { heading: number; text: string } | { verse: string[] } | { prose: string } | { gap: true };
  const items: Item[] = [];
  for (const b of blocks) {
    if ('heading' in b) { items.push(b); continue; }
    if (!b.lines.length) { items.push({ gap: true }); continue; }
    for (const l of b.lines) {
      const parts = l.split('\t').map((x) => x.trim()).filter(Boolean);
      if (parts.length === 2 && parts.every((x) => x.length <= PROSE)) items.push({ verse: parts });  // a couplet on one line
      else if (l.replace(/<ref>.*?<\/ref>/g, '').length > PROSE) items.push({ prose: l.replace(/\t/g, ' ') });
      else items.push({ verse: [l.replace(/\t/g, ' ')] });
    }
  }
  // runs of verse lines; whether the document separates units with blank lines at all
  const runs: string[][] = [], out: (string | string[][])[] = [];
  let cur: string[] | null = null, gaps = false;
  const close = () => { if (cur) { runs.push(cur); out.push([cur]); cur = null; } };
  for (const it of items) {
    if ('verse' in it) {
      if (it.verse.length === 2) { close(); runs.push(it.verse); out.push([it.verse]); continue; }  // a couplet on its own line
      (cur ??= []).push(it.verse[0]);
    } else {
      close();
      if ('gap' in it) gaps = true;
      else out.push('heading' in it ? `${'='.repeat(it.heading)} ${it.text} ${'='.repeat(it.heading)}` : it.prose);
    }
  }
  close();
  const longer = runs.filter((r) => r.length > 2).map((r) => r.length);
  const stanzas = gaps && longer.length > 1 && longer.every((n) => n === longer[0]);  // e.g. a musaddas: every بند 6 lines
  const units = (r: string[]) => r.length <= 2 || stanzas || r.length % 2 ? [r] : r.flatMap((_, i) => (i % 2 ? [] : [r.slice(i, i + 2)]));
  // verse runs next to each other share one <poem>
  const parts: string[] = [];
  for (const o of out) {
    if (typeof o === 'string') parts.push(o);
    else {
      const poem = o.flatMap(units).map((u) => u.join('\n')).join('\n\n');
      const last = parts.at(-1);
      if (last?.endsWith('</poem>')) parts[parts.length - 1] = last.slice(0, -'\n</poem>'.length) + '\n\n' + poem + '\n</poem>';
      else parts.push(`<poem>\n${poem}\n</poem>`);
    }
  }
  const head = Object.keys(meta).length ? `{{دیوان\n${Object.entries(meta).map(([k, v]) => `| ${k} = ${v}`).join('\n')}\n}}\n` : '';
  return head + parts.join('\n\n') + '\n';
}
