// Load divan-data (ganjoor-data layout: manifest.json, poets/**/_cat.json, poems) into PostgreSQL.
// Source: a local divan-data folder or the public CDN. Upserts, so re-running applies sync changes.
//   node src/import.ts [../../divan-data | https://cdn.jsdelivr.net/gh/anas-rashid/divan-data@main/]
import { readFile } from 'node:fs/promises';
import { join } from 'node:path';
import { pool } from './db.ts';
import { normalise } from './urdu.ts';

const SOURCE = process.argv[2] ?? new URL('../../../divan-data', import.meta.url).pathname;
const isHttp = /^https?:/.test(SOURCE);

async function read(path: string): Promise<any> {
  if (isHttp) {
    const res = await fetch(SOURCE.replace(/\/?$/, '/') + path);
    if (!res.ok) throw new Error(`${res.status} ${path}`);
    return res.json();
  }
  return JSON.parse(await readFile(join(SOURCE, path), 'utf8'));
}

const schema = await readFile(new URL('../../db/schema.sql', import.meta.url), 'utf8');
await pool.query(schema);

const manifest = await read('manifest.json');
console.log(`${manifest.PoetsCount} poets, ${manifest.PoemsCount} poems from ${SOURCE}`);

let poems = 0;
for (const entry of manifest.Poets) {
  const poet = await read(`poets${entry.FullUrl}/poet.json`);
  const client = await pool.connect();
  try {
    await client.query('BEGIN');
    await client.query(
      `INSERT INTO poets (id, url, name, nickname, description, birth_year_ah, death_year_ah)
       VALUES ($1,$2,$3,$4,$5,$6,$7)
       ON CONFLICT (id) DO UPDATE SET url=$2, name=$3, nickname=$4, description=$5, birth_year_ah=$6, death_year_ah=$7`,
      [poet.Id, poet.FullUrl, poet.Name, poet.Nickname, poet.Description,
       poet.ValidBirthDate ? poet.BirthYearInLHijri : null, poet.ValidDeathDate ? poet.DeathYearInLHijri : null],
    );
    // categories depth-first so parents exist before children
    const walk = async (url: string, position: number): Promise<void> => {
      const cat = await read(`poets${url}/_cat.json`);
      await client.query(
        `INSERT INTO categories (id, poet_id, parent_id, url, title, position) VALUES ($1,$2,$3,$4,$5,$6)
         ON CONFLICT (id) DO UPDATE SET poet_id=$2, parent_id=$3, url=$4, title=$5, position=$6`,
        [cat.Id, cat.PoetId, cat.ParentId, cat.FullUrl, cat.Title, position],
      );
      for (const [i, ref] of cat.Poems.entries()) {
        const p = await read(`poets${ref.FullUrl}.json`);
        const searchText = normalise([p.Title, ...p.Verses.map((v: any) => v.Text)].join(' '));
        await client.query(
          `INSERT INTO poems (id, category_id, poet_id, url, title, full_title, source_url, position, search_text)
           VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9)
           ON CONFLICT (id) DO UPDATE SET category_id=$2, poet_id=$3, url=$4, title=$5, full_title=$6,
             source_url=$7, position=$8, search_text=$9`,
          [p.Id, cat.Id, poet.Id, p.FullUrl, p.Title, p.FullTitle, p.SourceUrl ?? null, i, searchText],
        );
        await client.query('DELETE FROM verses WHERE poem_id = $1', [p.Id]);
        if (p.Verses.length) {
          // one multi-row insert per poem
          const values: unknown[] = [];
          const rows = p.Verses.map((v: any, j: number) => {
            values.push(p.Id, v.VOrder, v.Position, v.CoupletIndex ?? 0, v.Text);
            const o = j * 5;
            return `($${o + 1},$${o + 2},$${o + 3},$${o + 4},$${o + 5})`;
          });
          await client.query(`INSERT INTO verses (poem_id, vorder, position, couplet, text) VALUES ${rows.join(',')}`, values);
        }
        poems++;
      }
      for (const [i, child] of cat.ChildCats.entries()) await walk(child.FullUrl, i);
    };
    await walk(entry.FullUrl, 0);
    await client.query('COMMIT');
  } catch (e) {
    await client.query('ROLLBACK');
    throw e;
  } finally {
    client.release();
  }
  process.stdout.write(`\r${poems} poems`);
}
await pool.query(await readFile(new URL('../../db/featured.sql', import.meta.url), 'utf8'));
console.log(`\nimported ${manifest.Poets.length} poets, ${poems} poems`);
await pool.end();
