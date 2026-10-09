import { test, after } from 'node:test';
import assert from 'node:assert/strict';
import { mkdtemp, readFile, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { execFileSync } from 'node:child_process';
import Fastify from 'fastify';
import JSZip from 'jszip';
import { authRoutes } from './auth.ts';
import { moderationRoutes } from './moderation.ts';
import { ebookRoutes, archiveId, parseMeta, metaText } from './ebooks.ts';
import { pool } from './db.ts';

after(() => pool.end());

test('e-book details and archive.org links', () => {
  assert.deepEqual(parseMeta(metaText({ title: 'کلیات اقبال', source: 'ریختہ نہیں', licence: 'عوامی ملکیت' })),
    { title: 'کلیات اقبال', source: 'ریختہ نہیں', licence: 'عوامی ملکیت', note: '' });
  assert.match(parseMeta('ماخذ: کچھ') as string, /عنوان/);
  assert.equal(archiveId('https://archive.org/details/BaangEDara/page/n5'), 'BaangEDara');
  assert.equal(archiveId('kulliyat-e-iqbal_202001'), 'kulliyat-e-iqbal_202001');
  assert.equal(archiveId('https://example.com/x'), null);
});

test('e-books: upload permission, file kinds, pipeline, published list and files, divan-data record', async () => {
  const data = await mkdtemp(join(tmpdir(), 'divan-data-')), files = await mkdtemp(join(tmpdir(), 'divan-files-'));
  Object.assign(process.env, { DIVAN_DATA_DIR: data, DIVAN_FILES_DIR: files });
  const git = (...a: string[]) => execFileSync('git', ['-C', data, ...a], { encoding: 'utf8' });
  git('init', '-q'); git('-c', 'user.name=t', '-c', 'user.email=t@t', 'commit', '-q', '--allow-empty', '-m', 'start');
  const app = Fastify();
  authRoutes(app); moderationRoutes(app); ebookRoutes(app);
  const run = Date.now();
  const call = (method: string, url: string, body?: any, token?: string, type?: string) => app.inject({
    method: method as any, url, payload: body,
    headers: { ...(token && { authorization: `Bearer ${token}` }), ...(type && { 'content-type': type }), 'x-client-ip': `eb-${run}` } });
  const upload = (name: string, bytes: Buffer, token: string, poet = 238) =>
    call('POST', `/api/mod/ebooks/upload?poet=${poet}&name=${encodeURIComponent(name)}`, bytes, token, 'application/octet-stream');
  const person = async (name: string, role: string) => {
    const s = (await call('POST', '/api/auth/signup', { email: `${name}-${run}@divan.test`, password: 'pass-word-1' })).json();
    await pool.query('UPDATE users SET role = $2 WHERE id = $1', [s.user.id, role]);
    return { ...s, id: s.user.id };
  };
  const admin = await person('admin', 'admin'), l1 = await person('l1', 'mod-l1'), l2 = await person('l2', 'mod-l2');
  const pdf = Buffer.from('%PDF-1.4\n% آزمائشی کتاب ' + run + '\n%%EOF\n');
  try {
    assert.equal((await upload('a.pdf', pdf, l2.token)).statusCode, 403, 'needs the ebooks permission');
    for (const m of [l2, l1]) await pool.query(`INSERT INTO grants (user_id, scope, scope_id, content, actions) VALUES ($1, 'poet', 238, '{ebooks}', '{create}')`, [m.id]);
    assert.equal((await upload('a.pdf', pdf, l2.token, 266)).statusCode, 403, 'only for the granted poet');

    // kinds: PDF, EPUB (by its mimetype entry), .docx made into text; anything else refused
    const up = (await upload('کتاب.pdf', pdf, l2.token)).json();
    assert.equal(up.kind, 'pdf'); assert.match(up.file, /^[0-9a-f]{64}\.pdf$/);
    assert.equal((await upload('again.pdf', pdf, l2.token)).json().file, up.file, 'same file, same name (kept once)');
    const epub = new JSZip(); epub.file('mimetype', 'application/epub+zip', { compression: 'STORE' }); epub.file('OEBPS/a.xhtml', '<p>ا</p>');
    assert.equal((await upload('b.epub', await epub.generateAsync({ type: 'nodebuffer' }), l2.token)).json().kind, 'epub');
    const docx = new JSZip();
    docx.file('[Content_Types].xml', '<?xml version="1.0"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/></Types>');
    docx.file('_rels/.rels', '<?xml version="1.0"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="r1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/></Relationships>');
    docx.file('word/document.xml', '<?xml version="1.0"?><w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:body><w:p><w:r><w:t>دل ہی تو ہے نہ سنگ و خشت</w:t></w:r></w:p></w:body></w:document>');
    const d = (await upload('c.docx', await docx.generateAsync({ type: 'nodebuffer' }), l2.token)).json();
    assert.equal(d.kind, 'text'); assert.match(d.file, /\.txt$/);
    assert.equal((await upload('x.exe', Buffer.from('MZ....'), l2.token)).statusCode, 415);

    // the e-book and its draft; not listed and not served until approved
    assert.equal((await call('POST', '/api/mod/ebooks', { poet: 238, file: up.file }, l2.token)).statusCode, 400, 'a title is required');
    const made = (await call('POST', '/api/mod/ebooks', { poet: 238, file: up.file, title: `بانگ درا ${run}`, source: 'archive.org', licence: 'عوامی ملکیت' }, l2.token)).json();
    assert.ok(made.id && made.revision);
    assert.equal((await call('GET', `/api/ebook/${made.id}/file`)).statusCode, 404, 'not public yet');
    assert.equal((await call('GET', `/api/ebook/${made.id}/file`, undefined, l1.token)).statusCode, 200, 'reviewers can open it');
    assert.equal((await call('GET', '/api/mod/queue', undefined, l2.token)).json().mine.some((r: any) => r.id === made.revision), true, 'a new upload is a change');
    assert.equal((await call('POST', `/api/mod/revisions/${made.revision}/submit`, {}, l2.token)).json().status, 'submitted');
    assert.equal((await call('POST', `/api/mod/revisions/${made.revision}/approve`, {}, l1.token)).json().status, 'approved');
    assert.deepEqual((await call('POST', `/api/mod/revisions/${made.revision}/publish`, {}, admin.token)).json(), { status: 'published', version: 1 });

    assert.deepEqual((await call('GET', '/api/ebooks?poet=238')).json().map((b: any) => b.title), [`بانگ درا ${run}`]);
    const file = await call('GET', `/api/ebook/${made.id}/file`);
    assert.equal(file.statusCode, 200); assert.equal(file.headers['content-type'], 'application/pdf'); assert.deepEqual(file.rawPayload, pdf);
    const record = await readFile(join(data, 'divan', 'p238', 'ebooks', `${made.id}.txt`), 'utf8');
    assert.match(record, new RegExp(`عنوان: بانگ درا ${run}`)); assert.match(record, new RegExp(up.file));
    assert.match(git('log', '-1', '--format=%s'), /^ای بک: /);
    assert.ok((await pool.query(`SELECT 1 FROM ebooks WHERE id = $1 AND search_text LIKE '%بانگ درا%'`, [made.id])).rowCount, 'indexed for search');

    // a text book serves its text; an archive.org item needs a real id
    const t = (await call('POST', '/api/mod/ebooks', { poet: 238, file: d.file, title: 'ٹیکسٹ' }, admin.token)).json();
    assert.match((await call('GET', `/api/ebook/${t.id}`, undefined, admin.token)).json().text, /دل ہی تو ہے/);
    assert.equal((await call('POST', '/api/mod/ebooks', { poet: 238, archive: 'https://example.com/x', title: 'x' }, admin.token)).statusCode, 400);
    const a = (await call('POST', '/api/mod/ebooks', { poet: 238, archive: 'https://archive.org/details/BaangEDara', title: 'آرکائیو' }, admin.token)).json();
    assert.equal((await call('GET', `/api/ebook/${a.id}`, undefined, admin.token)).json().archive_id, 'BaangEDara');
  } finally {
    await pool.query(`DELETE FROM ebooks WHERE created_by IN (SELECT id FROM users WHERE email LIKE $1)`, [`%-${run}@divan.test`]);
    await pool.query(`DELETE FROM revisions WHERE author_email LIKE $1`, [`%-${run}@divan.test`]);
    await pool.query('DELETE FROM users WHERE email LIKE $1', [`%-${run}@divan.test`]);
    await rm(data, { recursive: true, force: true }); await rm(files, { recursive: true, force: true });
    await app.close();
  }
});
