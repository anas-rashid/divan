import { defineConfig } from 'astro/config';
import node from '@astrojs/node';

// Server-rendered pages (content comes from the Divan API at request time)
export default defineConfig({
  output: 'server',
  adapter: node({ mode: 'standalone' }),
  server: { port: 4200 },
  i18n: undefined,
});
