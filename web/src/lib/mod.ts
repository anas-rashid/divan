// Urdu labels for moderation (api/src/moderation.ts)
export const STATUS: Record<string, string> = {
  draft: 'مسودہ', submitted: 'جائزے کا منتظر', approved: 'منظور شدہ، اشاعت کا منتظر', published: 'شائع شدہ', returned: 'واپس بھیجا گیا', rejected: 'مسترد',
};
export const EVENT: Record<string, string> = {
  created: 'مسودہ بنایا', saved: 'محفوظ کیا', submitted: 'جائزے کے لیے بھیجا', approved: 'منظور کیا', returned: 'واپس بھیجا',
  rejected: 'مسترد کیا', published: 'شائع کیا',
};
export const when = (d: string) => new Date(d).toISOString().slice(0, 16).replace('T', ' ');
