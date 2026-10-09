<p align="center"><img src="web/public/logo/turanj.svg" width="140" alt="دیوان"></p>

# دیوان · Divan

An open-source site for reading and searching **classical Urdu poetry and prose**, in Urdu script (`ur-PK`). Content comes from [divan-data](https://github.com/anas-rashid/divan-data): public-domain texts from Urdu Wikisource, with short poet introductions from Urdu Wikipedia.

Divan follows the features of [GanjoorService](https://github.com/ganjoor/GanjoorService) (the software behind ganjoor.net), rebuilt in Node.js, TypeScript and PostgreSQL with its own UI. The first version was a fork of GanjoorService (.NET + SQL Server); that code is kept at the tag [`dotnet-final`](https://github.com/anas-rashid/divan/tree/dotnet-final).

## Layout

| Path | What |
|---|---|
| `db/schema.sql` | PostgreSQL schema: poets, categories, poems, verses; trigram index for Urdu substring search |
| `api/` | Node.js API (Fastify + pg). TypeScript runs natively on Node 24+, no build step |
| `web/` | The site (Astro, server-rendered). Naskh by default with a Nastaliq option, light/dark, RTL, mobile-first |
| `reader/index.html` | A single-file static reader over the divan-data CDN (no server) |

## Run locally

```sh
# PostgreSQL 17 (any Postgres 14+ works; port 5433 avoids clashing with a local one)
docker run -d --name divan-pg -e POSTGRES_USER=divan -e POSTGRES_PASSWORD=divan_local -e POSTGRES_DB=divan \
  -p 5433:5432 -v divan-pg:/var/lib/postgresql/data postgres:17-alpine

cd api && npm install
npm run import -- ../../divan-data      # a local divan-data checkout, or the CDN:
# npm run import -- https://cdn.jsdelivr.net/gh/anas-rashid/divan-data@main/
npm start                               # API on http://127.0.0.1:4100
npm test                                # Urdu normaliser tests

cd ../web && npm install && npm run build && npm start   # site on http://127.0.0.1:4200
```

Settings: `DATABASE_URL` (API, default `postgres://divan:divan_local@localhost:5433/divan`), `PORT`/`HOST`; `API_URL` (web, default `http://127.0.0.1:4100`); `SITE_HOSTS` (web, at build time: the site's hostnames, comma-separated, default `127.0.0.1,localhost`; form posts from other origins are refused).

The import upserts, so re-running it after a divan-data sync applies the changes.

**Accounts and admin.** Readers sign up with an email address and password (no email is sent). The first admin is made on the server: sign up on the site, then `npm run make-admin -- you@example.com` in `api/`. Admins manage users at `/admin` (search, password reset on a reader's request, disable, roles, delete) and see every admin action at `/admin/audit`. Moderators (L2 junior, L1 senior) get scoped permissions from admins: a scope (all poets, a poet, a book with everything in it, or one work), content types (poets, books, works, dictionary) and actions (create, edit, delete, arrange); `can()` in `api/src/permissions.ts` is the one check for moderation.

**Moderation.** Moderators open **ترمیم کریں** on a work they may edit, change its Divan text (see `docs/content-model.md`) with an edit summary and submit it. An L1 moderator covering that work approves, returns (with a reason) or rejects it; an admin publishes. L1 drafts go straight to the admin and an admin's own edits publish directly. Every step is recorded (`/mod`, the activity log, each work's history at `/mod/work/<id>`). Admins are super moderators: they edit, review and publish any work without grants. Publishing numbers the version, writes it to divan-data's `divan/` folder (`DIVAN_DATA_DIR`, default `../divan-data`), **commits it there** (the moderator as author by public name, with `Reviewed-by:` and `Approved-by:` trailers; `DIVAN_GIT_PUSH=1` also pushes), and shows it on the site at once with a link to the commit (`DIVAN_DATA_COMMIT_URL`, `{sha}` replaced). Public content never carries email addresses. A draft started before a newer version was published cannot be published. The daily sync rebases on pull so these commits are kept.

**E-books.** L2/L1 moderators with the e-books permission for a poet (admins always) see **ای بک شامل کریں** in the poet's **ای بکس** section: a PDF, EPUB, TXT or DOCX file (DOCX becomes text; up to 100 MB) or an archive.org link, with its title, source and copyright. Anyone with the permission may upload any book; it is listed only after an L1 moderator and an admin confirm it (the same pipeline), and until then only moderators can open it. Readers see PDFs in the browser's viewer, EPUBs in epub.js, text as text and archive.org items in archive.org's embedded reader (`/ebook/<id>`). Approved details (not the file) are committed to divan-data (`divan/<poet>/ebooks/<id>.txt`).

**File store.** Uploaded files are kept in `DIVAN_FILES_DIR` (default `../divan-files` beside the repo) under their SHA-256, `ab/cd/<sha256>.<ext>`, so a file uploaded twice is stored once and the folder spreads evenly. On the server, point it at storage that can grow: an attached block-storage volume (e.g. Vultr Block Storage, resizable later) mounted at `/srv/divan-files`, or an S3-compatible bucket mounted with rclone. Back it up with the database. The database keeps the details, indexed by poet and by a trigram index on the title (and the text of text books), so lists and search stay fast.

**Readers' addresses and rate limits.** Sign-in and sign-up are limited per address. The site passes the reader's address to the API in `x-client-ip`, which the API believes only from the site: set the same random `DIVAN_SITE_KEY` for both (e.g. `openssl rand -hex 32`); without it, only requests from the same machine count as the site. Keep the API off the internet (only the site talks to it), and let Caddy be the only way in: it replaces any `X-Forwarded-For` a visitor sends with their real address, which is what the site reads.

## Daily content sync (server)

`deploy/sync.sh` keeps a server current: it updates a divan-data checkout, fetches new and edited works from Wikisource (incremental, about a minute), rebuilds the export and upserts it into PostgreSQL. The site shows new content immediately. Runs are locked so they never overlap.

```sh
# crontab -e   (daily at 03:15; DATABASE_URL as for the API)
15 3 * * * DATABASE_URL=postgres://divan:...@localhost:5432/divan /opt/divan/deploy/sync.sh >> /var/log/divan-sync.log 2>&1
```

The same script keeps the word dictionary current (`npm run dict-sync` in `api/`): the full Wiktionary data for Urdu, Persian and Arabic (English Wiktionary via [kaikki.org](https://kaikki.org), and the Urdu, Persian and Arabic Wiktionary dumps) is re-imported when upstream publishes new files, and each day's Wiktionary edits are applied from recent changes. The first run downloads about 700 MB.

Settings: `DIVAN_DATA_DIR` (default `/opt/divan-data`, cloned on first run), `DIVAN_APP_DIR` (default: this repo), `DIVAN_DATA_PUSH=1` to also commit and push data changes (needs git push access). Needs git, python3 and Node 24+.

## API

| Endpoint | Returns |
|---|---|
| `GET /api/poets` | all poets |
| `GET /api/page?url=/p238/...` | the poet, category or poem at a site URL (breadcrumbs, children, verses, prev/next) |
| `GET /api/search?q=&poet=1,2&page=` | poems containing all words (or a `"quoted phrase"`), Urdu-normalised; exact phrase first; each with the best-matching couplet or paragraph (`snippet`); optionally only some poets/writers; plus `authors` (who the results come from, with counts), and on page 1 `poets` (by name) and `books` (books/chapters by title) |
| `GET /api/word?w=` | one word's meanings and pronunciation from the local Wiktionary data (Urdu, Persian, Arabic in that order; English meanings; Urdu equivalents via English when Urdu Wiktionary has none) |
| `/api/auth/*` | accounts: sign-up, sign-in (returns a Bearer token), profile, password, delete |
| `/api/mod/*` | moderation: what a moderator may do, queue, drafts, save/submit/approve/return/reject/publish, a work's history, activity log |
| `/api/library/*` | the signed-in reader's library: toggle poets, works, couplets and words; list with full paths; notes |
| `GET /health` | database check |

Search normalises both stored text and queries: Arabic ي/ك/ه → Urdu ی/ک/ہ, ۂ/ۓ, diacritics and the Urdu full stop removed; do-chashmi ھ stays distinct.

## Roadmap

v2 is reaching parity with Ganjoor's features in phases: reading, accounts, community (comments, bookmarks), editorial tools, recitations, then operations and deployment.

## License

GPL-3.0 (see `LICENSE`), with additional terms under its section 7 (see `NOTICE`): keep the attribution "Based on Divan by Muhammad Anas Rashid", mark modified versions as different, and do not use Divan's name and logos for your own public site ([TRADEMARKS.md](TRADEMARKS.md); set your own in `web/src/lib/brand.ts`). Texts are public domain; the divan-data compilation is CC BY-SA 4.0 (Urdu Wikisource and Wikipedia contributors, and Divan).
