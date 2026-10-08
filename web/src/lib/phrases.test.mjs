// node web/src/lib/phrases.test.mjs (plain assert; the API has the main test suite)
import assert from 'node:assert/strict';
const { phraseParts, phraseRe } = await import('./phrases.ts');
const lines = ['یہ مسائل تصوف یہ ترا بیان غالبؔ', 'تجھے ہم ولی سمجھتے جو نہ بادہ خوار ہوتا'];
assert.deepEqual(phraseParts(lines, 'مسائل تصوف'), ['مسائل تصوف', null]);
assert.deepEqual(phraseParts(lines, 'بیان غالب'), ['بیان غالب', null], 'takhallus sign ignored');
assert.ok(phraseRe('بیان غالب').test(lines[0]));
assert.deepEqual(phraseParts(lines, 'بیان غالب تجھے ہم'), ['بیان غالب', 'تجھے ہم'], 'across the misras');
assert.deepEqual(phraseParts(lines, 'کہیں نہیں'), [null, null]);
console.log('ok');
