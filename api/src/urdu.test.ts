import { test } from 'node:test';
import assert from 'node:assert/strict';
import { normalise, likePatterns, highlighter } from './urdu.ts';

test('Urdu variants and diacritics normalise to the same form', () => {
  const same = (a: string, b: string) => assert.equal(normalise(a), normalise(b), `${a} vs ${b}`);
  same('دلِ ناداں', 'دل ناداں'); // izafat kasra
  same('کوئی امید', 'كوئي اميد'); // arabic kaf/yeh
  same('ہوئی', 'هوئی'); // arabic heh vs heh goal
  same('نالۂ', 'نالہ'); // heh goal with hamza
  same('دیکھا۔', 'دیکھا'); // urdu full stop
  assert.notEqual(normalise('کھ'), normalise('کہ'), 'do-chashmi heh must stay distinct');
});

test('search patterns: words, phrases, escaping, empty', () => {
  assert.deepEqual(likePatterns('دل ناداں'), ['%دل%', '%ناداں%']);
  assert.deepEqual(likePatterns('"دل ناداں"'), ['%دل ناداں%']);
  assert.deepEqual(likePatterns('50%_x'), ['%50\\%\\_x%']);
  assert.deepEqual(likePatterns('  '), []);
});

test('highlighter finds terms in original text despite variants and diacritics', () => {
  const marks = (q: string, text: string) => text.split(highlighter(q)!).filter((_, i) => i % 2);
  assert.deepEqual(marks('دل ناداں', 'دلِ ناداں تجھے ہوا کیا ہے'), ['دلِ', 'ناداں']);
  assert.deepEqual(marks('"دل ناداں"', 'دلِ ناداں تجھے'), ['دلِ ناداں']);
  assert.deepEqual(marks('کوئی', 'كوئي اميد بر نہيں آتی'), ['كوئي']);
  assert.deepEqual(marks('نالہ', 'نالۂ دل'), ['نالۂ']);
  assert.deepEqual(marks('کہ', 'کچھ کھ کہ'), ['کہ']); // do-chashmi stays distinct
  assert.equal(highlighter('  '), null);
});
