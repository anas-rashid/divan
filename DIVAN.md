# دیوان · Divan site

This is a fork of [GanjoorService](https://github.com/ganjoor/GanjoorService) (GPL-3.0), the software behind ganjoor.net, adapted to serve **classical Urdu poetry and prose** from [divan-data](https://github.com/anas-rashid/divan-data).

## Changes from upstream

- **Branding:** "گنجور" becomes "دیوان" throughout the site. Upstream is credited in the footer.
- **Urdu basics:** pages are `lang="ur"`. The home page, footer and century groups (Hijri centuries, e.g. "تیرہویں صدی ہجری") are in Urdu. Deeper pages, such as admin and account pages, are still Persian.
- **Fonts:** Noto Nastaliq Urdu by default, with a **نستعلیق / نسخ** switch (Noto Naskh Arabic) at the bottom left. The choice is remembered per browser.
- **Footer:** links to Ganjoor-only services (Hafez divination, music index, etc.) are removed. Links to the Wikisource source, the data and the code are added.
- **Linux/Docker:** `Dockerfile` + `docker-compose.yml` (SQL Server 2022, API, site, Caddy for HTTPS). The code itself is unchanged apart from the text above.

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

1. Open `https://SITE_DOMAIN/login` and sign in with `ADMIN_EMAIL` and any password that meets the password rules. That first login creates the admin account.
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
dotnet build RMuseum/RMuseum.csproj -p:EnableWindowsTargeting=true
dotnet build GanjooRazor/GanjooRazor.csproj -p:EnableWindowsTargeting=true
```

Running it needs SQL Server, so use the Docker setup above. SQL Server's image is x86-64 only.

## License

GPL-3.0, same as upstream (see `LICENSE`). Data: see [divan-data](https://github.com/anas-rashid/divan-data).
