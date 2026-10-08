import { test } from 'node:test';
import assert from 'node:assert/strict';
import { forms, urduEntry, pronunciations } from './dictionary.ts';

test('spelling forms: diacritics dropped, final noon ghunna tried as noon', () => {
  assert.deepEqual(forms('ناداں'), ['ناداں', 'نادان']);
  assert.deepEqual(forms('وِصال'), ['وِصال', 'وصال']);
});

test('ur.wiktionary entry: meanings and origin', () => {
  const wt = "وِصال {وِصال} ([[عربی]])\n\nاسم [[نکرہ]]\n\n==معانی==\n\n1. بھیٹ، ملاقات، [[عاشق و معشوق]] کی محبت۔\n\n==مرکبات==\n\nوِصال کا دِن";
  assert.deepEqual(urduEntry(wt), { meanings: ['بھیٹ، ملاقات، عاشق و معشوق کی محبت۔'], origin: 'عربی' });
});

test('en.wiktionary HTML: IPA, transliteration and audio per language', () => {
  const html = '<h2 data-mw-wikitext="" id="Persian">Persian</h2><span class="IPA nowrap">/qis.ˈmat/</span><span class="IPA">-at</span>' +
    '<span lang="fa-Latn" class="headword-tr manual-tr tr Latn" dir="ltr">qismat</span><audio><source src="//upload.wikimedia.org/a/b.ogg"></audio>' +
    '<h2 id="Urdu">Urdu</h2><span class="IPA nowrap">/qɪs.mət̪/</span><span lang="ur-Latn" class="headword-tr tr Latn">qismat</span>';
  assert.deepEqual(pronunciations(html), {
    fa: { ipa: ['/qis.ˈmat/'], tr: 'qismat', audio: 'https://upload.wikimedia.org/a/b.ogg' },
    ur: { ipa: ['/qɪs.mət̪/'], tr: 'qismat', audio: null },
  });
});

test('fa./ar.wiktionary definitions: skip etymology, follow Arabic pointers', async () => {
  const { definitions, formsIn } = await import('./dictionary.ts');
  const fa = '==فارسی==\n===ریشه‌شناسی===\n* [[عربی]]\n# قسمة\n===اسم===\n#بهره، نصیب.\n#:example\n#سرنوشت، تقدیر.\n====برگردان‌ها====\n# x y z';
  assert.deepEqual(definitions(fa, 'fa').defs, ['بهره، نصیب.', 'سرنوشت، تقدیر.']);
  const ar = '== {{اللغة|عربية}} ==\nهل تقصد:\n* [[عِشْق]]\n* [[عَشَقَ]]\n----\n== {{اللغة|أردية}} ==\n# [[x]]';
  assert.deepEqual(definitions(ar, 'ar'), { defs: [], links: ['عِشْق', 'عَشَقَ'] });
  assert.deepEqual(definitions('# {{مصدر|عَشِقَ}}.\n# فرط الحب.', 'ar').defs, ['فرط الحب.']);
  assert.deepEqual(formsIn('fa', 'نگاہ'), ['نگاه']);
  assert.deepEqual(formsIn('ar', 'معنی'), ['معني', 'معنى']);
});
