import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdtemp, readFile, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { writeOwned } from './owned.ts';

test('a republished work keeps its source and edition fields; a new value replaces one', async () => {
  const dir = await mkdtemp(join(tmpdir(), 'divan-owned-'));
  try {
    const text = (t: string) => `{{دیوان\n| عنوان = غزل\n}}\n<poem>\n${t}\nدوسرا مصرع\n</poem>\n`;
    await writeOwned(dir, 'p1/ghazal/sh1', text('پہلا مصرع'), { by: 'دیوان' }, { SourceName: 'نسخۂ اردو ویب', SourceUrl: 'https://www.urduweb.org/mehfil/', RadifLetter: 'ا' });
    await writeOwned(dir, 'p1/ghazal/sh1', text('پہلا مصرع، درست'), { by: 'موڈریٹر' });  // a moderator's edit: no extra
    const j = JSON.parse(await readFile(join(dir, 'divan/p1/ghazal/sh1.json'), 'utf8'));
    assert.deepEqual([j.SourceName, j.SourceUrl, j.RadifLetter, j.Edited.by, j.Verses[0].Text],
      ['نسخۂ اردو ویب', 'https://www.urduweb.org/mehfil/', 'ا', 'موڈریٹر', 'پہلا مصرع، درست']);
    await writeOwned(dir, 'p1/ghazal/sh1', text('پہلا مصرع'), { by: 'دیوان' }, { SourceName: 'گنجور' });
    assert.equal(JSON.parse(await readFile(join(dir, 'divan/p1/ghazal/sh1.json'), 'utf8')).SourceName, 'گنجور');
  } finally {
    await rm(dir, { recursive: true });
  }
});
