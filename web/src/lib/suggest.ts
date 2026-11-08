// Labels for readers' suggestions (api/src/suggestions.ts CATEGORIES, STATUSES)
export const CATEGORY: Record<string, string> = {
  name: 'نام یا تخلص', portrait: 'تصویر', intro: 'تعارف', text: 'متن (کلام، غزل)', title: 'عنوان', metre: 'بحر',
  order: 'ترتیب', tags: 'ٹیگ', other: 'دیگر',
};
export const STATUS: Record<string, string> = { open: 'زیرِ غور', planned: 'منظور شدہ: کام جاری', done: 'مکمل شدہ', declined: 'نامنظور شدہ', hidden: 'پوشیدہ' };
