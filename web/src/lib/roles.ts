import { ud } from './urdu';
// Urdu labels for roles and permissions (api/src/admin.ts, api/src/permissions.ts)
export const ROLE: Record<string, string> = { reader: 'قاری', 'mod-l2': 'موڈریٹر (L2)', 'mod-l1': 'سینئر موڈریٹر (L1)', admin: 'ایڈمن' };
export const SCOPE: Record<string, string> = { all: 'تمام شعرا', poet: 'شاعر', category: 'کتاب / حصہ', poem: 'ایک کلام' };
export const CONTENT: Record<string, string> = { poets: 'شعرا', books: 'کتابیں', works: 'کلام', dictionary: 'لغت', site: 'سائٹ (سرورق کے حصے)', tags: 'ٹیگ', ebooks: 'ای بکس' };
export const ACTION: Record<string, string> = { create: 'نیا', edit: 'ترمیم', delete: 'حذف', arrange: 'ترتیب' };
// what each role may do, one line each (the user page's role picker)
export const ROLE_HINT: Record<string, string> = {
  reader: 'پڑھ سکتے ہیں، محفوظ کر سکتے ہیں',
  'mod-l2': 'دی گئی اجازتوں میں ترمیم بھیج سکتے ہیں',
  'mod-l1': 'اپنے دائرے میں ترامیم منظور یا واپس کر سکتے ہیں',
  admin: 'سب کچھ: صارفین، اجازتیں، اشاعت',
};
// a date as "today / yesterday / n days ago" for the last few weeks, the date after that
export const ago = (d: string | null) => {
  if (!d) return 'کبھی نہیں';
  const days = Math.floor((Date.now() - new Date(d).getTime()) / 864e5);
  return days < 1 ? 'آج' : days < 2 ? 'کل' : days < 30 ? `${ud(days)} دن پہلے` : ud(new Date(d).toISOString().slice(0, 10));
};
