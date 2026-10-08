# Content model and moderation: research and recommendation (#38)

October 2026. Question from the owner: content moderation should work like a wiki, with tags and data points
that say what a couplet, a stanza, a chapter and a word are, and we should see whether an open-source wiki
content-management module can be integrated.

## What Divan needs

- **Structure**: poet › book › chapter/section › work; verse as couplets (شعر, two misras), single lines and
  stanzas (بند); prose as paragraphs under headings; words (for the dictionary, search and the word book).
- **Data points**: genre (غزل, نظم, رباعی, …), radif, qafiya, matla/maqta, pen name, footnotes, variant
  readings between editions, links from words to dictionary entries, sources.
- **Moderation**: L2 → L1 → admin approval (#31), scoped per poet/book/work and per action, including arranging
  (#29, #41); Divan's copy takes precedence over Wikisource (#30); upstream changes reviewed (#36).
- **Publishing**: the published version is committed to the open divan-data repo, so its git history is the
  public version history and every change is a readable diff (#34).
- **Clients**: the site and mobile apps read the same API (#44).

## Options considered (checked October 2026)

| Option | Status | Licence | Fit | Why |
|---|---|---|---|---|
| MediaWiki 1.47 LTS (due Nov 2026) as a module | Stable; runs Urdu Wikisource | GPL-2.0+ | Poor | A second stack (PHP) and a second user/permission system. FlaggedRevs is "not recommended for production use" (its own documentation). Approved Revs has one approval level only. Content would be unstructured wikitext, not couplets. |
| + ProofreadPage, Semantic MediaWiki, Wikibase | Maintained | GPL-2.0+ | Poor/partial | ProofreadPage is for page scans, which we do not have. SMW and Wikibase keep data in the wiki, not in our schema. Wikibase is worth revisiting later for linked-data records of poets and works. |
| Wiki.js | v3 still in beta ("not for production") | AGPL-3.0 | Poor | Syncs to git, but stores Markdown pages, with no review workflow and no verse structure. |
| BookStack, Outline, Docmost, DokuWiki | Maintained | MIT / BSL / AGPL / GPL | Poor | No multi-level approval, no git publishing, no verse model. Outline is not open source (BSL). |
| **wikiparser-node** (library) | v1.48, very active | **GPL-3.0** | **Good** | A full wikitext parser for Node: `<poem>`, `<ref>`, templates; tested against MediaWiki's parser tests. For importing and re-syncing from Wikisource and Wiktionary. |
| **Tiptap 3 core / ProseMirror** (editor) | Active | **MIT** | **Good** | A schema-first editor: our own nodes (ghazal › sher › misra, paragraph, heading), RTL. Tiptap's paid "Pro" add-ons (comments, history, collaboration) are not needed: review and history live in our database and in git. |
| CodeMirror 6 (editor) | Active | MIT | Good | A plain-text "source" view with bidi support; wikiparser-node has a CodeMirror integration. |
| **TEI P5 4.12** | The standard for literary texts | Open | Export | A couplet is `<lg>` with two `<l>`. No established Urdu or Persian TEI corpus was found. Good as an export for scholars, not as storage. |
| **Ganjoor's correction system** (GanjoorService) | Active | GPL-3.0 | Reference | The closest working example: per-poem and per-section correction records that moderators accept or reject field by field, kept as history. We follow its data design, not its code. |

**Conclusion: no wiki engine is embedded.** Divan already has what a wiki engine would duplicate: accounts,
scoped permissions and the site. None of the engines offers the three things Divan needs together: verse
structure, L2 → L1 → admin review scoped per poet and book, and git publishing. The "wiki" qualities (open
history, diffs, notes per page, typed data points) are built into Divan's own moderation, using the libraries
above.

## Recommendation

1. **Divan text as the source format** (prototype: `api/src/divantext.ts`). It is a small subset of Wikisource's
   own markup, so Wikisource pages read as-is and moderators who know Wikisource feel at home:

   ```
   {{دیوان
   | عنوان = یہ نہ تھی ہماری قسمت کہ وصال یار ہوتا
   | شاعر = مرزا غالب
   | صنف = غزل
   | ردیف = ہوتا
   | ماخذ = ویکی ماخذ
   }}
   <poem>
   {{مطلع}} یہ نہ تھی ہماری قسمت کہ [[وصال]] یار ہوتا
   اگر اور جیتے رہتے یہی انتظار ہوتا

   کوئی میرے دل سے پوچھے ترے تیر [[نیم کش]] کو<ref>نیم کش: آدھا کھنچا ہوا تیر</ref>
   یہ خلش کہاں سے ہوتی جو جگر کے پار ہوتا
   </poem>
   ```

   | Mark-up | Meaning |
   |---|---|
   | `{{دیوان \| …}}` | The work's data points: title, poet/author, genre, radif, qafiya, metre, sources. |
   | `<poem>` | Verse. Blank lines separate units: **2 lines = شعر** (couplet: misra 1 and 2), **1 line = مصرع**, **3 or more = بند** (stanza). |
   | Text outside `<poem>` | Prose. Blank lines separate **paragraphs**; `== … ==` is a **chapter or section heading**. |
   | `{{مطلع}}` `{{حسن مطلع}}` `{{مقطع}}` | At the start of a couplet: overrides the computed labels. |
   | `[[لفظ]]` `[[لغت:دیوانہ\|دیوانے]]` | A **word** linked to its dictionary entry (lemma, then shown form). |
   | `<ref>…</ref>` | Footnote. |
   | `{{نسخہ\|متن\|دوسرا متن\|ماخذ=…}}` | Variant reading between editions; the first is shown. |

   **What a word is**: text split on spaces. A zero-width non-joiner keeps a compound as one word (بے‌ثبوت).
   Punctuation (، ۔ ؟ !) and the pen-name sign ؔ are not part of a word, and the izafat kasra stays on its word.
   The same rule is used by search, the word sidebar and the word book.

2. **Git diffs stay readable.** The same one-word correction in one misra, in each format:
   - **Divan text**: a one-line diff showing that misra (`-کبھی تو نہ توڑ سکتا اگر استوار ہوتا` /
     `+کبھی تو نہ توڑ سکتا جو استوار ہوتا`).
   - **The current JSON export**: the change also rewrites a field holding the whole poem on one line, which is
     unreadable in a diff.

   divan-data therefore keeps **one `.dtx` file per work** as the published source. The JSON layout and TEI are
   generated from it.

3. **Arranging is a list in a file.** Each book or chapter keeps its contents as an ordered list of works. A
   moderator who reorders Ghalib's ghazals (#41) changes the order of lines in that list, which is again a
   readable diff.

4. **The moderation workflow lives in PostgreSQL** (#31), modelled on Ganjoor's correction records:
   - a `drafts` table holds the proposed Divan text with its base version, author, scope, status (draft,
     submitted, L1-approved, published, returned, rejected) and comments;
   - reviewers see a side-by-side diff;
   - on publish, the `.dtx` file is written to divan-data and committed with the moderator as author, and the
     reviewer and admin in `Reviewed-by:` / `Approved-by:` trailers (#34);
   - the importer reads the published files, so the site and apps show the new text.

5. **Editors** (#28, #32):
   - the main editor is a structured Urdu editor on **Tiptap 3's MIT core**: couplet, misra, stanza, paragraph
     and heading blocks; the Urdu keyboard and Roman typing; a diacritics palette; and buttons for the data
     points above;
   - a **source view** (CodeMirror 6) shows the Divan text itself for experienced moderators;
   - both read and write the same Divan text.

6. **Wikisource sync** (#30, #36) uses **wikiparser-node** to read Wikisource pages into Divan text. Works that
   Divan has published are never overwritten; a changed upstream page becomes a draft in the review queue.

7. **TEI P5 export** for scholars:
   - a couplet is `<lg type="sher">` with two `<l>`;
   - matla/maqta are `@ana`;
   - dictionary-linked words are `<w lemma>`;
   - variants are `<app>`;
   - footnotes are `<note>`.

   Rendering with CETEIcean or publishing through TEI Publisher can be added if scholars ask for them.

8. **Later**: Wikibase as linked-data records for poets and works, if that becomes useful; OpenITI-style
   stable identifiers for works and versions.

## Prototype results

`api/src/divantext.ts` parses Divan text into blocks, converts them to the site's verse JSON, and exports TEI.
The tests (`api/src/divantext.test.ts`, 7 passing) run on the real Ghalib ghazal and Sir Syed's آزادی رائے
(`docs/content-model/*.dtx`):

- **Data points**: genre, radif, the مطلع override, a dictionary link, a footnote and a variant reading are read
  from the annotated ghazal.
- **Same text on the site**: the annotated ghazal produces **exactly the verses of today's export** (position,
  text and couplet number of all 22 lines), so adopting the format changes nothing for readers.
- **Wikisource compatibility**: raw Wikisource markup (`{{header}}`, `<poem align>`) reads unchanged.
- **Prose and stanzas**: headings and paragraphs (with a footnote) from the prose chapter; stanzas and single
  lines.
- **Words**: the rule above, including compounds and the pen-name sign.
- **TEI**: the export validates as well-formed XML (`xmllint`).

## Effect on the plan

- **#30** (Divan-owned content): `.dtx` files in divan-data, marked as Divan's once published; the export and
  sync respect them.
- **#31** (pipeline): drafts of Divan text, with diffs and the L2 → L1 → admin states.
- **#32, #41** (editors, arranging): Tiptap editor and source view; contents lists as ordered files.
- **#28, #33** (Urdu editor, document import): document import converts to Divan text.
- **#34** (git publishing): commits the `.dtx` files and the contents lists.
- **#36** (upstream changes): Wikisource re-reads through wikiparser-node become drafts.
- **New**: the TEI export, and a word index built on the word rule above.
