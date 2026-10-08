import { pool } from './db.ts';
import { normalise } from './urdu.ts';

// poets/writers by name or pen name, and books/chapters by title, matched with the same Urdu normalisation
// as the content; ponytail: ~1,400 rows filtered in memory per search, add normalised columns + trigram index if it grows
export async function nameMatches(terms: string[]) {
  const has = (text: string) => { const n = normalise(text); return terms.every((t) => n.includes(t)); };
  const [poets, cats] = await Promise.all([
    pool.query(`SELECT p.id, p.url, p.name, p.nickname, p.birth_year_ah, p.death_year_ah, p.birth_year_ce, p.death_year_ce,
                       (SELECT count(*)::int FROM poems w WHERE w.poet_id = p.id) AS works FROM poets p`),
    pool.query(`SELECT c.id, c.url, c.title, t.nickname AS poet, t.url AS poet_url,
                       (SELECT count(*)::int FROM poems w WHERE w.category_id = c.id) AS works
                FROM categories c JOIN poets t ON t.id = c.poet_id WHERE c.parent_id IS NOT NULL`),
  ]);
  return {
    poets: poets.rows.filter((p) => has(`${p.name} ${p.nickname}`)).sort((a, b) => b.works - a.works).slice(0, 24),
    books: cats.rows.filter((c) => has(c.title)).sort((a, b) => b.works - a.works).slice(0, 12),
  };
}
