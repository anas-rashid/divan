import { defineConfig } from 'astro/config';
import node from '@astrojs/node';
import { execSync } from 'node:child_process';

// the site's version, in the footer: DIVAN_VERSION from the deploy (its copy of the code has no git history), else git's
// own here. «v1.3.0», or «v1.3.0+4» four commits after it
const described = process.env.DIVAN_VERSION ||
  (() => { try { return execSync('git describe --tags --always', { stdio: ['ignore', 'pipe', 'ignore'] }).toString().trim(); } catch { return ''; } })();
const version = described.replace(/^(v[\d.]+)-(\d+)-g[0-9a-f]+$/, '$1+$2');

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
  vite: { define: { __DIVAN_VERSION__: JSON.stringify(version) } },
});
