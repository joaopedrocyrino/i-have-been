export const DEFAULT_BUDGET = 512 * 1024 * 1024;
export const MAX_FILE = 100 * 1024 * 1024;
export const MAX_JOURNAL = 10 * 1024 * 1024;
export const KDF_ITERATIONS = 600000;
export const LOCK_AFTER_MS = 15 * 60 * 1000;
const guid = /^[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}$/i;
const types = new Set(['image/jpeg', 'image/png', 'image/webp', 'video/mp4', 'video/quicktime', 'video/webm']);
export function mediaPath(file) {
  if (!guid.test(file.id) || file.url !== `/api/media/${file.id}` || !types.has(file.contentType)
    || !Number.isSafeInteger(file.byteLength) || file.byteLength < 1 || file.byteLength > MAX_FILE
    || !/^[a-f0-9]{64}$/i.test(file.sha256)) throw new Error('This file cannot be saved safely for offline use.');
  return file.url;
}
export function planMedia(logs, ids, photos, videos, budget = DEFAULT_BUDGET) {
  const chosen = new Set(ids), files = new Map();
  for (const log of logs) if (chosen.has(log.id)) for (const file of log.media) {
    if ((photos && file.contentType.startsWith('image/')) || (videos && file.contentType.startsWith('video/'))) {
      mediaPath(file); files.set(file.id, file);
    }
  }
  const result = [...files.values()];
  if (result.reduce((sum, file) => sum + file.byteLength + 28, 0) + new TextEncoder().encode(JSON.stringify(logs)).byteLength + 65536 > budget)
    throw new Error('The selected originals exceed the offline storage limit. Select fewer memories or exclude videos.');
  return result;
}
export function requirePassphrase(value) {
  if (typeof value !== 'string' || value.length < 12 || value.length > 1024)
    throw new Error('Use an offline passphrase of at least 12 characters.');
}
export function equalHash(buffer, expected) {
  return Array.from(new Uint8Array(buffer), byte => byte.toString(16).padStart(2, '0')).join('') === expected.toLowerCase();
}

export function localTilePattern(tiles) {
  if (tiles?.offlineCache !== true || !/^\/tiles\/(?:[a-zA-Z0-9_-]+\/)*\{z\}\/\{x\}\/\{y\}\.(png|jpg|jpeg|webp)$/.test(tiles.url || '')) return null;
  return new RegExp('^' + tiles.url.replace(/[.*+?^${}()|[\]\\]/g, '\\$&').replaceAll('\\{z\\}', '[0-9]+').replaceAll('\\{x\\}', '[0-9]+').replaceAll('\\{y\\}', '[0-9]+') + '$');
}
