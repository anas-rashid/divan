import { test } from 'node:test';
import assert from 'node:assert/strict';
import { cleanRich, richHtml, plainText } from './richtext.ts';

const p = (...content: any[]) => ({ type: 'paragraph', content });
const t = (text: string, ...marks: any[]) => ({ type: 'text', text, ...(marks.length && { marks }) });

test('rich text: only the allowed nodes and marks, safe links, escaped HTML', () => {
  const doc = cleanRich({ type: 'doc', content: [
    p(t('غالب '), t('کا', { type: 'bold' }), t(' شعر', { type: 'italic' }), t('<script>alert(1)</script>')),
    p(t('لنک', { type: 'link', attrs: { href: '/p266/ghazal/sh1' } }), t(' برا', { type: 'link', attrs: { href: 'javascript:alert(1)' } }),
      t(' باہر', { type: 'link', attrs: { href: 'https://ur.wikisource.org/wiki/x' } })),
    { type: 'bulletList', content: [{ type: 'listItem', content: [p(t('ایک'))] }] },
    { type: 'image', attrs: { src: 'x' } }, { type: 'iframe', content: [p(t('نہیں'))] },
    p(t('x', { type: 'textStyle', attrs: { style: 'color:red' } }), { type: 'hardBreak' }, t('y')),
  ] })!;
  const html = richHtml(doc);
  assert.equal(html, '<p>غالب <strong>کا</strong><em> شعر</em>&lt;script&gt;alert(1)&lt;/script&gt;</p>'
    + '<p><a href="/p266/ghazal/sh1" rel="nofollow ugc noopener">لنک</a> برا<a href="https://ur.wikisource.org/wiki/x" rel="nofollow ugc noopener"> باہر</a></p>'
    + '<ul><li><p>ایک</p></li></ul><p>x<br>y</p>');
  assert.ok(!/javascript|<script|iframe|style=|src=/.test(html));
  assert.match(plainText(doc), /غالب کا شعر/);
  assert.equal(cleanRich({ type: 'doc', content: [p(t('   '))] }), null, 'empty');
  assert.equal(cleanRich('<p>html</p>'), null, 'not the editor JSON');
  assert.equal(cleanRich({ type: 'doc', content: [p(t('ا'.repeat(20)))] }, 5)!.content[0].content[0].text.length, 5, 'length capped');
  assert.equal(cleanRich({ type: 'doc', content: [p(t('x', { type: 'link', attrs: { href: '//evil.example' } }))] })!.content[0].content[0].marks, undefined, 'protocol-relative is not this site');
});
