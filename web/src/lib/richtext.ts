// A small rich-text editor for readers' suggestions (api/src/suggestions.ts): paragraphs, bold, italic, links, lists
// and quotes, from Tiptap's core like the Urdu editor (editor.ts). Its JSON is what the API keeps (api/src/richtext.ts
// checks it and builds the HTML), so the node and mark names here are the API's.
import { Editor, Mark, Node } from '@tiptap/core';
import Document from '@tiptap/extension-document';
import Text from '@tiptap/extension-text';
import { Placeholder, UndoRedo } from '@tiptap/extensions';

const block = (name: string, tag: string, content: string, group = 'block') =>
  Node.create({ name, group, content, parseHTML: () => [{ tag }], renderHTML: () => [tag, 0] });
const Paragraph = block('paragraph', 'p', 'inline*');
const Blockquote = block('blockquote', 'blockquote', 'paragraph+');
const BulletList = block('bulletList', 'ul', 'listItem+');
const OrderedList = block('orderedList', 'ol', 'listItem+');
const ListItem = Node.create({
  name: 'listItem', content: 'paragraph+', defining: true,
  parseHTML: () => [{ tag: 'li' }], renderHTML: () => ['li', 0],
  addKeyboardShortcuts() {
    return { Enter: () => this.editor.commands.splitListItem('listItem'), Tab: () => this.editor.commands.sinkListItem('listItem'),
      'Shift-Tab': () => this.editor.commands.liftListItem('listItem') };
  },
});
const HardBreak = Node.create({
  name: 'hardBreak', group: 'inline', inline: true, selectable: false,
  parseHTML: () => [{ tag: 'br' }], renderHTML: () => ['br'],
  addKeyboardShortcuts() { return { 'Shift-Enter': () => this.editor.commands.insertContent({ type: 'hardBreak' }) }; },
});
const Bold = Mark.create({
  name: 'bold', parseHTML: () => [{ tag: 'strong' }, { tag: 'b' }], renderHTML: () => ['strong', 0],
  addKeyboardShortcuts() { return { 'Mod-b': () => this.editor.commands.toggleMark('bold') }; },
});
const Italic = Mark.create({
  name: 'italic', parseHTML: () => [{ tag: 'em' }, { tag: 'i' }], renderHTML: () => ['em', 0],
  addKeyboardShortcuts() { return { 'Mod-i': () => this.editor.commands.toggleMark('italic') }; },
});
const Link = Mark.create({
  name: 'link', inclusive: false,
  addAttributes: () => ({ href: { default: null } }),
  parseHTML: () => [{ tag: 'a[href]', getAttrs: (e) => ({ href: (e as HTMLElement).getAttribute('href') }) }],
  renderHTML: ({ mark }) => ['a', { href: mark.attrs.href, rel: 'nofollow noopener' }, 0],
});

export function createRichEditor(element: HTMLElement, placeholder: string) {
  return new Editor({
    element,
    extensions: [Document, Text, Paragraph, Blockquote, BulletList, OrderedList, ListItem, HardBreak, Bold, Italic, Link, UndoRedo,
      Placeholder.configure({ placeholder })],
    editorProps: { attributes: { class: 'rich-editor', dir: 'auto', 'data-urdu': '' } },
  });
}

// toolbar actions; a link asks for its address (a page of this site or https://…)
export function richCommand(editor: Editor, act: string) {
  const c = editor.chain().focus();
  if (act === 'bold' || act === 'italic') return c.toggleMark(act).run();
  if (act === 'bulletList' || act === 'orderedList') return c.toggleList(act, 'listItem').run();
  if (act === 'blockquote') return c.toggleWrap('blockquote').run();
  if (act === 'undo') return c.undo().run();
  if (act === 'redo') return c.redo().run();
  if (act === 'link') {
    const href = prompt('لنک (دیوان کے کسی صفحے کا یا https://…)', editor.getAttributes('link').href ?? '')?.trim();
    if (href === undefined) return false;
    if (!href) return c.extendMarkRange('link').unsetMark('link').run();
    if (!/^(https?:\/\/|\/)/i.test(href)) return alert('لنک https:// یا / سے شروع ہو'), false;
    return editor.state.selection.empty
      ? c.insertContent({ type: 'text', text: href, marks: [{ type: 'link', attrs: { href } }] }).run()
      : c.extendMarkRange('link').setMark('link', { href }).run();
  }
  return false;
}
