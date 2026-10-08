import { test, after } from 'node:test';
import assert from 'node:assert/strict';
import Fastify from 'fastify';
import { authRoutes } from './auth.ts';
import { libraryRoutes, cleanWord, cleanPhrase } from './library.ts';
import { pool } from './db.ts';

after(() => pool.end());

test('saved words are cleaned like the sidebar does', () => {
  assert.equal(cleanWord(' دل، '), 'دل');
  assert.equal(cleanWord('غالبؔ'), 'غالب');
  assert.equal(cleanWord('love'), null);
  assert.equal(cleanPhrase('  وصال   یار '), 'وصال یار');
  assert.equal(cleanPhrase('ی'), null);
  assert.equal(cleanWord(''), null);
});

test('library: save and unsave poets, works, couplets and words; full paths; notes; isolation', async () => {
  const app = Fastify();
  authRoutes(app); libraryRoutes(app);
  const run = Date.now();
  const call = (method: string, url: string, body?: object, token?: string) =>
    app.inject({ method: method as any, url, payload: body, headers: { ...(token && { authorization: `Bearer ${token}` }), 'x-client-ip': `lib-${run}` } });
  const me = (await call('POST', '/api/auth/signup', { email: `lib-${run}@divan.test`, password: 'pass-word-1' })).json();
  const other = (await call('POST', '/api/auth/signup', { email: `lib2-${run}@divan.test`, password: 'pass-word-1' })).json();
  const t = me.token, toggle = (body: object, tk = t) => call('POST', '/api/library/toggle', body, tk);

  const poem = (await pool.query(`SELECT p.id FROM poems p JOIN categories c ON c.id = p.category_id WHERE c.url = '/p266/ghazal' LIMIT 1`)).rows[0].id;

  assert.equal((await call('GET', '/api/library')).statusCode, 401, 'signed out');
  assert.deepEqual((await toggle({ kind: 'poet', poetId: 266 })).json().saved, true);
  assert.deepEqual((await toggle({ kind: 'poem', poemId: poem })).json().saved, true);
  const ghazals = (await pool.query(`SELECT id FROM categories WHERE url = '/p266/ghazal'`)).rows[0].id;
  assert.deepEqual((await toggle({ kind: 'category', categoryId: ghazals })).json().saved, true, 'a book/chapter');
  assert.equal((await toggle({ kind: 'category', categoryId: 999999 })).statusCode, 404);
  assert.deepEqual((await toggle({ kind: 'couplet', poemId: poem, couplet: 0 })).json().saved, true);
  assert.deepEqual((await toggle({ kind: 'couplet', poemId: poem, couplet: 2 })).json().saved, true);
  assert.deepEqual((await toggle({ kind: 'word', word: 'وصال،', poemId: poem, couplet: 0 })).json().saved, true);
  assert.equal((await toggle({ kind: 'couplet', poemId: poem, couplet: 9999 })).statusCode, 404);
  assert.equal((await toggle({ kind: 'poet', poetId: 999999 })).statusCode, 404);
  assert.equal((await toggle({ kind: 'word', word: 'hello' })).statusCode, 400);

  // phrases: part of a couplet (also across its two misras), must be in that couplet
  const lines0 = (await pool.query('SELECT text FROM verses WHERE poem_id = $1 AND couplet = 0 ORDER BY vorder', [poem])).rows.map((r) => r.text);
  const across = lines0[0].split(' ').slice(-2).join(' ') + ' ' + lines0[1].split(' ')[0];
  assert.equal((await toggle({ kind: 'phrase', poemId: poem, couplet: 0, phrase: lines0[0].split(' ').slice(0, 3).join(' ') })).json().saved, true);
  assert.equal((await toggle({ kind: 'phrase', poemId: poem, couplet: 0, phrase: across })).json().saved, true, 'across the two misras');
  assert.equal((await toggle({ kind: 'phrase', poemId: poem, couplet: 0, phrase: 'یہ عبارت یہاں نہیں' })).statusCode, 400);
  // marks for contents lists: the work, its book and its poet
  const marks = (await call('GET', '/api/library/marks', undefined, t)).json();
  assert.deepEqual(marks.poems[poem], { fav: true, bm: 4 }, 'two couplets and two phrases');
  const book = (await pool.query(`SELECT id FROM categories WHERE url = '/p266/ghazal'`)).rows[0].id;
  assert.ok(marks.categories.includes(book) && marks.poets.includes(266));
  // state for a page
  assert.deepEqual((await call('GET', `/api/library/state?poet=266&category=${ghazals}&poem=${poem}`, undefined, t)).json(), { poet: true, category: true, poem: true, couplets: [0, 2], phrases: [{ couplet: 0, phrase: lines0[0].split(' ').slice(0, 3).join(' ') }, { couplet: 0, phrase: across }] });

  assert.deepEqual((await call('GET', '/api/library/state?word=' + encodeURIComponent('وصال'), undefined, t)).json(), { word: true });
  assert.deepEqual((await call('GET', '/api/library/state?word=' + encodeURIComponent('ہجر'), undefined, t)).json(), { word: false });
  // toggling again removes
  assert.equal((await toggle({ kind: 'couplet', poemId: poem, couplet: 2 })).json().saved, false);

  const lib = (await call('GET', '/api/library', undefined, t)).json();
  assert.equal(lib.poets[0].poet.url, '/p266');
  assert.deepEqual(lib.poems[0].poem.path.map((c: any) => c.url), ['/p266', '/p266/ghazal'], 'poet » book path');
  assert.deepEqual(lib.categories[0].path.map((c: any) => c.url), ['/p266', '/p266/ghazal']);
  assert.equal(lib.couplets.length, 1);
  assert.equal(lib.phrases.length, 2);
  assert.deepEqual(lib.phrases[0].poem.path.map((c: any) => c.url), ['/p266', '/p266/ghazal']);
  assert.equal(lib.couplets[0].lines.length, 2, 'both misras');
  assert.equal(lib.words[0].word, 'وصال');
  assert.equal(lib.words[0].source.couplet, 0);

  // notes, deletion, and readers cannot touch each other's items
  const cid = lib.couplets[0].id;
  assert.equal((await call('POST', `/api/library/${cid}/note`, { note: 'مطلع' }, other.token)).statusCode, 404);
  assert.equal((await call('POST', `/api/library/${cid}/note`, { note: '  مطلع  ' }, t)).statusCode, 200);
  assert.equal((await call('GET', '/api/library', undefined, t)).json().couplets[0].note, 'مطلع');
  assert.equal((await call('POST', `/api/library/${cid}/delete`, undefined, other.token)).statusCode, 404);
  assert.equal((await call('GET', '/api/library', undefined, other.token)).json().poets.length, 0);
  assert.equal((await call('POST', `/api/library/${cid}/delete`, undefined, t)).statusCode, 200);

  // deleting the account deletes the library
  await call('POST', '/api/auth/delete', { password: 'pass-word-1' }, t);
  assert.equal((await pool.query('SELECT count(*)::int AS n FROM library WHERE user_id = $1', [me.user.id])).rows[0].n, 0);
  await pool.query('DELETE FROM users WHERE id = $1', [other.user.id]);
  await app.close();
});
