// node web/src/lib/fonts.test.mjs: the three kinds of font (global.css, "Fonts, three kinds") stay apart
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
const css = readFileSync(new URL('../styles/global.css', import.meta.url), 'utf8').replace(/\/\*[\s\S]*?\*\//g, '');
// literature: only .lit takes the reader's font (a selector list broken by an edit once turned Nastaliq off)
const reading = [...css.matchAll(/([^{}]+)\{[^}]*\bfont(?:-family)?:[^;}]*var\(--font\)/g)].map((m) => m[1].trim());
assert.deepEqual(reading, ['.lit'], `only .lit may use var(--font); found: ${reading.join(' | ')}`);
// calligraphy: Nastaliq is named only where the font variables are defined
const named = css.split('\n').filter((l) => l.includes('Nastaliq Urdu') && !/--font(-nastaliq)?:/.test(l));
assert.deepEqual(named, [], 'use var(--font-nastaliq) instead of naming the font');
console.log('ok');
