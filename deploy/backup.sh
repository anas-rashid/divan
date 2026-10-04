#!/usr/bin/env bash
# Back up the Divan SQL Server database to ./backups/divan-<UTC timestamp>.bak
#   deploy/backup.sh                       (docker-compose stack: service "db")
#   DB_CONTAINER=divan-mssql deploy/backup.sh   (local run-local.sh container)
# Needs MSSQL_SA_PASSWORD (read from .env when present).
set -euo pipefail
cd "$(dirname "$0")/.."
[ -f .env ] && set -a && . ./.env && set +a
: "${MSSQL_SA_PASSWORD:?set MSSQL_SA_PASSWORD}"
DB=${DB_NAME:-divan}
C=${DB_CONTAINER:-$(docker compose ps -q db)}
STAMP=$(date -u +%Y%m%dT%H%M%SZ)
FILE=/var/opt/mssql/backup/$DB-$STAMP.bak
docker exec "$C" mkdir -p /var/opt/mssql/backup
docker exec "$C" /opt/mssql-tools18/bin/sqlcmd -C -b -S localhost -U sa -P "$MSSQL_SA_PASSWORD" \
  -Q "BACKUP DATABASE [$DB] TO DISK = N'$FILE' WITH INIT, CHECKSUM"
mkdir -p backups
docker cp "$C:$FILE" "backups/$DB-$STAMP.bak"
docker exec "$C" rm -f "$FILE"
echo "backups/$DB-$STAMP.bak"
