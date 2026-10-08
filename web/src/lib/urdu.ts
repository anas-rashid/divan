// Urdu display helpers
const DIGITS = '۰۱۲۳۴۵۶۷۸۹';
export const ud = (n: number | string | null | undefined) =>
  n == null ? '' : String(n).replace(/[0-9]/g, (d) => DIGITS[+d]);

const ORDINALS = ['', 'پہلی', 'دوسری', 'تیسری', 'چوتھی', 'پانچویں', 'چھٹی', 'ساتویں', 'آٹھویں', 'نویں', 'دسویں',
  'گیارہویں', 'بارہویں', 'تیرہویں', 'چودہویں', 'پندرہویں'];
export const centuryOf = (yearAh: number | null) => (yearAh ? Math.floor(yearAh / 100) + 1 : null);
export const centuryName = (c: number | null) => (c ? `${ORDINALS[c] ?? ud(c)} صدی ہجری` : 'دیگر شعرا');

const span = (a: number | null, b: number | null, mark: string) =>
  a || b ? `${a ? ud(a) : '؟'} – ${b ? ud(b) : '؟'} ${mark}` : '';
// Hijri and Gregorian (عیسوی): "۱۲۹۴ – ۱۳۵۷ ھ · ۱۸۷۷ – ۱۹۳۸ ء"
type Years = { birth_year_ah: number | null; death_year_ah: number | null; birth_year_ce?: number | null; death_year_ce?: number | null };
// Hijri and Gregorian spans, each on its own (poet cards show them as two lines)
export const yearLines = (p: Years) =>
  [span(p.birth_year_ah, p.death_year_ah, 'ھ'), span(p.birth_year_ce ?? null, p.death_year_ce ?? null, 'ء')].filter(Boolean) as string[];
export const years = (p: Years) => yearLines(p).join(' · ');

// search highlighting (same matching rules as the API's search)
export { highlighter } from '../../../api/src/urdu.ts';
// text split so odd indexes are the matches
export const parts = (text: string, re: RegExp | null) => (re ? text.split(re) : [text]);
// prose: a stretch of text around the first match
export function excerpt(text: string, re: RegExp | null, room = 90) {
  if (text.length <= room * 2) return text;
  const i = Math.max(0, re ? text.search(re) : 0);
  const a = Math.max(0, text.lastIndexOf(' ', Math.max(0, i - room))), b = text.indexOf(' ', i + room);
  return (a > 0 ? '… ' : '') + text.slice(a, b < 0 ? undefined : b).trim() + (b < 0 ? '' : ' …');
}
