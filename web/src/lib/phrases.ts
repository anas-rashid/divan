// Where a bookmarked phrase sits in a couplet's lines: the part of each line to mark. A phrase can run
// across the two misras (end of the first + start of the second). The pen-name sign ؔ is ignored.
const bare = (s: string) => s.replace(/ؔ/g, '');
const esc = (s: string) => s.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
// the phrase as a regex over original text: ؔ allowed after any letter, any run of spaces between words
export const phraseRe = (p: string) => new RegExp([...p].map((c) => (c === ' ' ? '\\s+' : esc(c) + '\\u0614?')).join(''), 'u');

export function phraseParts(lines: string[], phrase: string): (string | null)[] {
  const parts: (string | null)[] = lines.map(() => null);
  const i = lines.findIndex((l) => phraseRe(phrase).test(l));
  if (i >= 0) { parts[i] = phrase; return parts; }
  const words = phrase.split(' ');
  for (let k = words.length - 1; k > 0 && lines.length > 1; k--) { // first k words end line 0, the rest start line 1
    const head = words.slice(0, k).join(' '), tail = words.slice(k).join(' ');
    if (bare(lines[0]).trimEnd().endsWith(head) && bare(lines[1]).trimStart().startsWith(tail)) return [head, tail];
  }
  return parts;
}
