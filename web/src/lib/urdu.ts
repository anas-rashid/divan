// Urdu display helpers
const DIGITS = '۰۱۲۳۴۵۶۷۸۹';
export const ud = (n: number | string | null | undefined) =>
  n == null ? '' : String(n).replace(/[0-9]/g, (d) => DIGITS[+d]);

const ORDINALS = ['', 'پہلی', 'دوسری', 'تیسری', 'چوتھی', 'پانچویں', 'چھٹی', 'ساتویں', 'آٹھویں', 'نویں', 'دسویں',
  'گیارہویں', 'بارہویں', 'تیرہویں', 'چودہویں', 'پندرہویں'];
export const centuryOf = (yearAh: number | null) => (yearAh ? Math.floor(yearAh / 100) + 1 : null);
export const centuryName = (c: number | null) => (c ? `${ORDINALS[c] ?? ud(c)} صدی ہجری` : 'دیگر شعرا');

export const years = (birth: number | null, death: number | null) =>
  birth || death ? `${birth ? ud(birth) : '؟'} – ${death ? ud(death) : '؟'} ھ` : '';
