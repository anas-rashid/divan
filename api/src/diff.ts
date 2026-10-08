// Line diff for reviewing revisions of Divan text: each line kept ('='), removed ('-') or added ('+').
// ponytail: longest-common-subsequence table, O(lines²); fine for a poem or a chapter (hundreds of lines),
// switch to Myers' algorithm if whole books are ever diffed at once.
export type DiffLine = { op: '=' | '-' | '+'; text: string };

export function diffLines(a: string, b: string): DiffLine[] {
  const x = a.split('\n'), y = b.split('\n');
  const n = x.length, m = y.length;
  const lcs = Array.from({ length: n + 1 }, () => new Uint32Array(m + 1));
  for (let i = n - 1; i >= 0; i--)
    for (let j = m - 1; j >= 0; j--) lcs[i][j] = x[i] === y[j] ? lcs[i + 1][j + 1] + 1 : Math.max(lcs[i + 1][j], lcs[i][j + 1]);
  const out: DiffLine[] = [];
  let i = 0, j = 0;
  while (i < n && j < m) {
    if (x[i] === y[j]) { out.push({ op: '=', text: x[i] }); i++; j++; }
    else if (lcs[i + 1][j] >= lcs[i][j + 1]) out.push({ op: '-', text: x[i++] });
    else out.push({ op: '+', text: y[j++] });
  }
  while (i < n) out.push({ op: '-', text: x[i++] });
  while (j < m) out.push({ op: '+', text: y[j++] });
  return out;
}

export const changed = (d: DiffLine[]) => d.filter((l) => l.op !== '=').length;
