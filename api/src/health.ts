// Disk space for /api/health (and so the uptime monitor): the file system holding Divan's data (the e-book store, or
// the API's own folder) counts as full above DIVAN_DISK_MAX_USE percent used (default 90), well before a full disk
// stops PostgreSQL (2026-10-10: the server's disk filled and the site was down for hours).
import { statfs } from 'node:fs/promises';

export async function disk(path = process.env.DIVAN_FILES_DIR ?? process.cwd(), max = Number(process.env.DIVAN_DISK_MAX_USE ?? 90)) {
  const s = await statfs(path);
  const used = Math.round(100 - (s.bavail / s.blocks) * 100); // bavail: what non-root users may still write
  return { used, full: used >= max };
}
