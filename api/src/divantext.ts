// Divan text (research prototype, #38): the source format for content Divan edits and publishes.
// A small, Wikisource-compatible subset of wikitext, written as plain Urdu lines so git diffs show exactly
// which misra or paragraph changed. Parsed into blocks (what is a couplet, stanza, paragraph, chapter, word)
// and converted to the site's verse JSON and to TEI P5 for scholars.
//
//   {{دیوان | عنوان = … | شاعر = … | صنف = غزل | ردیف = … | ماخذ = …}}     work data points (one per line)
//   == باب ==                                                             chapter/section heading (prose)
//   <poem> … </poem>                                                       verse: units split by blank lines;
//                                                                          2 lines = شعر (couplet), 1 = مصرع, 3+ = بند
//   blank-line paragraphs outside <poem>                                   prose
//   {{مطلع}} {{حسن مطلع}} {{مقطع}} at the start of a unit                  overrides the computed labels
//   [[لفظ]]  [[لغت:دیوانہ|دیوانے]]                                         word linked to the dictionary (lemma|shown)
//   <ref>…</ref>                                                           footnote
//   {{نسخہ|متن|دوسرا متن|ماخذ=…}}                                           variant reading (first is shown)
// Words: split on spaces; a zero-width non-joiner keeps a compound one word (بے‌ثبوت); punctuation ، ۔ ؟ ! is
// not part of a word; the izafat kasra stays on its word.

export type Variant = { shown: string; others: string[]; source?: string };
// a line in order: plain text, a word linked to the dictionary, a footnote, a variant reading (for editors)
export type Segment = { text: string } | { text: string; lemma: string } | { note: string } | { variant: Variant };
export type Inline = { text: string; words: { shown: string; lemma: string }[]; notes: string[]; variants: Variant[]; segments: Segment[] };
export type Block =
  | { type: 'heading'; text: string; level: number } // == chapter == is level 2, === sub-heading === level 3, …
  | { type: 'para'; line: Inline }
  | { type: 'couplet' | 'line' | 'stanza'; lines: Inline[]; label?: string };
export type Doc = { meta: Record<string, string>; blocks: Block[] };

const LABELS = ['مطلع', 'حسن مطلع', 'مقطع'];

// {{name | a = b | …}} → fields (positional ones as 1, 2, …); nested templates and links kept as text
function template(src: string) {
  const body = src.slice(2, -2), parts: string[] = [];
  let depth = 0, cur = '';
  for (const ch of body) {
    if (ch === '{' || ch === '[') depth++;
    if (ch === '}' || ch === ']') depth--;
    if (ch === '|' && depth === 0) { parts.push(cur); cur = ''; } else cur += ch;
  }
  parts.push(cur);
  const fields: Record<string, string> = {};
  let n = 0;
  for (const p of parts.slice(1)) {
    const m = p.match(/^\s*([^=]+?)\s*=\s*([\s\S]*)$/);
    if (m) fields[m[1]] = m[2].trim(); else fields[String(++n)] = p.trim();
  }
  return { name: parts[0].trim(), fields };
}

// one line of text: links, notes and variants out, plain text in
const TOKEN = /<ref>([\s\S]*?)<\/ref>|(\{\{نسخہ\|[^{}]*\}\})|\[\[(?:لغت:)?([^\]|]+)(?:\|([^\]]+))?\]\]/g;

export function inline(src: string): Inline {
  const segments: Segment[] = [];
  let at = 0;
  const plain = (t: string) => { if (t) segments.push({ text: t.replace(/[ \t]+/g, ' ') }); };
  for (const m of src.matchAll(TOKEN)) {
    plain(src.slice(at, m.index));
    at = m.index! + m[0].length;
    if (m[1] !== undefined) segments.push({ note: m[1].trim() });
    else if (m[2]) {
      const { fields } = template(m[2]);
      segments.push({ variant: { shown: fields['1'] ?? '', others: Object.keys(fields).filter((k) => /^\d+$/.test(k) && k !== '1').map((k) => fields[k]),
        ...(fields['ماخذ'] && { source: fields['ماخذ'] }) } });
    } else segments.push({ lemma: m[3].trim(), text: (m[4] ?? m[3]).trim() });
  }
  plain(src.slice(at));
  // trim the line's outer spaces (in its first and last text)
  const texts = segments.filter((x): x is { text: string } => 'text' in x && !('lemma' in x));
  if (texts[0] && segments[0] === texts[0]) texts[0].text = texts[0].text.trimStart();
  if (texts.at(-1) && segments.at(-1) === texts.at(-1)) texts.at(-1)!.text = texts.at(-1)!.text.trimEnd();
  const shown = segments.map((x) => ('text' in x ? x.text : 'variant' in x ? x.variant.shown : '')).join('');
  return {
    text: shown.replace(/[ \t]+/g, ' ').trim(),
    words: segments.filter((x): x is { text: string; lemma: string } => 'lemma' in x).map((x) => ({ shown: x.text, lemma: x.lemma })),
    notes: segments.filter((x): x is { note: string } => 'note' in x).map((x) => x.note),
    variants: segments.filter((x): x is { variant: Variant } => 'variant' in x).map((x) => x.variant),
    segments: segments.filter((x) => !('text' in x && !('lemma' in x) && x.text === '')),
  };
}

// ---- writing Divan text back (editors build a Doc; this is its text) ----

export const segmentsText = (segs: Segment[]) => segs.map((x) =>
  'note' in x ? `<ref>${x.note}</ref>`
  : 'variant' in x ? `{{نسخہ|${[x.variant.shown, ...x.variant.others].join('|')}${x.variant.source ? `|ماخذ=${x.variant.source}` : ''}}}`
  : 'lemma' in x ? (x.lemma === x.text ? `[[${x.text}]]` : `[[لغت:${x.lemma}|${x.text}]]`)
  : x.text).join('');

export function toText(doc: Doc) {
  const out: string[] = [];
  const meta = Object.entries(doc.meta).filter(([, v]) => v !== undefined);
  if (meta.length) out.push(`{{دیوان\n${meta.map(([k, v]) => `| ${k} = ${v}`).join('\n')}\n}}`);
  let verse: string[] = [];
  const flush = () => { if (verse.length) out.push(`<poem>\n${verse.join('\n\n')}\n</poem>`); verse = []; };
  for (const b of doc.blocks) {
    if (b.type === 'heading') { flush(); const eq = '='.repeat(b.level); out.push(`${eq} ${b.text} ${eq}`); }
    else if (b.type === 'para') { flush(); out.push(segmentsText(b.line.segments)); }
    else verse.push(b.lines.map((l, i) => (i === 0 && b.label ? `{{${b.label}}} ` : '') + segmentsText(l.segments)).join('\n'));
  }
  flush();
  return out.join('\n\n') + '\n';
}

export function parse(src: string): Doc {
  const meta: Record<string, string> = {};
  let rest = src.replace(/\r\n?/g, '\n').replace(/^\s*\{\{\s*(?:دیوان|header)(?=[\s|])[\s\S]*?\n\}\}\s*\n?/u, (t) => {
    Object.assign(meta, template(t.trim()).fields);
    return '';
  });
  const blocks: Block[] = [];
  // verse inside <poem>, prose outside
  for (const piece of rest.split(/(<poem[^>]*>[\s\S]*?<\/poem>)/)) {
    const verse = piece.match(/^<poem[^>]*>([\s\S]*?)<\/poem>$/);
    for (const unit of (verse ? verse[1] : piece).split(/\n\s*\n/)) {
      let lines = unit.split('\n').map((l) => l.trim()).filter(Boolean);
      if (!lines.length) continue;
      if (!verse) {
        for (const l of lines) {
          const h = l.match(/^(=+)\s*(.+?)\s*=+$/);
          blocks.push(h ? { type: 'heading', text: h[2], level: h[1].length } : { type: 'para', line: inline(l) });
        }
        continue;
      }
      let label: string | undefined;
      const m = lines[0].match(/^\{\{(مطلع|حسن مطلع|مقطع)\}\}\s*/);
      if (m) { label = m[1]; lines[0] = lines[0].slice(m[0].length); }
      lines = lines.filter(Boolean);
      const type = lines.length === 2 ? 'couplet' : lines.length === 1 ? 'line' : 'stanza';
      blocks.push({ type, lines: lines.map(inline), ...(label && { label }) });
    }
  }
  return { meta, blocks };
}

// what counts as a word (for indexing, the word sidebar and dictionary links)
export const words = (text: string) =>
  text.split(/[\s،۔؟!؛:«»()"]+/).map((w) => w.replace(/ؔ/g, '')).filter(Boolean);

// → the site's verse JSON (the ganjoor-data layout divan-data already exports)
export function toVerses(doc: Doc) {
  const out: { VOrder: number; Position: string; Text: string; CoupletIndex: number; Level?: number }[] = [];
  let couplet = 0;
  for (const b of doc.blocks) {
    // headings inside a work (chapter, sub-heading) are their own kind of verse entry
    if (b.type === 'heading') { out.push({ VOrder: 0, Position: 'Heading', Text: b.text, CoupletIndex: couplet++, Level: b.level }); continue; }
    if (b.type === 'para') out.push({ VOrder: 0, Position: 'Paragraph', Text: b.line.text, CoupletIndex: couplet++ });
    else if (b.type === 'couplet') { b.lines.forEach((l, i) => out.push({ VOrder: 0, Position: i ? 'Left' : 'Right', Text: l.text, CoupletIndex: couplet })); couplet++; }
    else for (const l of b.lines) out.push({ VOrder: 0, Position: 'Single', Text: l.text, CoupletIndex: couplet++ });
  }
  return out.map((v, i) => ({ ...v, VOrder: i + 1 }));
}

const x = (s: string) => s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
// a line with its linked words as <w lemma>, variants as <app>, notes as <note>
function teiLine(l: Inline) {
  let s = x(l.text);
  for (const w of l.words) s = s.replace(x(w.shown), `<w lemma="${x(w.lemma)}">${x(w.shown)}</w>`);
  for (const v of l.variants) s = s.replace(x(v.shown), `<app><lem>${x(v.shown)}</lem>${v.others.map((o) => `<rdg${v.source ? ` source="${x(v.source)}"` : ''}>${x(o)}</rdg>`).join('')}</app>`);
  return s + l.notes.map((n) => `<note place="foot">${x(n)}</note>`).join('');
}

// → TEI P5: verse as line groups (a couplet is <lg type="sher"> with two <l>), prose as <div>/<p>
export function toTEI(doc: Doc) {
  const m = doc.meta, body: string[] = [];
  let n = 0;
  for (const b of doc.blocks) {
    if (b.type === 'heading') body.push(`<head type="h${b.level}">${x(b.text)}</head>`);
    else if (b.type === 'para') body.push(`<p>${teiLine(b.line)}</p>`);
    else {
      const type = b.type === 'couplet' ? 'sher' : b.type === 'stanza' ? 'band' : 'misra';
      const ana = b.label ? ` ana="#${b.label === 'مطلع' ? 'matla' : b.label === 'مقطع' ? 'maqta' : 'husn-e-matla'}"` : '';
      body.push(`<lg type="${type}" n="${++n}"${ana}>${b.lines.map((l, i) => `<l n="${i + 1}">${teiLine(l)}</l>`).join('')}</lg>`);
    }
  }
  const genre = m['صنف'] ?? '', poem = doc.blocks.some((b) => b.type !== 'para' && b.type !== 'heading');
  return `<TEI xmlns="http://www.tei-c.org/ns/1.0" xml:lang="ur"><teiHeader><fileDesc>`
    + `<titleStmt><title>${x(m['عنوان'] ?? m['title'] ?? '')}</title><author>${x(m['شاعر'] ?? m['مصنف'] ?? m['author'] ?? '')}</author></titleStmt>`
    + `<publicationStmt><publisher>دیوان</publisher><availability><licence target="https://creativecommons.org/licenses/by-sa/4.0/"/></availability></publicationStmt>`
    + `<sourceDesc><p>${x(m['ماخذ'] ?? 'ویکی ماخذ')}</p></sourceDesc></fileDesc></teiHeader>`
    + `<text><body>${poem ? `<lg type="${x(genre || 'poem')}"${m['ردیف'] ? ` rhyme="${x(m['ردیف'])}"` : ''}>${body.join('')}</lg>` : `<div type="chapter">${body.join('')}</div>`}</body></text></TEI>`;
}

export const LABELS_ = LABELS;
