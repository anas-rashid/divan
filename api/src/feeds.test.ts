import { test, after } from 'node:test';
import assert from 'node:assert/strict';
import Fastify from 'fastify';
import { feedRoutes } from './feeds.ts';
import { pool } from './db.ts';

after(() => pool.end());

test('sitemap and feed: every public page; a published version appears with its date, summary, author and lines', async () => {
  const app = Fastify(); feedRoutes(app);
  const work = (await pool.query(`SELECT id, url FROM poems WHERE url LIKE '/p266/ghazal/%' ORDER BY id LIMIT 1`)).rows[0];
  const when = new Date(Date.now() + 60_000).toISOString(); // newer than anything else, so it comes first
  const rev = (await pool.query(
    `INSERT INTO revisions (entity, entity_id, version, base_version, content, status, author_email, summary, published_at, credits)
     VALUES ('work', $1, 99, 98, '', 'published', 'feed-test@divan.test', 'آزمائشی ترمیم', $2, $3) RETURNING id`,
    [work.id, when, { by: 'آزمائشی موڈریٹر', publishedBy: 'ایڈمن' }])).rows[0].id;
  try {
    const s = (await app.inject('/api/sitemap')).json();
    assert.ok(s.poets.length > 300 && s.categories.length > 500 && s.works.length > 10000, 'all the public pages');
    assert.ok(s.poets.includes('/p266'));
    assert.equal(new Date(s.works.find((w: any) => w.url === work.url).updated).toISOString(), when, "a work's date is its latest version");

    const feed = (await app.inject('/api/feed?limit=5')).json();
    assert.ok(feed.length >= 1 && feed.length <= 5);
    const first = feed[0];
    assert.deepEqual([first.kind, first.url, first.version, first.summary, first.by], ['work', work.url, 99, 'آزمائشی ترمیم', 'آزمائشی موڈریٹر']);
    assert.ok(first.lines.length >= 1, 'the opening lines');
    assert.equal((await app.inject('/api/feed?limit=999')).json().length <= 100, true, 'at most 100');
  } finally {
    await pool.query('DELETE FROM revisions WHERE id = $1', [rev]);
    await app.close();
  }
});
