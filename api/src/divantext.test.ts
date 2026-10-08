import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, existsSync } from 'node:fs';
import { spawnSync } from 'node:child_process';
import { parse, toVerses, toTEI, words, inline } from './divantext.ts';

const doc = (f: string) => readFileSync(new URL(`../../docs/content-model/${f}`, import.meta.url), 'utf8');
const ghazal = parse(doc('ghalib-ghazal.dtx')), prose = parse(doc('sir-syed-azadi-e-rai.dtx'));

test('a ghazal: data points, couplets, labels, dictionary links, footnote, variant', () => {
  assert.equal(ghazal.meta['شاعر'], 'مرزا غالب');
  assert.equal(ghazal.meta['ردیف'], 'ہوتا');
  assert.equal(ghazal.blocks.length, 11);
  assert.ok(ghazal.blocks.every((b) => b.type === 'couplet'), 'two lines = a couplet');
  const first = ghazal.blocks[0] as any, fourth = ghazal.blocks[3] as any, sixth = ghazal.blocks[5] as any;
  assert.equal(first.label, 'مطلع');
  assert.deepEqual(first.lines[0].words, [{ lemma: 'وصال', shown: 'وصال' }]);
  assert.equal(first.lines[0].text, 'یہ نہ تھی ہماری قسمت کہ وصال یار ہوتا', 'markup removed from the text');
  assert.equal(fourth.lines[0].notes.length, 1);
  assert.deepEqual(sixth.lines[0].variants, [{ shown: 'تھمتا', others: ['رکتا'], source: 'مثال کے لیے' }]);
});

test('the same verses as the current export (the site would show the same text)', { skip: !existsSync(new URL('../../../divan-data/poets/p266/ghazal', import.meta.url)) }, () => {
  const dir = new URL('../../../divan-data/poets/p266/ghazal/', import.meta.url);
  const cat = JSON.parse(readFileSync(new URL('_cat.json', dir), 'utf8'));
  const ref = cat.Poems.find((p: any) => p.Title === 'یہ نہ تھی ہماری قسمت کہ وصال یار ہوتا');
  const exported = JSON.parse(readFileSync(new URL(`../../../divan-data/poets${ref.FullUrl}.json`, import.meta.url), 'utf8')).Verses;
  const pick = (v: any) => [v.Position, v.Text, v.CoupletIndex];
  assert.deepEqual(toVerses(ghazal).map(pick), exported.map(pick));
});

test('Wikisource markup reads as-is ({{header}}, <poem align>)', () => {
  const ws = parse(`{{header\n | title = غزل\n | author = مرزا غالب\n}}\n\n<poem align="right" dir="rtl">\nپہلا مصرع\nدوسرا مصرع\n\nاکیلا مصرع\n</poem>`);
  assert.equal(ws.meta.author, 'مرزا غالب');
  assert.deepEqual(ws.blocks.map((b) => b.type), ['couplet', 'line']);
});

test('prose: chapter heading, paragraphs, footnote', () => {
  assert.equal(prose.meta['مصنف'], 'سید احمد خان');
  assert.deepEqual(prose.blocks.map((b) => b.type), ['heading', 'para', 'para', 'para']);
  assert.equal((prose.blocks[1] as any).line.notes.length, 1);
  assert.deepEqual(toVerses(prose).map((v) => v.Position), ['Heading', 'Paragraph', 'Paragraph', 'Paragraph']);
  assert.equal(toVerses(parse('== باب ==\n\n=== فصل ===\n\nمتن')).map((v) => v.Level ?? 0).join(), '2,3,0', 'chapter and sub-heading levels');
});

test('stanzas and single lines', () => {
  const d = parse('<poem>\nا\nب\nج\nد\nہ\n\nو\n</poem>');
  assert.deepEqual(d.blocks.map((b) => b.type), ['stanza', 'line']);
});

test('what is a word: spaces split, a ZWNJ keeps a compound, punctuation and ؔ are not part of words', () => {
  assert.deepEqual(words('یہ مسائل تصوف، یہ ترا بیان غالبؔ!'), ['یہ', 'مسائل', 'تصوف', 'یہ', 'ترا', 'بیان', 'غالب']);
  assert.deepEqual(words('بے‌ثبوت بات۔'), ['بے‌ثبوت', 'بات']);
  assert.deepEqual(inline('[[لغت:دیوانہ|دیوانے]] ہیں').words, [{ lemma: 'دیوانہ', shown: 'دیوانے' }]);
});

test('TEI export is well-formed XML with couplets as <lg type="sher"> of two <l>', () => {
  const tei = toTEI(ghazal);
  assert.match(tei, /<lg type="sher" n="1" ana="#matla"><l n="1">یہ نہ تھی ہماری قسمت کہ <w lemma="وصال">وصال<\/w> یار ہوتا<\/l>/);
  assert.match(tei, /<app><lem>تھمتا<\/lem><rdg source="مثال کے لیے">رکتا<\/rdg><\/app>/);
  const lint = spawnSync('xmllint', ['--noout', '-'], { input: tei + '\n' + '' });
  if (!lint.error) assert.equal(lint.status, 0, String(lint.stderr));
  if (!lint.error) assert.equal(spawnSync('xmllint', ['--noout', '-'], { input: toTEI(prose) }).status, 0);
});
