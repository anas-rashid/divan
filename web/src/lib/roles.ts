// Urdu labels for roles and permissions (api/src/admin.ts, api/src/permissions.ts)
export const ROLE: Record<string, string> = { reader: 'قاری', 'mod-l2': 'موڈریٹر (L2)', 'mod-l1': 'سینئر موڈریٹر (L1)', admin: 'ایڈمن' };
export const SCOPE: Record<string, string> = { all: 'تمام شعرا', poet: 'شاعر', category: 'کتاب / حصہ', poem: 'ایک کلام' };
export const CONTENT: Record<string, string> = { poets: 'شعرا', books: 'کتابیں', works: 'کلام', dictionary: 'لغت', site: 'سائٹ (سرورق کے حصے)', tags: 'ٹیگ', ebooks: 'ای بکس' };
export const ACTION: Record<string, string> = { create: 'نیا', edit: 'ترمیم', delete: 'حذف', arrange: 'ترتیب' };
