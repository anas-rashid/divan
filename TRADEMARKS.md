# Divan's name and logos

The code is free software (GNU GPL v3, with the additional terms in [NOTICE](NOTICE)), and you are welcome to
study it, change it and run your own site with it. Divan's **name and logos are not part of that licence**:
they identify this project and its site, so that readers know what is Divan and what is not.

## What belongs to Divan

- The name **دیوان / Divan** used for this project, its site and its apps.
- The logos and icons: the shamsa (illuminated medallion) and the ترنج (lobed medallion), in every form:
  `web/public/logo/`, `web/public/favicon.*`, `web/public/icon-*.png`, `web/public/apple-touch-icon.png`,
  `web/public/og-image.png`, and the logo drawn in the header and home page.
- The tagline **ایک پاکستانی کی طرف سے، پاکستان کے لیے** with Pakistan's flag in the footer.
- The masthead (the logo beside **اردو کا کلاسیکی ادب** between the couplet lines) and the share-card design.

## What you may do

- Say that your work is **based on Divan**, link to it, and describe what Divan is.
- Run Divan unchanged for yourself or for testing, privately.
- Use the name when writing about the project (articles, reviews, talks).

## What you may not do

- Run a public site, app or service from a modified copy under the Divan name or with its logos.
- Use a name, domain, logo or design that could make people think your site is Divan or is endorsed by it.
- Remove the attribution required by [NOTICE](NOTICE).

## Making your own site

1. Set your name and taglines in `web/src/lib/brand.ts`.
2. Replace the logo files listed above with your own, and the logo in the header (`web/src/layouts/Base.astro`)
   and home page (`web/src/pages/index.astro`).
3. Rewrite the about page (`web/src/pages/about.astro`) for your project, keeping the attribution
   "Based on Divan by Anas Rashid" from [NOTICE](NOTICE).
4. Update `web/public/site.webmanifest`.

Questions or permission requests: open an issue on the Divan repository.
