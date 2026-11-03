import { test, after } from 'node:test';
import assert from 'node:assert/strict';
import { mkdtemp, readFile, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { execFileSync } from 'node:child_process';
import Fastify from 'fastify';
import { authRoutes } from './auth.ts';
import { adminRoutes } from './admin.ts';
import { permissionRoutes } from './permissions.ts';
import { moderationRoutes } from './moderation.ts';
import { checkDetails, loadWords } from './details.ts';
import { divanEntry, lookup, key } from './dictionary.ts';
import { pool } from './db.ts';

after(() => pool.end());

test('a dictionary entry: its text, checks, and the sidebar\'s reading of it', () => {
  const text = 'لفظ: ملک\nزبان: اردو\nاصل: عربی\nمعانی:\nمُلْک (mulk، اسم مذکر): بادشاہت، سلطنت\nمثال: ملک کا انتظام\nمَلِک (malik): بادشاہ\nدیس';
  assert.equal(checkDetails('word', text), null);
  assert.match(checkDetails('word', 'لفظ: ملک\nزبان: انگریزی\nمعانی:\nx')!, /زبان/);
  assert.match(checkDetails('word', 'لفظ: ملک\nزبان: اردو\nمعانی:\n')!, /معنی/);
  assert.match(checkDetails('word', 'لفظ: mulk\nزبان: اردو\nمعانی:\nx')!, /حروف/);
  assert.deepEqual(divanEntry(text), { word: 'ملک', lang: 'ur', origin: 'عربی', meanings: [
    { form: 'مُلْک', tr: 'mulk', pos: 'اسم مذکر', text: 'بادشاہت، سلطنت', examples: ['ملک کا انتظام'] },
    { form: 'مَلِک', tr: 'malik', pos: null, text: 'بادشاہ', examples: [] },
    { form: null, tr: null, pos: null, text: 'دیس', examples: [] }] });
});

test('dictionary editor: the grant, a new entry from Wiktionary, publishing to divan-data, first in the sidebar, the import', async () => {
  const data = await mkdtemp(join(tmpdir(), 'divan-data-'));
  process.env.DIVAN_DATA_DIR = data;
  const git = (...a: string[]) => execFileSync('git', ['-C', data, ...a], { encoding: 'utf8' });
  git('init', '-q'); git('-c', 'user.name=t', '-c', 'user.email=t@t', 'commit', '-q', '--allow-empty', '-m', 'start');
  const app = Fastify();
  authRoutes(app); adminRoutes(app); permissionRoutes(app); moderationRoutes(app);
  const run = Date.now(), word = 'آزمائشلفظ' + 'ابتثج'[run % 5];  // a word no dictionary has
  const call = (method: string, url: string, body?: object, token?: string) =>
    app.inject({ method: method as any, url, payload: body, headers: { ...(token && { authorization: `Bearer ${token}` }), 'x-client-ip': `words-${run}` } });
  const person = async (name: string, role: string) => {
    const s = (await call('POST', '/api/auth/signup', { email: `${name}-${run}@divan.test`, password: 'pass-word-1' })).json();
    await pool.query('UPDATE users SET role = $2 WHERE id = $1', [s.user.id, role]);
    return { ...s, id: s.user.id };
  };
  const admin = await person('wadmin', 'admin'), l2 = await person('wl2', 'mod-l2');
  try {
    assert.equal((await call('POST', '/api/mod/word', { word }, l2.token)).statusCode, 403, 'no dictionary grant yet');
    await call('POST', `/api/admin/users/${l2.id}/grants`, { scope: 'all', content: ['dictionary'], actions: ['edit', 'create'] }, admin.token);
    assert.equal((await call('POST', '/api/mod/word', { word: 'English' }, l2.token)).statusCode, 400);
    const entry = (await call('POST', '/api/mod/word', { word }, l2.token)).json();
    assert.equal(entry.kind, 'word');
    assert.deepEqual((await call('POST', '/api/mod/word', { word }, l2.token)).json(), entry, 'one entry per word');
    const id = (await call('POST', `/api/mod/details/word/${entry.id}/draft`, undefined, l2.token)).json().id;
    const rev = (await call('GET', `/api/mod/revisions/${id}`, undefined, l2.token)).json().revision;
    assert.match(rev.content, new RegExp(`^لفظ: ${word}\\nزبان: اردو\\nمعانی:`), 'a new entry starts from the word');
    const text = `لفظ: ${word}\nزبان: اردو\nمعانی:\nآزمائش (azmaish، اسم مؤنث): جانچ، امتحان`;
    assert.equal((await call('POST', `/api/mod/revisions/${id}/save`, { content: `لفظ: ${word}\nزبان: اردو\nمعانی:\n` }, l2.token)).statusCode, 400);
    assert.equal((await call('POST', `/api/mod/revisions/${id}/save`, { content: text, summary: 'نیا لفظ' }, l2.token)).statusCode, 200);
    await pool.query(`UPDATE revisions SET status = 'approved' WHERE id = $1`, [id]);
    assert.deepEqual((await call('POST', `/api/mod/revisions/${id}/publish`, {}, admin.token)).json(), { status: 'published', version: 1 });
    // divan-data has it, as a commit; the sidebar shows it first
    assert.equal(await readFile(join(data, 'divan', 'dictionary', word + '.word'), 'utf8'), text + '\n');
    assert.match(git('log', '-1', '--format=%s'), new RegExp(`لغت: ${word}`));
    const d = await lookup(word);
    assert.equal(d.found, true);
    assert.deepEqual(d.divan.meanings[0], { form: 'آزمائش', tr: 'azmaish', pos: 'اسم مؤنث', text: 'جانچ، امتحان', examples: [] });
    // a rebuilt database gets it back from divan-data
    await pool.query('DELETE FROM divan_words WHERE key = $1', [key(word)]);
    assert.equal(await loadWords(data), 1);
    assert.equal((await lookup(word)).divan.meanings[0].text, 'جانچ، امتحان');
  } finally {
    await pool.query('DELETE FROM revisions WHERE entity = $1 AND entity_id IN (SELECT id FROM divan_words WHERE key = $2)', ['word', key(word)]);
    await pool.query('DELETE FROM divan_words WHERE key = $1', [key(word)]);
    await pool.query('DELETE FROM users WHERE email LIKE $1', [`%-${run}@divan.test`]);
    await rm(data, { recursive: true });
    delete process.env.DIVAN_DATA_DIR;
  }
});
