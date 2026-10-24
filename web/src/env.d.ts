declare const __DIVAN_VERSION__: string; // astro.config.mjs

declare namespace App {
  interface Locals {
    user: import('./lib/auth').User | null;
  }
}
