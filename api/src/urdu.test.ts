import { test } from 'node:test';
import assert from 'node:assert/strict';
import { normalise, likePatterns, highlighter, tsQuery } from './urdu.ts';

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

test('full-text queries: every word as a word start, a quoted phrase in order, words quoted', () => {
  assert.equal(tsQuery(''), null);
  const q = tsQuery('دلِ ناداں')!;
  assert.equal(q.all, "'دل':* & 'ناداں':*", 'normalised (no kasra), each word as a word start');
  assert.equal(q.phrase, "'دل' <-> 'ناداں'");
  assert.equal(q.whole, "'دل' & 'ناداں'");
  assert.equal(tsQuery('"دل ہی تو ہے"')!.all, "'دل' <-> 'ہی' <-> 'تو' <-> 'ہے'", 'a quoted phrase: its words, in order');
  assert.equal(tsQuery("a'b & !c")!.all, "'ab':* & '&':* & 'c':*", 'nothing in a word is tsquery syntax (quoted)');
  assert.equal(tsQuery("x'y")!.all, "'xy':*", 'quotes go with the punctuation');
});
