# دیوان · Divan site

Divan is a standalone project built on a fork of [GanjoorService](https://github.com/ganjoor/GanjoorService) (GPL-3.0), the software behind ganjoor.net, adapted to serve **classical Urdu poetry and prose** from [divan-data](https://github.com/anas-rashid/divan-data).

## Changes from upstream

- **Renamed Ganjoor → Divan throughout:** projects (`DivanRazor`, `DivanService.sln`), files, folders, classes, settings (`Divan:` section), API routes (`/api/divan/...`) and database tables (`Divan*`). Real external addresses (ganjoor.net, github.com/ganjoor) are unchanged. Because table names changed, Divan needs a fresh database; it can't reuse a Ganjoor one.
- **Removed Ganjoor/Persian-specific features:** music (Spotify, Golha, Beeptunes, music index, song suggestions; DB models kept for a future Urdu version), Ganjoor's visit analytics, Turkish/Kurdish page options, abjad, Persian dictionary links. Random verse now picks from Divan's own data.
- **Tajik removed:** the TajikGanjoor site, Tajik API endpoints, services, export and transliteration are gone. Migration `DivanRemoveTajik` drops their tables.
- **Branding:** "گنجور" becomes "دیوان" throughout the site. Upstream is credited in the footer.
- **Urdu basics:** pages are `lang="ur-PK"`. The home page, footer and century groups (Hijri centuries, e.g. "تیرہویں صدی ہجری") are in Urdu. Deeper pages, such as admin and account pages, are still Persian.
- **Fonts:** Noto Nastaliq Urdu by default, with a **نستعلیق / نسخ** switch (Noto Naskh Arabic) at the bottom left. The choice is remembered per browser.
- **Footer:** links to Divan-only services (Hafez divination, music index, etc.) are removed. Links to the Wikisource source, the data and the code are added.
- **Linux/Docker:** `Dockerfile` + `docker-compose.yml` (SQL Server 2022, API, site, Caddy for HTTPS).
- **Config fixes so env vars work:** the four places that read `appsettings.json` directly now also read environment variables. The JWT issuer follows `RSecurityBackend:ApplicationName` instead of the hard-coded "Divan". `deploy/entrypoint.sh` copies the settings that RSecurityBackend reads only from `appsettings.json` (connection string, secret, app name, admin email) into the file at container start.
- **Links:** `ganjoor.net` links to the site's own pages are now relative. Links to Divan's other services (blog, audio, etc.) are left as they are.
- **Locale:** `ur-PK`.

## Static reader (no server)

`reader/index.html` is a single-file reader: poets, intros, books and poems, with Nastaliq/Naskh switching. It reads the [divan-data](https://github.com/anas-rashid/divan-data) static API directly in the browser, so it needs no API, database or build step.

```sh
# against the public CDN: just open reader/index.html in a browser, or host it anywhere (e.g. GitHub Pages)
# against a local divan-data checkout:
mkdir -p www && ln -s "$PWD/reader/index.html" www/ && ln -s /path/to/divan-data www/data
python3 -m http.server 5300 -d www     # open http://localhost:5300/?data=data/
```

## Deploy (Ubuntu/Debian x86-64, e.g. Vultr)

```sh
# 1. Docker
curl -fsSL https://get.docker.com | sh

# 2. Code + config
git clone https://github.com/anas-rashid/divan.git && cd divan
cp .env.example .env && nano .env      # domains, passwords, admin email

# 3. DNS: point SITE_DOMAIN and API_DOMAIN (A records) at the server, then:
docker compose up -d --build           # first build takes a few minutes
docker compose logs -f api             # wait for "Application started"
```

SQL Server needs about 2 GB of RAM. Use a plan with at least 4 GB in total.

### Load the data

1. Open `https://SITE_DOMAIN/login` and sign in with `ADMIN_EMAIL` and the password **`Test!123`**. The first login creates the admin account with that fixed password (RSecurityBackend's default; upstream's guide is wrong about this). **Change it right away** in the user panel.
2. On the import page that opens (or **Admin → مالی و سایت → درون‌ریزی دادهٔ عمومی**), choose **Internet URL** and enter:
   ```
   https://cdn.jsdelivr.net/gh/anas-rashid/divan-data@main/
   ```
3. The import runs in the background (about 11k poems). Century groups are rebuilt automatically when it finishes.

Re-running the import adds new poems and leaves existing ones untouched, so it can be repeated after divan-data's daily sync.

### Update

```sh
git pull && docker compose up -d --build
```

## Run locally (macOS/Linux)

```sh
./run-local.sh import   # SQL Server container + API + site, then imports divan-data (~1 h, background)
./run-local.sh          # later runs: rebuild + start
./run-local.sh stop
```

Site: http://localhost:5200 · API: http://localhost:5100/swagger · admin `admin@divan.local` / `Test!123`. On Apple Silicon, start Docker via `colima start --vm-type vz --vz-rosetta --memory 6` first (SQL Server is x86-64 only).

## Build locally (macOS/Linux)

```sh
cd RMuseum   # its global.json pins SDK 10.0.302; newer SDKs fail on some upstream Razor views
dotnet build RMuseum.csproj -p:EnableWindowsTargeting=true
dotnet build ../DivanRazor/DivanRazor.csproj -p:EnableWindowsTargeting=true
```

Running it needs SQL Server, so use the Docker setup above. SQL Server's image is x86-64 only.

## License

GPL-3.0, same as upstream (see `LICENSE`). Data: see [divan-data](https://github.com/anas-rashid/divan-data).
