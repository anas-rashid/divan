// Divan-owned content (#30): works edited in Divan are kept in divan-data's divan/ folder, so the daily
// Wikisource sync never overwrites them (export_divan.py prefers them). Each work is two files:
//   divan/<work url>.dtx   Divan text, the readable source (see divantext.ts, docs/content-model.md)
//   divan/<work url>.json  generated from it: Title, Verses (the site's verse layout), Edited (who, when)
// Publishing (#34) calls writeOwned and commits both; fromPoem gives the starting text for an edit.
//   node src/owned.ts <divan-data dir> <work url> <file.dtx> [editor]   write one by hand
import { mkdir, readFile, writeFile } from 'node:fs/promises';
import { dirname, join } from 'node:path';
import { parse, toVerses } from './divantext.ts';

type Verse = { Position: string; Text: string; CoupletIndex: number };

// a work (ganjoor-data poem JSON) as Divan text: verse in <poem> (blank line between couplets, stanzas and
// lines), prose as paragraphs
export function fromPoem(poem: { Title: string; Verses: Verse[]; SourceUrl?: string }, meta: Record<string, string> = {}) {
  const head = Object.entries({ عنوان: poem.Title, ...meta, ...(poem.SourceUrl && { ماخذ: 'ویکی ماخذ', ماخذ_ربط: poem.SourceUrl }) })
    .map(([k, v]) => `| ${k} = ${v}`).join('\n');
  const units = new Map<number, Verse[]>();
  for (const v of poem.Verses) (units.get(v.CoupletIndex) ?? units.set(v.CoupletIndex, []).get(v.CoupletIndex)!).push(v);
  const parts: string[] = [];
  let verse: string[] = [];
  const flush = () => { if (verse.length) parts.push(`<poem>\n${verse.join('\n\n')}\n</poem>`); verse = []; };
  for (const u of units.values()) {
    if (u[0].Position === 'Paragraph') { flush(); parts.push(u.map((v) => v.Text).join('\n')); }
    else if (u[0].Position === 'Heading') { flush(); parts.push(`${'='.repeat((u[0] as any).Level ?? 2)} ${u[0].Text} ${'='.repeat((u[0] as any).Level ?? 2)}`); }
    else verse.push(u.map((v) => v.Text).join('\n'));
  }
  flush();
  return `{{دیوان\n${head}\n}}\n${parts.join('\n\n')}\n`;
}

// write a Divan-owned work (the .dtx and the generated .json); returns the paths written
export async function writeOwned(dataDir: string, url: string, dtx: string, edited: { by: string; at?: string }) {
  const doc = parse(dtx);
  const verses = toVerses(doc);
  if (!verses.length) throw new Error('the text has no verses or paragraphs');
  const base = join(dataDir, 'divan', url.replace(/^\/+|\/+$/g, ''));
  if (!/^[\w/-]+$/.test(url) || url.includes('..')) throw new Error(`not a work url: ${url}`);
  await mkdir(dirname(base), { recursive: true });
  const json = { FullUrl: '/' + url.replace(/^\/+/, ''), Title: doc.meta['عنوان'] ?? '', Verses: verses.map(({ VOrder, ...v }) => ({ VOrder, ...v, SectionIndex1: 0 })),
    Edited: { by: edited.by, at: edited.at ?? new Date().toISOString() } };
  await writeFile(base + '.dtx', dtx.endsWith('\n') ? dtx : dtx + '\n');
  await writeFile(base + '.json', JSON.stringify(json, null, 1) + '\n');
  return [base + '.dtx', base + '.json'];
}

if (import.meta.url === `file://${process.argv[1]}`) {
  const [dir, url, file, by = 'divan'] = process.argv.slice(2);
  if (!dir || !url || !file) { console.error('usage: node src/owned.ts <divan-data dir> <work url> <file.dtx> [editor]'); process.exit(1); }
  console.log((await writeOwned(dir, url, await readFile(file, 'utf8'), { by })).join('\n'));
}
