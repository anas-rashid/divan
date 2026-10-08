// Divan API client (server-side; API_URL defaults to the local API)
const API = process.env.API_URL ?? 'http://127.0.0.1:4100';

export async function api<T = any>(path: string): Promise<T | null> {
  const res = await fetch(API + path);
  if (res.status === 404) return null;
  if (!res.ok) throw new Error(`API ${res.status} for ${path}`);
  return res.json() as Promise<T>;
}
