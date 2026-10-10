import { test } from 'node:test';
import assert from 'node:assert/strict';
import { disk } from './health.ts';

test('disk: percent used of the data folder, full at the threshold', async () => {
  const d = await disk('.', 101);
  assert.ok(d.used > 0 && d.used <= 100, `used ${d.used}`);
  assert.equal(d.full, false, 'never full below 101%');
  assert.equal((await disk('.', 0)).full, true, 'always full at 0%');
});
