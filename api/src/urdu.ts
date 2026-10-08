// Urdu text normalisation for search. Applied to stored text at import and to search terms,
// so both sides compare in the same form. Ported from the .NET LanguageUtils.MakeTextSearchable.

const STRIP = new RegExp(
  '[' +
    '\u064B-\u0652' + // Arabic diacritics: tanween, fatha, kasra, damma, shadda, sukun
    '\u0654\u0670\u0657\u0658\u0615' + // hamza above, superscript alef, Urdu diacritics
    '\u200D\u200E\u200F' + // zwj, ltr/rtl marks
    '.\u060C!\u061F:\u061B;*()\\[\\]"\'\u00AB\u00BB\u06D4' + // punctuation incl. Urdu full stop
  ']',
  'g',
);

const LETTERS: Record<string, string> = {
  '\u064A': '\u06CC', // arabic yeh -> farsi/urdu yeh
  '\u0649': '\u06CC', // alef maksura -> yeh
  '\u0626': '\u06CC', // yeh with hamza -> yeh (upstream rule)
  '\u0643': '\u06A9', // arabic kaf -> keheh
  '\u06C2': '\u06C1', // heh goal with hamza -> heh goal
  '\u0647': '\u06C1', // arabic heh -> heh goal (do-chashmi heh U+06BE stays distinct)
  '\u06D3': '\u06D2', // yeh barree with hamza -> yeh barree
  '\u0623': '\u0627', // alef with hamza above -> alef
  '\u0625': '\u0627', // alef with hamza below -> alef
};
const LETTER_RE = new RegExp('[' + Object.keys(LETTERS).join('') + ']', 'g');

export function normalise(text: string): string {
  return text
    .replace(/\u200C/g, ' ') // zwnj -> space
    .replace(STRIP, '')
    .replace(LETTER_RE, (c) => LETTERS[c])
    .replace(/\s+/g, ' ')
    .trim();
}

// Normalised search terms: "quoted phrase" -> one term, otherwise one per word (all must match).
export function terms(term: string): string[] {
  const t = (term ?? '').trim();
  if (!t) return [];
  const phrase = t.length > 1 && t.startsWith('"') && t.endsWith('"');
  const n = normalise(t);
  return (phrase ? [n] : n.split(' ')).filter(Boolean);
}

// ILIKE patterns, one per term.
export const likePatterns = (term: string) => terms(term).map((p) => '%' + p.replace(/[\\%_]/g, (c) => '\\' + c) + '%');

// A regex that finds the terms in original (unnormalised) text, with one capture group so
// text.split(re) puts the matches at odd indexes. Each letter also matches its variants, with
// diacritics allowed between letters; a space matches spaces and punctuation.
const VARIANTS: Record<string, string> = {};
for (const [from, to] of Object.entries(LETTERS)) VARIANTS[to] = (VARIANTS[to] ?? to) + from;
const SKIP = '[\u064B-\u0652\u0654\u0670\u0657\u0658\u0615\u200D\u200E\u200F]*';
export function highlighter(term: string): RegExp | null {
  const ts = terms(term);
  if (!ts.length) return null;
  const one = (t: string) => [...t].map((c) =>
    c === ' ' ? '[\\s\u200C.\u060C!\u061F:\u061B;*()"\'\u00AB\u00BB\u06D4]+'
    : VARIANTS[c] ? `[${VARIANTS[c]}]` : c.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')).join(SKIP) + SKIP;
  return new RegExp(`(${ts.sort((a, b) => b.length - a.length).map(one).join('|')})`, 'iu');
}
