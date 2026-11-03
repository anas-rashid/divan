import { test, after } from 'node:test';
import assert from 'node:assert/strict';
import { mkdtemp, readFile, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { execFileSync } from 'node:child_process';
import Fastify from 'fastify';
import { authRoutes } from './auth.ts';
import { moderationRoutes } from './moderation.ts';
import { tagRoutes, parseTags, tagsText, loadTags } from './tags.ts';
import { lookup, key } from './dictionary.ts';
import { pool } from './db.ts';

after(() => pool.end());

test('tag text: types, couplets, Urdu digits, repeats, order', () => {
  assert.deepEqual(parseTags('موضوع: عشق\nشعر ۳ › شخصیت: مجنوں\n\nموضوع: عشق'),
    [{ couplet: 0, type: 'موضوع', name: 'عشق' }, { couplet: 3, type: 'شخصیت', name: 'مجنوں' }]);
  assert.match(parseTags('رنگ: سرخ') as string, /قسم/);
  assert.match(parseTags('بس ایک لفظ') as string, /سمجھ/);
  assert.equal(tagsText(parseTags('شعر 2 › مقام: دلی\nٹیگ: مشہور\nموضوع: تصوف') as any), 'موضوع: تصوف\nٹیگ: مشہور\nشعر 2 › مقام: دلی');
  // a phrase in a couplet: kept as written (its Urdu digits too), its own line in the text
  assert.deepEqual(parseTags('شعر ۲ › «دلِ  ناداں ۱» › موضوع: عشق'), [{ couplet: 2, type: 'موضوع', name: 'عشق', phrase: 'دلِ ناداں ۱' }]);
  assert.equal(tagsText(parseTags('شعر 2 › «ب» › موضوع: عشق\nشعر 2 › موضوع: عشق\nشعر 2 › «ا» › موضوع: عشق') as any),
    'شعر 2 › موضوع: عشق\nشعر 2 › «ا» › موضوع: عشق\nشعر 2 › «ب» › موضوع: عشق');
});

test('tagging: tags permission, pipeline, published to divan-data and the site, tag pages, reloaded by the import', async () => {
  const data = await mkdtemp(join(tmpdir(), 'divan-data-'));
  process.env.DIVAN_DATA_DIR = data;
  const git = (...a: string[]) => execFileSync('git', ['-C', data, ...a], { encoding: 'utf8' });
  git('init', '-q'); git('-c', 'user.name=t', '-c', 'user.email=t@t', 'commit', '-q', '--allow-empty', '-m', 'start');
  const app = Fastify();
  authRoutes(app); moderationRoutes(app); tagRoutes(app);
  const run = Date.now();
  const call = (method: string, url: string, body?: object, token?: string) =>
    app.inject({ method: method as any, url, payload: body, headers: { ...(token && { authorization: `Bearer ${token}` }), 'x-client-ip': `tag-${run}` } });
  const person = async (name: string, role: string) => {
    const s = (await call('POST', '/api/auth/signup', { email: `${name}-${run}@divan.test`, password: 'pass-word-1' })).json();
    await pool.query('UPDATE users SET role = $2 WHERE id = $1', [s.user.id, role]);
    return { ...s, id: s.user.id };
  };
  const admin = await person('admin', 'admin'), l1 = await person('l1', 'mod-l1'), l2 = await person('l2', 'mod-l2');
  const cat = (await pool.query(`SELECT id, url FROM categories WHERE url = '/p266/ghazal'`)).rows[0];
  const work = (await pool.query(`SELECT p.id, p.url FROM poems p WHERE p.category_id = $1
    AND EXISTS (SELECT 1 FROM verses v WHERE v.poem_id = p.id AND v.couplet >= 2) ORDER BY p.id LIMIT 1`, [cat.id])).rows[0];
  const saved = (await pool.query('SELECT t.type, t.name, e.entity, e.entity_id, e.couplet, e.phrase FROM entity_tags e JOIN tags t ON t.id = e.tag_id')).rows;
  const word = 'آزمائشٹیگ' + 'ابتثج'[run % 5];
  const second = (await pool.query('SELECT text FROM verses WHERE poem_id = $1 AND couplet = 1 ORDER BY vorder LIMIT 1', [work.id])).rows[0].text;
  const phrase = second.split(' ').slice(0, 2).join(' ');
  try {
    const grant = (m: any, content: string) =>
      pool.query(`INSERT INTO grants (user_id, scope, scope_id, content, actions) VALUES ($1, 'category', $2, $3, '{edit}')`, [m.id, cat.id, [content]]);
    await grant(l2, 'works');
    assert.equal((await call('GET', `/api/mod/can?poem=${work.id}`, undefined, l2.token)).json().tags, false, 'editing text is not tagging');
    assert.equal((await call('POST', `/api/mod/tags/work/${work.id}/draft`, undefined, l2.token)).statusCode, 403);
    await grant(l2, 'tags'); await grant(l1, 'tags');

    // L2 tags the work (whole and its third couplet) and the ghazals' section
    const w = (await call('POST', `/api/mod/tags/work/${work.id}/draft`, undefined, l2.token)).json().id;
    assert.equal((await call('POST', `/api/mod/revisions/${w}/save`, { content: 'رنگ: سرخ' }, l2.token)).statusCode, 400, 'unknown type');
    assert.equal((await call('POST', `/api/mod/revisions/${w}/save`, { content: 'شعر 999 › موضوع: عشق' }, l2.token)).statusCode, 400, 'no such couplet');
    assert.match((await call('POST', `/api/mod/revisions/${w}/save`, { content: 'شعر 2 › «کوئی اور عبارت» › موضوع: عشق' }, l2.token)).json().error, /میں «کوئی اور عبارت» نہیں/, 'a phrase must be in its couplet');
    const tagText = `موضوع: عشق-${run}\nشعر 2 › شخصیت: مجنوں-${run}\nشعر 2 › «${phrase}» › موضوع: عشق-${run}`;
    assert.equal((await call('POST', `/api/mod/revisions/${w}/save`, { content: tagText, summary: 'ٹیگ' }, l2.token)).statusCode, 200);
    const c = (await call('POST', `/api/mod/tags/category/${cat.id}/draft`, undefined, l2.token)).json().id;
    assert.equal((await call('POST', `/api/mod/revisions/${c}/save`, { content: 'شعر 1 › موضوع: عشق' }, l2.token)).statusCode, 400, 'no couplets on a section');
    assert.equal((await call('POST', `/api/mod/revisions/${c}/save`, { content: `صنف: غزل-${run}` }, l2.token)).statusCode, 200);
    for (const id of [w, c]) {
      assert.equal((await call('POST', `/api/mod/revisions/${id}/submit`, {}, l2.token)).json().status, 'submitted');
      assert.equal((await call('POST', `/api/mod/revisions/${id}/approve`, {}, l1.token)).json().status, 'approved');
      assert.deepEqual((await call('POST', `/api/mod/revisions/${id}/publish`, {}, admin.token)).json(), { status: 'published', version: 1 });
    }

    // divan-data holds the tags next to the work; the tag pages list what carries them
    assert.equal(await readFile(join(data, 'divan', work.url.slice(1) + '.tags'), 'utf8'), tagsText(parseTags(tagText) as any) + '\n');
    assert.match(git('log', '-1', '--format=%s'), /^ٹیگ: /);
    const love = (await call('GET', `/api/tag?type=${encodeURIComponent('موضوع')}&name=${encodeURIComponent(`عشق-${run}`)}`)).json();
    assert.deepEqual(love.works.map((x: any) => [x.id, x.whole]), [[work.id, true]]);
    assert.deepEqual(love.works[0].couplets.map((x: any) => [x.couplet, x.phrases]), [[2, [phrase]]], 'the tagged phrase, with its couplet');
    const majnun = (await call('GET', `/api/tag?type=${encodeURIComponent('شخصیت')}&name=${encodeURIComponent(`مجنوں-${run}`)}`)).json();
    assert.equal(majnun.works[0].couplets[0].couplet, 2);
    assert.ok(majnun.works[0].couplets[0].lines.length >= 1, 'the couplet\'s lines');
    const genre = (await call('GET', `/api/tag?type=${encodeURIComponent('صنف')}&name=${encodeURIComponent(`غزل-${run}`)}`)).json();
    assert.deepEqual(genre.categories.map((x: any) => x.id), [cat.id]);
    assert.ok((await call('GET', '/api/tags')).json().some((t: any) => t.name === `عشق-${run}` && t.n === 2), 'used twice: the work and the phrase');

    // a word: site-wide tags permission; its tags next to its dictionary entry, on the tag page and in the sidebar
    assert.equal((await call('POST', '/api/mod/word', { word, for: 'tags' }, l2.token)).statusCode, 403, 'a section\'s grant is not site-wide');
    await pool.query(`INSERT INTO grants (user_id, scope, content, actions) VALUES ($1, 'all', '{tags}', '{edit}')`, [l2.id]);
    const wid = (await call('POST', '/api/mod/word', { word, for: 'tags' }, l2.token)).json().id;
    const t = (await call('POST', `/api/mod/tags/word/${wid}/draft`, undefined, l2.token)).json().id;
    assert.equal((await call('POST', `/api/mod/revisions/${t}/save`, { content: 'شعر 1 › موضوع: عشق' }, l2.token)).statusCode, 400, 'a word has no couplets');
    assert.equal((await call('POST', `/api/mod/revisions/${t}/save`, { content: `موضوع: تصوف-${run}` }, l2.token)).statusCode, 200);
    await call('POST', `/api/mod/revisions/${t}/submit`, {}, l2.token);
    assert.equal((await call('POST', `/api/mod/revisions/${t}/approve`, {}, l1.token)).statusCode, 403, 'a section\'s reviewer does not review words');
    await call('POST', `/api/mod/revisions/${t}/approve`, {}, admin.token);
    assert.deepEqual((await call('POST', `/api/mod/revisions/${t}/publish`, {}, admin.token)).json(), { status: 'published', version: 1 });
    assert.equal(await readFile(join(data, 'divan', 'dictionary', word + '.tags'), 'utf8'), `موضوع: تصوف-${run}\n`);
    assert.deepEqual((await call('GET', `/api/tag?type=${encodeURIComponent('موضوع')}&name=${encodeURIComponent(`تصوف-${run}`)}`)).json().words, [word]);
    assert.deepEqual((await lookup(word)).tags, [{ type: 'موضوع', name: `تصوف-${run}` }], 'in the word sidebar');

    // the import rebuilds the site's tags from divan-data (the word's too, its row made again if need be)
    await pool.query('DELETE FROM entity_tags');
    await pool.query('DELETE FROM divan_words WHERE key = $1', [key(word)]);
    assert.equal(await loadTags(data), 3);
    assert.equal((await lookup(word)).tags.length, 1);
    assert.equal((await call('GET', `/api/tag?type=${encodeURIComponent('صنف')}&name=${encodeURIComponent(`غزل-${run}`)}`)).json().categories.length, 1);

    // removing every tag deletes the file
    const again = (await call('POST', `/api/mod/tags/work/${work.id}/draft`, undefined, admin.token)).json().id;
    await call('POST', `/api/mod/revisions/${again}/save`, { content: '' }, admin.token);
    assert.deepEqual((await call('POST', `/api/mod/revisions/${again}/submit`, {}, admin.token)).json(), { status: 'published', version: 2 });
    await assert.rejects(readFile(join(data, 'divan', work.url.slice(1) + '.tags'), 'utf8'));
  } finally {
    // the site's tags as they were
    await pool.query('DELETE FROM entity_tags');
    for (const t of saved) {
      const id = (await pool.query('INSERT INTO tags (type, name) VALUES ($1, $2) ON CONFLICT (type, name) DO UPDATE SET name = EXCLUDED.name RETURNING id', [t.type, t.name])).rows[0].id;
      await pool.query('INSERT INTO entity_tags (tag_id, entity, entity_id, couplet, phrase) VALUES ($1, $2, $3, $4, $5) ON CONFLICT DO NOTHING', [id, t.entity, t.entity_id, t.couplet, t.phrase]);
    }
    await pool.query('DELETE FROM tags t WHERE NOT EXISTS (SELECT 1 FROM entity_tags e WHERE e.tag_id = t.id)');
    await pool.query(`DELETE FROM revisions WHERE author_email LIKE $1`, [`%-${run}@divan.test`]);
    await pool.query('DELETE FROM divan_words WHERE key = $1', [key(word)]);
    await pool.query('DELETE FROM users WHERE email LIKE $1', [`%-${run}@divan.test`]);
    await rm(data, { recursive: true, force: true });
    await app.close();
  }
});
