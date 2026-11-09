import { test, after } from 'node:test';
import assert from 'node:assert/strict';
import Fastify from 'fastify';
import { metreRoutes, zameen } from './metres.ts';
import { pool } from './db.ts';

after(() => pool.end());

test('metres: a metre\'s works, the same zameen (metre + rhyme), rhyme alone without a metre, a «بحر» tag corrects', async () => {
  const app = Fastify();
  metreRoutes(app);
  const run = Date.now(), metre = `آزمائشی بحر ${run}`, fixed = `درست بحر ${run}`, rhyme = `زمینہے${run}`;
  const ps = (await pool.query(`SELECT id, metre, rhyme, language FROM poems WHERE language = 'ur-PK' ORDER BY id LIMIT 3`)).rows;
  const set = (p: any, m: string | null, r: string | null) => pool.query('UPDATE poems SET metre = $2, rhyme = $3 WHERE id = $1', [p.id, m, r]);
  const tag = (await pool.query(`INSERT INTO tags (type, name) VALUES ('بحر', $1) RETURNING id`, [fixed])).rows[0];
  try {
    await set(ps[0], metre, rhyme); await set(ps[1], metre, rhyme); await set(ps[2], null, rhyme);
    const z = await zameen({ ...ps[0], metre, rhyme });
    assert.deepEqual([z.metre, z.source, z.rhymeOnly], [metre, 'auto', false]);
    // a Persian work's metre is Ganjoor's, unless the scanner suggested it
    assert.equal((await zameen({ ...ps[0], metre, rhyme, language: 'fa-IR' })).source, 'ganjoor');
    assert.equal((await zameen({ ...ps[0], metre, rhyme, language: 'fa-IR', metre_auto: true })).source, 'auto');
    assert.equal(z.works.length, 1, 'the other work in the metre and rhyme; not the one without a metre');
    const list = (await app.inject(`/api/metre?name=${encodeURIComponent(metre)}`)).json();
    assert.equal(list.total, 2);
    assert.ok((await app.inject('/api/metres')).json().some((m: any) => m.name === metre && m.n === 2));
    assert.equal((await app.inject(`/api/metre?name=${encodeURIComponent('نہیں')}`)).statusCode, 404);

    const z2 = await zameen({ ...ps[2], metre: null, rhyme });
    assert.deepEqual([z2.metre, z2.rhymeOnly, z2.works.length], [null, true, 2], 'no metre: the same rhyme letters alone');
    assert.equal((await zameen({ ...ps[2], metre: null, rhyme: 'ا' })).works.length, 0, 'too short to mean anything alone');

    // a moderator's published «بحر» tag on the whole work wins
    await pool.query(`INSERT INTO entity_tags (tag_id, entity, entity_id) VALUES ($1, 'work', $2)`, [tag.id, ps[0].id]);
    const z3 = await zameen({ ...ps[0], metre, rhyme });
    assert.deepEqual([z3.metre, z3.source, z3.works.length], [fixed, 'mod', 0]);
    assert.equal((await app.inject(`/api/metre?name=${encodeURIComponent(metre)}`)).json().total, 1);
    assert.equal((await app.inject(`/api/metre?name=${encodeURIComponent(fixed)}`)).json().total, 1);
  } finally {
    await pool.query('DELETE FROM tags WHERE id = $1', [tag.id]);
    for (const p of ps) await set(p, p.metre, p.rhyme);
  }
});
