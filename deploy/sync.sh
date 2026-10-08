#!/usr/bin/env bash
# Daily content sync for a Divan server: Wikisource -> divan-data -> PostgreSQL.
#
#   1. update the divan-data checkout and fetch new/edited works from Wikisource (incremental)
#   2. rebuild its search index and site export
#   3. load the export into PostgreSQL (upserts; the site reads it live)
#   3b. sync the word dictionary from Wiktionary (api/src/dict-sync.ts; first run imports ~700 MB)
#   4. optionally commit + push the refreshed data (DIVAN_DATA_PUSH=1, needs git push access)
#
# Schedule with cron, e.g. daily at 03:15:
#   15 3 * * * /opt/divan/deploy/sync.sh >> /var/log/divan-sync.log 2>&1
#
# Settings (env): DIVAN_DATA_DIR (default /opt/divan-data), DIVAN_APP_DIR (default: this repo),
#   DATABASE_URL (Postgres, as for the API), DIVAN_DATA_PUSH=1 to push data changes.
# Needs: git, python3 (stdlib only), node 24+, curl and bzcat (dictionary dumps).
set -euo pipefail

APP_DIR=${DIVAN_APP_DIR:-$(cd "$(dirname "$0")/.." && pwd)}
DATA_DIR=${DIVAN_DATA_DIR:-/opt/divan-data}
LOCK=${TMPDIR:-/tmp}/divan-sync.lock

# one run at a time (a slow Wikisource day must not overlap the next run)
exec 9>"$LOCK"
if command -v flock >/dev/null && ! flock -n 9; then echo "$(date -u +%FT%TZ) sync already running"; exit 0; fi

echo "== $(date -u +%FT%TZ) divan sync"
[ -d "$DATA_DIR/.git" ] || git clone -q https://github.com/anas-rashid/divan-data.git "$DATA_DIR"
cd "$DATA_DIR"
git pull -q --rebase   # keep commits made by publishing in the Divan app (git.ts)

if [ "${DIVAN_DATA_PUSH:-0}" = 1 ]; then
  ./update.sh                                   # fetch + rebuild, commit and push if the data changed
else
  python3 wikisource.py && python3 build_index.py && python3 export_divan.py
fi

cd "$APP_DIR/api"
[ -d node_modules ] || npm ci --omit=dev --silent
node src/import.ts "$DATA_DIR"

# word dictionary (Wiktionary): re-imports sources that changed upstream, then the day's edits.
# A failure here must not fail the content sync.
node src/dict-sync.ts || echo "$(date -u +%FT%TZ) dictionary sync failed"
echo "== $(date -u +%FT%TZ) done"
