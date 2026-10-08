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

Settings: `DATABASE_URL` (API, default `postgres://divan:divan_local@localhost:5433/divan`), `PORT`/`HOST`; `API_URL` (web, default `http://127.0.0.1:4100`).

The import upserts, so re-running it after a divan-data sync applies the changes.

## Daily content sync (server)

`deploy/sync.sh` keeps a server current: it updates a divan-data checkout, fetches new and edited works from Wikisource (incremental, about a minute), rebuilds the export and upserts it into PostgreSQL. The site shows new content immediately. Runs are locked so they never overlap.

```sh
# crontab -e   (daily at 03:15; DATABASE_URL as for the API)
15 3 * * * DATABASE_URL=postgres://divan:...@localhost:5432/divan /opt/divan/deploy/sync.sh >> /var/log/divan-sync.log 2>&1
```

Settings: `DIVAN_DATA_DIR` (default `/opt/divan-data`, cloned on first run), `DIVAN_APP_DIR` (default: this repo), `DIVAN_DATA_PUSH=1` to also commit and push data changes (needs git push access). Needs git, python3 and Node 24+.

## API

| Endpoint | Returns |
|---|---|
| `GET /api/poets` | all poets |
| `GET /api/page?url=/p238/...` | the poet, category or poem at a site URL (breadcrumbs, children, verses, prev/next) |
| `GET /api/search?q=&poet=&page=` | poems containing all words (or a `"quoted phrase"`), Urdu-normalised; exact phrase first; each with the best-matching couplet or paragraph (`snippet`) |
| `GET /health` | database check |

Search normalises both stored text and queries: Arabic ي/ك/ه → Urdu ی/ک/ہ, ۂ/ۓ, diacritics and the Urdu full stop removed; do-chashmi ھ stays distinct.

## Roadmap

v2 is reaching parity with Ganjoor's features in phases: reading, accounts, community (comments, bookmarks), editorial tools, recitations, then operations and deployment.

## License

GPL-3.0 (see `LICENSE`). Texts are public domain; the divan-data compilation is CC BY-SA 4.0 (Urdu Wikisource and Wikipedia contributors).
