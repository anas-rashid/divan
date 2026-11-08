// Readers' rich text (suggestions.ts): the editor's JSON (Tiptap, web/src/lib/richtext.ts), kept only as far as it
// is one of these, and shown as HTML built here with everything escaped, so a post can never carry markup of its own:
//   paragraph, bulletList / orderedList > listItem, blockquote, hardBreak, text with bold, italic and link marks
//   (links: https/http, or a path on this site)
export type RichDoc = { type: 'doc'; content: any[] };

const BLOCKS: Record<string, string> = { paragraph: 'p', bulletList: 'ul', orderedList: 'ol', listItem: 'li', blockquote: 'blockquote' };
const MARKS = ['bold', 'italic', 'link'];
const esc = (s: string) => s.replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c]!);
export const safeHref = (h: unknown) => {
  const s = String(h ?? '').trim();
  return /^https?:\/\/[^\s<>"]+$/i.test(s) || /^\/(?!\/)[^\s<>"]*$/.test(s) ? s : null;
};

// the document with anything not allowed dropped (a link without a safe address keeps its text), or null when empty
export function cleanRich(doc: unknown, maxChars = 10_000): RichDoc | null {
  let chars = 0;
  const node = (n: any, depth: number): any => {
    if (!n || typeof n !== 'object' || depth > 8) return null;
    if (n.type === 'text') {
      const text = String(n.text ?? '').normalize('NFC').slice(0, maxChars - chars);
      if (!text) return null;
      chars += text.length;
      const marks = (Array.isArray(n.marks) ? n.marks : []).flatMap((m: any) => !MARKS.includes(m?.type) ? []
        : m.type !== 'link' ? [{ type: m.type }] : safeHref(m.attrs?.href) ? [{ type: 'link', attrs: { href: safeHref(m.attrs.href) } }] : []);
      return { type: 'text', text, ...(marks.length && { marks }) };
    }
    if (n.type === 'hardBreak') return { type: 'hardBreak' };
    if (!BLOCKS[n.type]) return null;
    const content = (Array.isArray(n.content) ? n.content : []).map((c: any) => node(c, depth + 1)).filter(Boolean);
    return n.type === 'paragraph' || content.length ? { type: n.type, ...(content.length && { content }) } : null;
  };
  const content = ((doc as any)?.type === 'doc' && Array.isArray((doc as any).content) ? (doc as any).content : [])
    .map((c: any) => node(c, 0)).filter(Boolean);
  return plainText({ type: 'doc', content }).trim() ? { type: 'doc', content } : null;
}

export function richHtml(doc: RichDoc): string {
  const node = (n: any): string => {
    if (n.type === 'hardBreak') return '<br>';
    if (n.type === 'text') {
      let h = esc(n.text);
      for (const m of n.marks ?? []) h = m.type === 'bold' ? `<strong>${h}</strong>` : m.type === 'italic' ? `<em>${h}</em>`
        : `<a href="${esc(m.attrs.href)}" rel="nofollow ugc noopener">${h}</a>`;
      return h;
    }
    const tag = BLOCKS[n.type];
    return tag ? `<${tag}>${(n.content ?? []).map(node).join('')}</${tag}>` : '';
  };
  return doc.content.map(node).join('');
}

export function plainText(doc: RichDoc): string {
  const node = (n: any): string => n.type === 'text' ? n.text : n.type === 'hardBreak' ? '\n'
    : (n.content ?? []).map(node).join('') + (BLOCKS[n.type] ? '\n' : '');
  return doc.content.map(node).join('').replace(/\n{3,}/g, '\n\n').trim();
}
