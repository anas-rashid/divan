import { defineConfig } from 'astro/config';
import node from '@astrojs/node';

// Server-rendered pages (content comes from the Divan API at request time)
export default defineConfig({
  output: 'server',
  adapter: node({ mode: 'standalone' }),
  server: { port: 4200 },
  // Form posts from other sites are refused (CSRF). Astro only trusts the Host header for these
  // hostnames, so the production domain must be listed (SITE_HOSTS at build time, comma-separated).
  security: {
    checkOrigin: true,
    allowedDomains: (process.env.SITE_HOSTS ?? '127.0.0.1,localhost').split(',').map((h) => ({ hostname: h.trim() })),
  },
  i18n: undefined,
});
