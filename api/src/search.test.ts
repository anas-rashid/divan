import { test, after } from 'node:test';
import assert from 'node:assert/strict';
import { nameMatches } from './search.ts';
import { terms } from './urdu.ts';
import { pool } from './db.ts';

after(() => pool.end());

test('poets/writers by name or pen name; books/chapters by title; Urdu variants match', async () => {
  const ghalib = await nameMatches(terms('غالب'));
  assert.ok(ghalib.poets.some((p) => p.url === '/p266'), 'Ghalib by pen name');
  const iqbal = await nameMatches(terms('اقبال'));
  assert.equal(iqbal.poets[0].url, '/p238', 'most works first');
  const book = await nameMatches(terms('بانگ درا'));
  assert.ok(book.books.some((b) => b.url.startsWith('/p238/')), 'Iqbal\'s book by title');
  assert.ok(book.books[0].poet && book.books[0].works > 0);
  const arabic = await nameMatches(terms('مير تقي مير')); // Arabic yeh
  assert.ok(arabic.poets.some((p) => p.url === '/p303'), 'Arabic ي/ی variants');
  assert.deepEqual(await nameMatches(terms('ژژژژژ')), { poets: [], books: [] });
});
