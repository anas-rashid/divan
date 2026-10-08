// The Urdu content editor (#28) on Tiptap (MIT core, ProseMirror). It edits Divan text as structure:
//   unit      a verse unit of misras: 1 misra = مصرع, 2 = شعر (couplet), 3 or more = بند (stanza), as in Divan text;
//             attribute label = مطلع | حسن مطلع | مقطع
//   para      a prose paragraph            heading   a chapter (level 2) or sub-heading (3, 4) title
//   dict      a word linked to the dictionary (mark, attribute lemma)
//   note      a footnote, variant a variant reading (inline atoms)
// Enter adds a misra to the unit (or moves into the empty next one); Enter on an empty misra starts a new unit
// (a blank line in Divan text).
// toDoc / fromEditor convert between Divan text and the editor, so the editor and the source view always agree.
import { Editor, Mark, Node, mergeAttributes } from '@tiptap/core';
import Document from '@tiptap/extension-document';
import Text from '@tiptap/extension-text';
import { Placeholder, UndoRedo } from '@tiptap/extensions';
import { parse, toText, type Doc, type Segment } from '../../../api/src/divantext.ts';

const Unit = Node.create({
  name: 'unit', group: 'block', content: 'misra+', defining: true,
  addAttributes: () => ({ label: { default: null, parseHTML: (e) => e.getAttribute('data-mark'), renderHTML: (a) => (a.label ? { 'data-mark': a.label } : {}) } }),
  parseHTML: () => [{ tag: 'div.unit' }],
  renderHTML: ({ HTMLAttributes }) => ['div', mergeAttributes(HTMLAttributes, { class: 'unit' }), 0],
  addKeyboardShortcuts() {
    return {
      // Enter on an empty misra: end this unit and start a new one (like a blank line in Divan text)
      Enter: ({ editor }) => {
        const { $from, empty } = editor.state.selection;
        if (!empty || $from.parent.type.name !== 'misra') return false;
        const unit = $from.node(-1), next = $from.index(-1) + 1;
        // at the end of a misra followed by an empty one (a new couplet's second misra): move into it
        if ($from.parentOffset === $from.parent.content.size && next < unit.childCount && !unit.child(next).content.size)
          return editor.commands.setTextSelection($from.after() + 1);
        if ($from.parent.content.size || unit.childCount < 2) return false;
        const misraPos = $from.before(), unitEnd = $from.after(-1);
        return editor.chain().command(({ tr }) => {
          tr.delete(misraPos, misraPos + $from.parent.nodeSize);
          return true;
        }).insertContentAt(unitEnd - $from.parent.nodeSize, { type: 'unit', content: [{ type: 'misra' }] }).focus(unitEnd - $from.parent.nodeSize + 2).run();
      },
    };
  },
});
const Misra = Node.create({
  name: 'misra', content: 'inline*',
  parseHTML: () => [{ tag: 'p.misra' }], renderHTML: () => ['p', { class: 'misra' }, 0],
});
const Para = Node.create({
  name: 'para', group: 'block', content: 'inline*',
  parseHTML: () => [{ tag: 'p.para' }], renderHTML: () => ['p', { class: 'para' }, 0],
});
const Heading = Node.create({
  name: 'heading', group: 'block', content: 'text*', defining: true,
  addAttributes: () => ({ level: { default: 2 } }),
  parseHTML: () => [2, 3, 4].map((l) => ({ tag: `h${l + 1}`, attrs: { level: l } })),
  renderHTML: ({ node }) => [`h${node.attrs.level + 1}`, { class: node.attrs.level > 2 ? 'mod-h sub' : 'mod-h' }, 0],
});
const Dict = Mark.create({
  name: 'dict', inclusive: false,
  addAttributes: () => ({ lemma: { default: null } }),
  parseHTML: () => [{ tag: 'span.dv-dict' }],
  renderHTML: ({ mark }) => ['span', { class: 'dv-dict', title: `لغت: ${mark.attrs.lemma}` }, 0],
});
const Note = Node.create({
  name: 'note', group: 'inline', inline: true, atom: true,
  addAttributes: () => ({ text: { default: '' } }),
  parseHTML: () => [{ tag: 'sup.note' }],
  renderHTML: ({ node }) => ['sup', { class: 'note', title: node.attrs.text, 'data-note': node.attrs.text }, '*'],
});
const Variant = Node.create({
  name: 'variant', group: 'inline', inline: true, atom: true,
  addAttributes: () => ({ shown: { default: '' }, others: { default: [] }, source: { default: null } }),
  parseHTML: () => [{ tag: 'span.variant' }],
  renderHTML: ({ node }) => ['span', { class: 'variant', title: `دوسرے نسخے: ${node.attrs.others.join('، ')}${node.attrs.source ? ` (${node.attrs.source})` : ''}` }, node.attrs.shown],
});

// ---- Divan text <-> editor content ----

const inlineContent = (segs: Segment[]) => segs.flatMap((s): any[] =>
  'note' in s ? [{ type: 'note', attrs: { text: s.note } }]
  : 'variant' in s ? [{ type: 'variant', attrs: { shown: s.variant.shown, others: s.variant.others, source: s.variant.source ?? null } }]
  : !s.text ? [] : 'lemma' in s ? [{ type: 'text', text: s.text, marks: [{ type: 'dict', attrs: { lemma: s.lemma } }] }]
  : [{ type: 'text', text: s.text }]);

export function toEditor(doc: Doc) {
  const content = doc.blocks.map((b): any =>
    b.type === 'heading' ? { type: 'heading', attrs: { level: b.level }, content: b.text ? [{ type: 'text', text: b.text }] : [] }
    : b.type === 'para' ? { type: 'para', content: inlineContent(b.line.segments) }
    : { type: 'unit', attrs: { label: b.label ?? null }, content: b.lines.map((l) => ({ type: 'misra', content: inlineContent(l.segments) })) });
  return { type: 'doc', content: content.length ? content : [{ type: 'unit', content: [{ type: 'misra' }, { type: 'misra' }] }] };
}

const segments = (node: any): Segment[] => (node.content ?? []).map((n: any): Segment =>
  n.type === 'note' ? { note: n.attrs.text }
  : n.type === 'variant' ? { variant: { shown: n.attrs.shown, others: n.attrs.others, ...(n.attrs.source && { source: n.attrs.source }) } }
  : n.marks?.find((m: any) => m.type === 'dict') ? { text: n.text, lemma: n.marks.find((m: any) => m.type === 'dict').attrs.lemma }
  : { text: n.text });
const lineOf = (node: any) => ({ text: '', words: [], notes: [], variants: [], segments: segments(node) });

// editor content (+ the work's data points) -> Divan text; empty misras and paragraphs are dropped
export function fromEditor(json: any, meta: Record<string, string>) {
  const blocks: Doc['blocks'] = [];
  for (const n of json.content ?? []) {
    const hasText = (x: any) => (x.content ?? []).some((c: any) => c.type !== 'text' || c.text.trim());
    if (n.type === 'heading') { const t = (n.content ?? []).map((c: any) => c.text).join('').trim(); if (t) blocks.push({ type: 'heading', text: t, level: n.attrs.level }); }
    else if (n.type === 'para') { if (hasText(n)) blocks.push({ type: 'para', line: lineOf(n) }); }
    else {
      const lines = (n.content ?? []).filter(hasText).map(lineOf);
      if (lines.length) blocks.push({ type: lines.length === 2 ? 'couplet' : lines.length === 1 ? 'line' : 'stanza', lines, ...(n.attrs?.label && { label: n.attrs.label }) });
    }
  }
  return toText({ meta, blocks });
}

export function createEditor(element: HTMLElement, text: string) {
  const doc = parse(text);
  const editor = new Editor({
    element,
    extensions: [Document, Text, Unit, Misra, Para, Heading, Dict, Note, Variant, UndoRedo,
      Placeholder.configure({ placeholder: ({ node }) => (node.type.name === 'misra' ? 'مصرع' : node.type.name === 'para' ? 'پیراگراف' : 'عنوان') })],
    content: toEditor(doc),
    editorProps: { attributes: { class: 'dv-editor poem', dir: 'rtl', 'data-urdu': '', spellcheck: 'false' } },
  });
  return { editor, meta: doc.meta };
}

// toolbar actions on the block at the cursor
export const actions = {
  couplet: (e: Editor) => insertAfter(e, { type: 'unit', content: [{ type: 'misra' }, { type: 'misra' }] }),
  para: (e: Editor) => insertAfter(e, { type: 'para' }),
  heading: (e: Editor, level: number) => insertAfter(e, { type: 'heading', attrs: { level } }),
  label: (e: Editor, label: string | null) => {
    const { $from } = e.state.selection;
    for (let d = $from.depth; d > 0; d--) if ($from.node(d).type.name === 'unit') {
      const pos = $from.before(d), cur = $from.node(d).attrs.label;
      return e.chain().command(({ tr }) => { tr.setNodeMarkup(pos, undefined, { label: cur === label ? null : label }); return true; }).focus().run();
    }
    return false;
  },
  dict: (e: Editor) => {
    if (e.isActive('dict')) return e.chain().focus().unsetMark('dict').run();
    const { from, to } = e.state.selection;
    const word = e.state.doc.textBetween(from, to).trim();
    if (!word) return alert('پہلے لفظ منتخب کریں');
    const lemma = prompt('لغت میں لفظ کی بنیادی شکل', word);
    if (lemma) e.chain().focus().setMark('dict', { lemma: lemma.trim() }).run();
  },
  note: (e: Editor) => {
    const text = prompt('حاشیہ');
    if (text?.trim()) e.chain().focus().insertContent({ type: 'note', attrs: { text: text.trim() } }).run();
  },
  variant: (e: Editor) => {
    const { from, to } = e.state.selection;
    const shown = (e.state.doc.textBetween(from, to).trim() || prompt('متن میں دکھایا جانے والا لفظ')) ?? '';
    const others = prompt('دوسرے نسخے کا متن (کئی ہوں تو ، سے الگ کریں)');
    if (!shown || !others) return;
    const source = prompt('دوسرے نسخے کا نام') || null;
    e.chain().focus().insertContentAt({ from, to }, { type: 'variant', attrs: { shown, others: others.split('،').map((s) => s.trim()).filter(Boolean), source } }).run();
  },
};

function insertAfter(e: Editor, node: any) {
  const { $from } = e.state.selection;
  const pos = $from.depth > 0 ? $from.after(1) : e.state.doc.content.size;
  return e.chain().insertContentAt(pos, node).focus(pos + (node.type === 'unit' ? 2 : 1)).run();
}
