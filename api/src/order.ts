// Arranging (#41): the order of a book or section's contents (its sub-sections, then its works); a poet's top level
// lists their books. An arrangement is a revision (entity 'order', entity_id = categories.id) and goes through the
// same pipeline as a work's text. Its text is one line per item, "<last part of the URL> <title>", e.g.
//   c641 بانگ درا
//   sh6556 ہمالہ
// Only the order of the lines counts; titles are for reading. Published orders live in divan-data as
// divan/<category url>.order, which the export follows (export_divan.py), so the daily sync keeps Divan's order.
import { mkdir, writeFile } from 'node:fs/promises';
import { dirname, join } from 'node:path';
import type { PoolClient } from 'pg';
import { pool } from './db.ts';

const slug = (url: string) => url.slice(url.lastIndexOf('/') + 1);

// the category's items in their current order (the site's positions)
async function items(catId: number) {
  const [cats, poems] = await Promise.all([
    pool.query('SELECT id, url, title FROM categories WHERE parent_id = $1 ORDER BY position, id', [catId]),
    pool.query('SELECT id, url, title FROM poems WHERE category_id = $1 ORDER BY position, id', [catId]),
  ]);
  return { cats: cats.rows, poems: poems.rows };
}

const lines = (xs: { url: string; title: string }[]) => xs.map((x) => `${slug(x.url)} ${x.title}`);

// a category's current order as text, and the latest published version of its order
export async function currentOrder(catId: number) {
  const cat = (await pool.query('SELECT id, url, title, parent_id FROM categories WHERE id = $1', [catId])).rows[0];
  if (!cat) return null;
  const { cats, poems } = await items(catId);
  const last = (await pool.query(
    `SELECT max(version) AS v FROM revisions WHERE entity = 'order' AND entity_id = $1 AND status = 'published'`, [catId])).rows[0];
  return { cat, version: Number(last?.v ?? 0), content: [...lines(cats), ...lines(poems)].join('\n'), count: cats.length + poems.length };
}

export const orderSlugs = (text: string) => text.split('\n').map((l) => l.trim().split(/\s/)[0]).filter(Boolean);

// the text must list every item of the category exactly once (nothing added, removed or repeated)
export async function checkOrder(catId: number, text: string) {
  const { cats, poems } = await items(catId);
  const want = [...cats, ...poems].map((x) => slug(x.url)).sort(), got = orderSlugs(text);
  if (new Set(got).size !== got.length) return 'ایک ہی چیز دو بار درج ہے';
  if (got.length !== want.length || [...got].sort().some((s, i) => s !== want[i]))
    return 'فہرست میں اس حصے کی تمام چیزیں ایک ایک بار ہونی چاہییں (نہ کوئی نئی، نہ کم)';
  return null;
}

// positions on the site: sub-sections and works each numbered in the order of the text
export async function applyOrder(client: PoolClient, catId: number, text: string) {
  const at = new Map(orderSlugs(text).map((s, i) => [s, i]));
  const { cats, poems } = await items(catId);
  for (const [table, xs] of [['categories', cats], ['poems', poems]] as const)
    for (const x of xs) await client.query(`UPDATE ${table} SET position = $2 WHERE id = $1`, [x.id, at.get(slug(x.url))]);
}

export async function writeOrder(dataDir: string, url: string, text: string) {
  const path = join(dataDir, 'divan', url.replace(/^\//, '') + '.order');
  await mkdir(dirname(path), { recursive: true });
  await writeFile(path, text.trim() + '\n');
  return path;
}
