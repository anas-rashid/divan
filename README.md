# دیوان · Divan site

Divan is a standalone project built on a fork of [GanjoorService](https://github.com/ganjoor/GanjoorService) (GPL-3.0), the software behind ganjoor.net, adapted to serve **classical Urdu poetry and prose** from [divan-data](https://github.com/anas-rashid/divan-data).

## Changes from upstream

- **Tajik removed:** the TajikGanjoor site, Tajik API endpoints, services, export and transliteration are gone. Migration `DivanRemoveTajik` drops their tables.

- **Branding:** "گنجور" becomes "دیوان" throughout the site. Upstream is credited in the footer.
- **Urdu basics:** pages are `lang="ur"`. The home page, footer and century groups (Hijri centuries, e.g. "تیرہویں صدی ہجری") are in Urdu. Deeper pages, such as admin and account pages, are still Persian.
- **Fonts:** Noto Nastaliq Urdu by default, with a **نستعلیق / نسخ** switch (Noto Naskh Arabic) at the bottom left. The choice is remembered per browser.
- **Footer:** links to Ganjoor-only services (Hafez divination, music index, etc.) are removed. Links to the Wikisource source, the data and the code are added.
- **Linux/Docker:** `Dockerfile` + `docker-compose.yml` (SQL Server 2022, API, site, Caddy for HTTPS).
- **Config fixes so env vars work:** the four places that read `appsettings.json` directly now also read environment variables. The JWT issuer follows `RSecurityBackend:ApplicationName` instead of the hard-coded "Ganjoor". `deploy/entrypoint.sh` copies the settings that RSecurityBackend reads only from `appsettings.json` (connection string, secret, app name, admin email) into the file at container start.
- **Links:** `ganjoor.net` links to the site's own pages are now relative. Links to Ganjoor's other services (blog, audio, etc.) are left as they are.
- **Locale:** `ur-PK`.

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

## Build locally (macOS/Linux)

```sh
cd RMuseum   # its global.json pins SDK 10.0.302; newer SDKs fail on some upstream Razor views
dotnet build RMuseum.csproj -p:EnableWindowsTargeting=true
dotnet build ../GanjooRazor/GanjooRazor.csproj -p:EnableWindowsTargeting=true
```

Running it needs SQL Server, so use the Docker setup above. SQL Server's image is x86-64 only.

## License

GPL-3.0, same as upstream (see `LICENSE`). Data: see [divan-data](https://github.com/anas-rashid/divan-data).
