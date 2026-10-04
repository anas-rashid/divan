#!/usr/bin/env bash
# Restore a backup made by deploy/backup.sh.
#   deploy/restore.sh backups/divan-<stamp>.bak               (replaces database "divan"; stop the api first)
#   TARGET_DB=divan_restoretest deploy/restore.sh <file>      (restore side by side, e.g. to test a backup)
# DB_CONTAINER as in backup.sh. Needs MSSQL_SA_PASSWORD.
set -euo pipefail
cd "$(dirname "$0")/.."
[ -f .env ] && set -a && . ./.env && set +a
: "${MSSQL_SA_PASSWORD:?set MSSQL_SA_PASSWORD}"
BAK=${1:?usage: deploy/restore.sh <backup file>}
TARGET=${TARGET_DB:-divan}
C=${DB_CONTAINER:-$(docker compose ps -q db)}
SQL() { docker exec "$C" /opt/mssql-tools18/bin/sqlcmd -C -b -S localhost -U sa -P "$MSSQL_SA_PASSWORD" "$@"; }
docker exec "$C" mkdir -p /var/opt/mssql/backup
docker cp "$BAK" "$C:/var/opt/mssql/backup/restore.bak"
docker exec -u 0 "$C" chown mssql /var/opt/mssql/backup/restore.bak  # docker cp keeps the host uid; sqlservr runs as mssql
# logical file names inside the backup, moved to files named after the target database
FILES=$(SQL -h -1 -W -s '|' -Q "SET NOCOUNT ON; RESTORE FILELISTONLY FROM DISK = N'/var/opt/mssql/backup/restore.bak'" | awk -F'|' 'NF>2 {print $1"|"$3}')
[ -n "$FILES" ] || { echo "could not read the backup's file list" >&2; exit 1; }
MOVE=""
while IFS='|' read -r logical type; do
  [ -z "$logical" ] && continue
  ext=mdf; [ "$type" = L ] && ext=ldf
  MOVE="$MOVE, MOVE N'$logical' TO N'/var/opt/mssql/data/${TARGET}_${logical}.$ext'"
done <<< "$FILES"
SQL -Q "IF DB_ID(N'$TARGET') IS NOT NULL ALTER DATABASE [$TARGET] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
        RESTORE DATABASE [$TARGET] FROM DISK = N'/var/opt/mssql/backup/restore.bak' WITH REPLACE, CHECKSUM$MOVE;
        ALTER DATABASE [$TARGET] SET MULTI_USER;"
docker exec "$C" rm -f /var/opt/mssql/backup/restore.bak
echo "restored $BAK into [$TARGET]"
