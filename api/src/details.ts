// Poet and book details (#32): a poet's name, pen name, years and intro, and a book or section's title, edited in
// Divan. Each is a revision (entity 'poet', entity_id = poets.id; 'book', entity_id = categories.id) through the same
// pipeline as a work. The text is one "label: value" per line; a poet's intro follows "تعارف:" to the end:
//   نام: محمد اقبال
//   تخلص: اقبال
//   پیدائش: 1877
//   وفات: 1938
//   تعارف:
//   ڈاکٹر سر علامہ محمد اقبال …
// Published details live in divan-data as divan/<url>.poet and divan/<url>.book, which the export
// (export_divan.py) prefers to Wikisource's and Wikipedia's, so the daily sync keeps them.
import { mkdir, writeFile } from 'node:fs/promises';
import { dirname, join } from 'node:path';
import type { PoolClient } from 'pg';
import { pool } from './db.ts';

export type DetailKind = 'poet' | 'book';
export const isDetail = (entity: string): entity is DetailKind => entity === 'poet' || entity === 'book';
const POET = ['نام', 'تخلص', 'پیدائش', 'وفات'];
const hijri = (ce: number | null) => (ce ? Math.round(((ce - 622) * 33) / 32) : null); // as export_divan.py

export function parseDetails(text: string) {
  const lines = text.replace(/\r\n?/g, '\n').split('\n'), out: Record<string, string> = {};
  const intro = lines.findIndex((l) => /^تعارف\s*:/.test(l));
  for (const l of intro < 0 ? lines : lines.slice(0, intro)) {
    const m = l.match(/^([^:]+):\s*(.*)$/);
    if (m) out[m[1].trim()] = m[2].trim();
  }
  if (intro >= 0) out['تعارف'] = [lines[intro].replace(/^تعارف\s*:\s*/, ''), ...lines.slice(intro + 1)].join('\n').trim();
  return out;
}

export const detailsText = (kind: DetailKind, d: Record<string, string>) => kind === 'book' ? `عنوان: ${d['عنوان'] ?? ''}`
  : `${POET.map((l) => `${l}: ${d[l] ?? ''}`).join('\n')}\nتعارف:\n${d['تعارف'] ?? ''}`;

// what the details are now (the site's values, which follow the latest published version) and that version
export async function currentDetails(kind: DetailKind, id: number) {
  const row = kind === 'poet'
    ? (await pool.query('SELECT id, url, nickname AS title, name, nickname, description, birth_year_ce, death_year_ce FROM poets WHERE id = $1', [id])).rows[0]
    : (await pool.query('SELECT id, url, title FROM categories WHERE id = $1', [id])).rows[0];
  if (!row) return null;
  const last = (await pool.query(
    `SELECT max(version) AS v FROM revisions WHERE entity = $1 AND entity_id = $2 AND status = 'published'`, [kind, id])).rows[0];
  const content = detailsText(kind, kind === 'book' ? { عنوان: row.title } : {
    نام: row.name, تخلص: row.nickname, پیدائش: row.birth_year_ce ?? '', وفات: row.death_year_ce ?? '', تعارف: row.description ?? '' });
  return { details: { kind, id: row.id, url: row.url, title: row.title }, version: Number(last?.v ?? 0), content };
}

const year = (s: string | undefined) => (s ? Number(s.replace(/[۰-۹]/g, (d) => String(d.charCodeAt(0) - 0x6f0))) : null);

// what is wrong with the text, or null
export function checkDetails(kind: DetailKind, text: string) {
  const d = parseDetails(text);
  if (kind === 'book') {
    if (!d['عنوان']) return 'عنوان ضروری ہے';
    return d['عنوان'].length > 200 ? 'عنوان بہت لمبا ہے' : null;
  }
  if (!d['نام'] || !d['تخلص']) return 'نام اور تخلص ضروری ہیں';
  if (d['نام'].length > 100 || d['تخلص'].length > 100) return 'نام بہت لمبا ہے';
  const [born, died] = [year(d['پیدائش']), year(d['وفات'])];
  for (const y of [born, died]) if (y !== null && !(Number.isInteger(y) && y >= 500 && y <= 2100)) return 'سال عیسوی میں لکھیں، جیسے ۱۸۷۷';
  if (born && died && died < born) return 'وفات کا سال پیدائش سے پہلے ہے';
  return (d['تعارف'] ?? '').length > 5000 ? 'تعارف بہت لمبا ہے' : null;
}

export async function applyDetails(client: PoolClient, kind: DetailKind, id: number, text: string) {
  const d = parseDetails(text);
  if (kind === 'book') return void (await client.query('UPDATE categories SET title = $2 WHERE id = $1', [id, d['عنوان']]));
  const [born, died] = [year(d['پیدائش']), year(d['وفات'])];
  await client.query(`UPDATE poets SET name = $2, nickname = $3, description = $4, birth_year_ce = $5, death_year_ce = $6,
                      birth_year_ah = $7, death_year_ah = $8 WHERE id = $1`,
    [id, d['نام'], d['تخلص'], d['تعارف'] || null, born, died, hijri(born), hijri(died)]);
  // the poet's top level carries their name (as the export makes it)
  await client.query('UPDATE categories SET title = $2 WHERE poet_id = $1 AND parent_id IS NULL', [id, d['نام']]);
}

export async function writeDetails(dataDir: string, kind: DetailKind, url: string, text: string) {
  const path = join(dataDir, 'divan', url.replace(/^\//, '') + '.' + kind);
  await mkdir(dirname(path), { recursive: true });
  const d = parseDetails(text);
  for (const k of ['پیدائش', 'وفات']) if (d[k]) d[k] = String(year(d[k])); // plain digits for the export
  await writeFile(path, detailsText(kind, d).trim() + '\n');
  return path;
}
