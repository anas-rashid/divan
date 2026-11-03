import { test } from 'node:test';
import assert from 'node:assert/strict';
import JSZip from 'jszip';
import { fromDocx, fromOdt, fromText, decodeText, toDivanText, clean } from './importdoc.ts';
import { parse, toVerses } from './divantext.ts';

const docx = async (body: string, footnotes = '') => {
  const z = new JSZip();
  z.file('word/document.xml', `<?xml version="1.0"?><w:document xmlns:w="w"><w:body>${body}</w:body></w:document>`);
  if (footnotes) z.file('word/footnotes.xml', `<w:footnotes xmlns:w="w">${footnotes}</w:footnotes>`);
  return z.generateAsync({ type: 'uint8array', compression: 'DEFLATE' });
};
const p = (text: string, style = '') => `<w:p>${style ? `<w:pPr><w:pStyle w:val="${style}"/></w:pPr>` : ''}<w:r><w:t xml:space="preserve">${text}</w:t></w:r></w:p>`;
const verse = (d: string) => toVerses(parse(d)).map((v) => [v.Position, v.Text]);

test('docx: a heading, couplets (one misra per paragraph, blank lines between), a line-break couplet, a footnote', async () => {
  const body = p('غزل', 'Heading1') + p('دلِ ناداں تجھے ہوا کیا ہے') + p('آخر اس درد کی دوا کیا ہے') + '<w:p/>'
    + `<w:p><w:r><w:t>ہم ہیں مشتاق اور وہ بیزار</w:t><w:br/><w:t>یا الٰہی یہ ماجرا کیا ہے</w:t><w:footnoteReference w:id="1"/></w:r></w:p>`;
  const d = toDivanText(await fromDocx(await docx(body, '<w:footnote w:id="0"><w:p><w:r><w:t>-</w:t></w:r></w:p></w:footnote><w:footnote w:id="1"><w:p><w:r><w:t>نسخۂ حمیدیہ</w:t></w:r></w:p></w:footnote>')));
  assert.equal(d, '== غزل ==\n\n<poem>\nدلِ ناداں تجھے ہوا کیا ہے\nآخر اس درد کی دوا کیا ہے\n\nہم ہیں مشتاق اور وہ بیزار\nیا الٰہی یہ ماجرا کیا ہے<ref>نسخۂ حمیدیہ</ref>\n</poem>\n');
  assert.deepEqual(toVerses(parse(d)).filter((v) => v.Notes).map((v) => v.Notes), [['نسخۂ حمیدیہ']]);
});

test('a ghazal typed straight through is split into couplets; same-size runs are stanzas; long lines are prose', () => {
  const ghazal = fromText('ایک\nدو\nتین\nچار');
  assert.deepEqual(verse(toDivanText(ghazal)).map(([p]) => p), ['Right', 'Left', 'Right', 'Left']);
  const musaddas = fromText(['ا', 'ب', 'ج', 'د', 'ہ', 'و', '', 'ز', 'ح', 'ط', 'ی', 'ک', 'ل'].join('\n'));
  assert.deepEqual(verse(toDivanText(musaddas)).map(([p]) => p), Array(12).fill('Single'), 'two بند of six: their lines');
  assert.equal(parse(toDivanText(musaddas)).blocks.map((b) => b.type).join(), 'stanza,stanza');
  const prose = fromText('یہ ایک لمبا نثری پیراگراف ہے جو ایک مصرعے سے کہیں زیادہ طویل ہے اور اس میں کئی جملے ہیں، اس لیے یہ نثر ہے۔\n\nدوسرا پیراگراف بھی نثر ہے کیونکہ یہ بھی بہت لمبا ہے اور اس میں بہت سے الفاظ ہیں جو ایک مصرعے میں نہیں سماتے۔');
  assert.equal(parse(toDivanText(prose)).blocks.map((b) => b.type).join(), 'para,para');
  const tab = toDivanText(fromText('پہلا مصرع\tدوسرا مصرع'));
  assert.deepEqual(verse(tab), [['Right', 'پہلا مصرع'], ['Left', 'دوسرا مصرع']], 'a couplet on one line, split at the tab');
});

test('odt: headings by outline level, line breaks, spaces, a note', async () => {
  const z = new JSZip();
  z.file('content.xml', `<office:document-content><office:body><office:text>
    <text:h text:outline-level="1">باب اول</text:h><text:h text:outline-level="2">فصل</text:h>
    <text:p>پہلا<text:s text:c="2"/>مصرع<text:line-break/>دوسرا مصرع<text:note text:note-class="footnote"><text:note-citation>1</text:note-citation><text:note-body><text:p>حاشیہ</text:p></text:note-body></text:note></text:p>
  </office:text></office:body></office:document-content>`);
  const d = toDivanText(await fromOdt(await z.generateAsync({ type: 'uint8array', compression: 'DEFLATE' })));
  assert.equal(d, '== باب اول ==\n\n=== فصل ===\n\n<poem>\nپہلا مصرع\nدوسرا مصرع<ref>حاشیہ</ref>\n</poem>\n');
});

test('clean-up and text encodings: Arabic forms to Urdu, invisible marks out; UTF-8, UTF-16, Windows-1256', () => {
  assert.equal(clean('‏كيا  ہے ٢'), 'کیا ہے ۲');
  const s = 'دیوان';
  assert.equal(decodeText(new TextEncoder().encode(s)), s);
  const le = new Uint8Array([0xff, 0xfe, ...[...s].flatMap((c) => [c.charCodeAt(0) & 255, c.charCodeAt(0) >> 8])]);
  assert.equal(decodeText(le), s);
  assert.equal(decodeText(new Uint8Array([0xe3, 0xc7])), 'ما', 'the Windows Arabic code page');
});
