import { test } from 'node:test';
import assert from 'node:assert/strict';
import { key, kaikkiEntry, glossKeys, urduEntry, definitions, dumpPages } from './dictionary.ts';

test('lookup key: spelling variants across Urdu, Persian and Arabic meet', () => {
  const same = (a: string, b: string) => assert.equal(key(a), key(b), `${a} vs ${b}`);
  same('ناداں', 'نادان');
  same('قِسْمَت', 'قسمت');
  same('نگاہ', 'نگاه');
  same('معنی', 'معنى');
  same('كتاب', 'کتاب');
  assert.notEqual(key('کھ'), key('کہ'), 'do-chashmi heh stays distinct');
  assert.notEqual(key('دیوانے'), key('دیوانی'), 'bari ye stays distinct');
});

test('kaikki entry: glosses, IPA, audio, romanisation, form-of', () => {
  const e = kaikkiEntry({
    word: 'قسمت', pos: 'noun', etymology_text: 'From Arabic',
    senses: [{ glosses: ['fate, destiny'] }, { glosses: ['division'] }, { links: [] }],
    sounds: [{ ipa: '/qɪs.mət̪/' }, { audio: 'x.wav', mp3_url: 'https://upload.wikimedia.org/x.mp3' }, { mp3_url: 'https://evil.example/x.mp3' }],
    forms: [{ form: 'قِسْمَت', tags: ['canonical'] }, { form: 'qismat', tags: ['romanization'] }],
  });
  assert.deepEqual(e, { pos: 'noun', glosses: ['fate, destiny', 'division'], formOf: false, ipa: ['/qɪs.mət̪/'],
    audio: 'https://upload.wikimedia.org/x.mp3', tr: 'qismat', ety: 'From Arabic', synonyms: [] });
  assert.equal(kaikkiEntry({ word: 'x', pos: 'noun', senses: [{ glosses: ['plural of y'], tags: ['form-of'] }] }).formOf, true);
});

test('pivot keys from English glosses', () => {
  assert.deepEqual(glossKeys(['fate, destiny', 'to love (someone)', 'a very long description of something that is not a key']), ['fate', 'destiny', 'love']);
});

test('ur.wiktionary entry: meanings and origin', () => {
  const wt = 'وِصال {وِصال} ([[عربی]])\n\nاسم [[نکرہ]]\n\n==معانی==\n\n1. بھیٹ، ملاقات، [[عاشق و معشوق]] کی محبت۔\n\n==مرکبات==\n\nوِصال کا دِن';
  assert.deepEqual(urduEntry(wt), { defs: ['بھیٹ، ملاقات، عاشق و معشوق کی محبت۔'], origin: 'عربی', links: [] });
});

test('fa./ar.wiktionary definitions: skip etymology, Arabic pointers', () => {
  const fa = '==فارسی==\n===ریشه‌شناسی===\n* [[عربی]]\n# قسمة\n===اسم===\n#بهره، نصیب.\n#:example\n#سرنوشت، تقدیر.\n====برگردان‌ها====\n# x y z';
  assert.deepEqual(definitions(fa, 'fa').defs, ['بهره، نصیب.', 'سرنوشت، تقدیر.']);
  const ar = '== {{اللغة|عربية}} ==\nهل تقصد:\n* [[عِشْق]]\n* [[عَشَقَ]]\n----\n== {{اللغة|أردية}} ==\n# [[x]]';
  assert.deepEqual(definitions(ar, 'ar'), { defs: [], origin: null, links: ['عِشْق', 'عَشَقَ'] });
  assert.deepEqual(definitions('# {{مصدر|عَشِقَ}}.\n# فرط الحب.', 'ar').defs, ['فرط الحب.']);
});

test('dump pages: articles only, entities decoded, redirects skipped', () => {
  const xml = '<page><title>عشق</title><ns>0</ns><revision><text bytes="9">a &amp; b &lt;x&gt;</text></revision></page>' +
    '<page><title>Talk:x</title><ns>1</ns><text>t</text></page><page><title>r</title><ns>0</ns><redirect title="y" /><text>#R</text></page>';
  assert.deepEqual([...dumpPages(xml)], [{ title: 'عشق', text: 'a & b <x>' }]);
});

test('etymology cut never leaves half a surrogate pair', () => {
  const ety = kaikkiEntry({ word: 'x', pos: 'noun', senses: [], etymology_text: 'a'.repeat(299) + '𑀭𑀭' }).ety!;
  assert.ok(ety.isWellFormed());
});

test('ur.wiktionary: English entries give Urdu words; Urdu entries fall back to # lines or prose', async () => {
  const { englishToUrdu, isEnglish } = await import('./dictionary.ts');
  assert.ok(isEnglish('north') && !isEnglish('شمال'));
  assert.deepEqual(englishToUrdu('==انگریزی==\n===صفت===\n{{en-adjective}}\n# [[شمالی]]۔\n# [[شمال]] کی [[جانب]]۔\n#: [[x]]'), ['شمالی', 'شمال', 'جانب']);
  assert.deepEqual(urduEntry('===اسم===\n# [[بہار]] کا [[موسم]]۔').defs, ['بہار کا موسم۔']);
  assert.deepEqual(urduEntry("'''پھوڑی''' اس چادر کو کہتے ہیں جس پر بیٹھتے ہیں۔").defs, ['پھوڑی اس چادر کو کہتے ہیں جس پر بیٹھتے ہیں۔']);
});

test('etymology drops the "Etymology tree" summary', () => {
  const ety = kaikkiEntry({ word: 'x', pos: 'noun', senses: [], etymology_text: 'Etymology tree Arabic قَسَمَ (qasama)bor. Urdu قِسْمَت Borrowed from Classical Persian قِسْمَت (qismat).' }).ety;
  assert.equal(ety, 'Borrowed from Classical Persian قِسْمَت (qismat).');
});
