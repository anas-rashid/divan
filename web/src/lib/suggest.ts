// Labels for readers' suggestions (api/src/suggestions.ts CATEGORIES, STATUSES)
export const CATEGORY: Record<string, string> = {
  name: 'نام یا تخلص', portrait: 'تصویر', intro: 'تعارف', text: 'متن (کلام، غزل)', title: 'عنوان', metre: 'بحر',
  order: 'ترتیب', tags: 'ٹیگ', other: 'دیگر',
};
export const STATUS: Record<string, string> = { open: 'زیرِ غور', planned: 'منظور: کام جاری', done: 'مکمل', declined: 'نامنظور', hidden: 'پوشیدہ' };
