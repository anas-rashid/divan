// Import / sync the Wiktionary data (see dictionary.ts). First run imports everything (about 1 GB of downloads);
// later runs re-import only sources that changed upstream and apply the day's Wiktionary edits.
//   node src/dict-sync.ts
import { readFile } from 'node:fs/promises';
import { pool } from './db.ts';
import { sync } from './dictionary.ts';

await pool.query(await readFile(new URL('../../db/schema.sql', import.meta.url), 'utf8'));
await sync((line) => console.log(`${new Date().toISOString()} ${line}`));
await pool.end();
