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
export const years = (p: { birth_year_ah: number | null; death_year_ah: number | null; birth_year_ce?: number | null; death_year_ce?: number | null }) =>
  [span(p.birth_year_ah, p.death_year_ah, 'ھ'), span(p.birth_year_ce ?? null, p.death_year_ce ?? null, 'ء')].filter(Boolean).join(' · ');
