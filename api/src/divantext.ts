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

export type Inline = { text: string; words: { shown: string; lemma: string }[]; notes: string[]; variants: { shown: string; others: string[]; source?: string }[] };
export type Block =
  | { type: 'heading'; text: string }
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
export function inline(src: string): Inline {
  const words: Inline['words'] = [], notes: string[] = [], variants: Inline['variants'] = [];
  let text = src
    .replace(/<ref>([\s\S]*?)<\/ref>/g, (_, n) => { notes.push(n.trim()); return ''; })
    .replace(/\{\{نسخہ\|[^{}]*\}\}/g, (t) => {
      const { fields } = template(t), others = Object.keys(fields).filter((k) => /^\d+$/.test(k) && k !== '1').map((k) => fields[k]);
      variants.push({ shown: fields['1'] ?? '', others, source: fields['ماخذ'] });
      return fields['1'] ?? '';
    })
    .replace(/\[\[(?:لغت:)?([^\]|]+)(?:\|([^\]]+))?\]\]/g, (_, lemma, shown) => {
      words.push({ lemma: lemma.trim(), shown: (shown ?? lemma).trim() });
      return (shown ?? lemma).trim();
    });
  text = text.replace(/[ \t]+/g, ' ').trim();
  return { text, words, notes, variants };
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
          const h = l.match(/^=+\s*(.+?)\s*=+$/);
          blocks.push(h ? { type: 'heading', text: h[1] } : { type: 'para', line: inline(l) });
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
  const out: { VOrder: number; Position: string; Text: string; CoupletIndex: number }[] = [];
  let couplet = 0;
  for (const b of doc.blocks) {
    if (b.type === 'heading') continue;
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
    if (b.type === 'heading') body.push(`<head>${x(b.text)}</head>`);
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
