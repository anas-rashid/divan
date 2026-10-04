#!/usr/bin/env bash
# Run the full Divan app locally (macOS/Linux): SQL Server in Docker + API + site, natively with dotnet.
#   ./run-local.sh            start everything (site http://localhost:5200, API http://localhost:5100)
#   ./run-local.sh import     also import divan-data (from $DIVAN_DATA, default ../divan, else the public CDN)
#   ./run-local.sh stop       stop API + site (SQL Server container keeps running)
# Needs: docker (on Apple Silicon: colima start --vm-type vz --vz-rosetta --memory 6), .NET SDK 10.0.302, python3.
# First admin: admin@divan.local / Test!123 (change it in the user panel).
set -euo pipefail
cd "$(dirname "$0")"
ROOT=$PWD
RUN=${DIVAN_LOCAL:-$HOME/divan-local}
SA_PASSWORD=${MSSQL_SA_PASSWORD:-Divan_local_2026!}
DATA=${DIVAN_DATA:-$ROOT/../divan}
export PATH=$HOME/.dotnet:$PATH DOTNET_CLI_TELEMETRY_OPTOUT=1
mkdir -p "$RUN"/{logs,keys,museum,museum-trash,tempaudio,audio,export}

stop() { pkill -f "dotnet RMuseum.dll" || true; pkill -f "dotnet DivanRazor.dll" || true; }
if [ "${1:-}" = stop ]; then stop; echo stopped; exit 0; fi

# 1. SQL Server (x86-64 image; runs under Rosetta on Apple Silicon)
if ! docker ps --format '{{.Names}}' | grep -qx divan-mssql; then
  docker start divan-mssql 2>/dev/null || docker run -d --name divan-mssql --platform linux/amd64 \
    -e ACCEPT_EULA=Y -e MSSQL_PID=Express -e "MSSQL_SA_PASSWORD=$SA_PASSWORD" -p 1433:1433 \
    -v divan-mssql:/var/opt/mssql --restart unless-stopped mcr.microsoft.com/mssql/server:2022-latest
  echo "waiting for SQL Server..."; until docker logs divan-mssql 2>&1 | grep -q "ready for client connections"; do sleep 3; done
fi

# 2. publish (from RMuseum/: its global.json pins the SDK newer Razor compilers can't build with)
stop
(cd RMuseum && dotnet publish RMuseum.csproj -c Release -o "$RUN/api" -p:EnableWindowsTargeting=true -v q \
  && dotnet publish ../DivanRazor/DivanRazor.csproj -c Release -o "$RUN/site" -p:EnableWindowsTargeting=true -v q)
git checkout -- RMuseum/RMuseum.xml 2>/dev/null || true  # build regenerates this tracked doc file

# 3. API (settings that RSecurityBackend only reads from appsettings.json are written into the published copy)
export ConnectionStrings__DefaultConnection="Server=localhost,1433;Database=divan;User Id=sa;Password=$SA_PASSWORD;TrustServerCertificate=True;MultipleActiveResultSets=true"
export Security__Secret="local-dev-only-divan-secret-key-0123456789abcdef"
export RSecurityBackend__ApplicationName=Divan RSecurityBackend__FirstUserEmail=admin@divan.local
export WebServiceUrl=http://localhost:5100 SignUp__Enabled=False
export PictureFileService__StoragePath=$RUN/museum PictureFileService__TrashStoragePath=$RUN/museum-trash
export AudioUploadService__TempUploadPath=$RUN/tempaudio AudioUploadService__LocalAudioRepositoryPath=$RUN/audio
export Divan__SitemapLocation=$RUN/sitemap.xml PublicDataExport__LocalWorkingCopyPath=$RUN/export
(cd "$RUN/api" && python3 - <<'PY'
import json, os
f = 'appsettings.json'; d = json.load(open(f, encoding='utf-8-sig'))
d['ConnectionStrings']['DefaultConnection'] = os.environ['ConnectionStrings__DefaultConnection']
d['Security']['Secret'] = os.environ['Security__Secret']
d['RSecurityBackend']['ApplicationName'] = os.environ['RSecurityBackend__ApplicationName']
d['RSecurityBackend']['FirstUserEmail'] = os.environ['RSecurityBackend__FirstUserEmail']
json.dump(d, open(f, 'w', encoding='utf-8'), ensure_ascii=False, indent=2)
PY
ASPNETCORE_URLS=http://localhost:5100 nohup dotnet RMuseum.dll > "$RUN/logs/api.log" 2>&1 &)
until grep -q "Now listening" "$RUN/logs/api.log" 2>/dev/null; do sleep 2; done
curl -s -o /dev/null http://localhost:5100/api/divan/poets  # first request creates/migrates the database

# 4. site
(cd "$RUN/site" && APIRoot=http://localhost:5100 GlobalAPIRoot=http://localhost:5100 SiteUrl=http://localhost:5200 \
  DataProtectionPersistPath=$RUN/keys TrackingScript="" SemanticSearchAPIRoot="" ASPNETCORE_URLS=http://localhost:5200 \
  nohup dotnet DivanRazor.dll > "$RUN/logs/site.log" 2>&1 &)
until grep -q "Now listening" "$RUN/logs/site.log" 2>/dev/null; do sleep 2; done

# 5. optional data import (background job; ~1 hour for all 11k poems; re-running only adds what's missing)
if [ "${1:-}" = import ]; then
  if [ -f "$DATA/manifest.json" ]; then SRC="{\"useHttp\":false,\"location\":\"$(cd "$DATA" && pwd)\",\"poetId\":0}"
  else SRC='{"useHttp":true,"location":"https://cdn.jsdelivr.net/gh/anas-rashid/divan-data@main/","poetId":0}'; fi
  TOKEN=$(curl -s -X POST http://localhost:5100/api/users/login -H 'Content-Type: application/json' \
    -d '{"username":"admin@divan.local","password":"Test!123","clientAppName":"run-local","language":"ur-PK"}' \
    | python3 -c "import json,sys; print(json.load(sys.stdin)['token'])")
  curl -sf -X POST http://localhost:5100/api/divan/publicdata/import -H "Authorization: Bearer $TOKEN" \
    -H 'Content-Type: application/json' -d "$SRC" && echo "import started (progress: Admin -> long running jobs)"
fi

echo "site: http://localhost:5200   API: http://localhost:5100/swagger   logs: $RUN/logs"
