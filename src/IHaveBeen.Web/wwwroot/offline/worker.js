import { clearDatabase, getMetadata } from './offline/database.js';
import { localTilePattern } from './offline/policy.js';
const PREFIX = 'ihb-public-';
const TILES = 'ihb-self-hosted-tiles-v1';
const CORE = new Set([
  '/offline/index.html','/offline/offline.css','/offline/app.js','/offline/render.js','/offline/database.js',
  '/offline/crypto.js','/offline/policy.js','/offline/register.js','/offline/manifest.webmanifest',
  '/map-settings.json','/app.css','/theme.js','/journal.js','/map.js','/favicon.svg','/assets/countries.geo.json',
  '/vendor/leaflet/leaflet.js','/vendor/leaflet/leaflet.css',
  '/vendor/leaflet/images/layers.png','/vendor/leaflet/images/layers-2x.png',
  '/vendor/leaflet/images/marker-icon.png','/vendor/leaflet/images/marker-icon-2x.png','/vendor/leaflet/images/marker-shadow.png',
  '/Components/Map/MapCanvas.razor.rz.scp.css','/Components/Map/MapHeader.razor.rz.scp.css',
  '/Components/Layout/ThemeSwitcher.razor.rz.scp.css','/Components/Journal/JournalShell.razor.rz.scp.css',
  '/Components/Journal/JournalPanel.razor.rz.scp.css','/Components/Journal/MemoryDetails.razor.rz.scp.css',
  '/Components/Experiences/ExperienceList.razor.rz.scp.css','/Components/Experiences/StarRating.razor.rz.scp.css'
]);
let tilePattern, tileQueue = Promise.resolve();
async function name() { return PREFIX + REVISION; }
self.addEventListener('install', event => event.waitUntil((async () => {
  const response = await fetch('/offline-assets.json', { cache: 'no-store' });
  if (!response.ok) throw new Error('Offline assets unavailable');
  const manifest = await response.json();
  if (!/^[a-f0-9]{64}$/.test(manifest.version) || manifest.version !== REVISION || manifest.assets.length !== CORE.size || !manifest.assets.every(path => CORE.has(path)))
    throw new Error('Invalid public asset manifest');
  const cacheName = PREFIX + manifest.version, cache = await caches.open(cacheName);
  try {
    await cache.addAll(manifest.assets.map(path => new Request(path, { cache: 'reload', credentials: 'omit' })));
  } catch (error) { await caches.delete(cacheName); throw error; }
  // An update activates after existing tabs close, preserving one complete asset version.
})()));
self.addEventListener('activate', event => event.waitUntil((async () => {
  const current = await name();
  for (const key of await caches.keys()) if (key.startsWith(PREFIX) && key !== current) await caches.delete(key);
  await self.clients.claim();
})()));
async function notify(message) {
  for (const client of await self.clients.matchAll({ type: 'window', includeUncontrolled: true })) client.postMessage({ type: message });
}
self.addEventListener('message', event => {
  if (event.data?.type === 'FORGET_OFFLINE') event.waitUntil(Promise.all([clearDatabase(), caches.delete(TILES)]).then(() => notify('OFFLINE_REMOVED')));
});
async function cached(path) {
  const current = await name();
  return current ? (await caches.open(current)).match(path) : null;
}
async function tileAllowed(path) {
  if (tilePattern === undefined) {
    const response = await cached('/map-settings.json');
    const tiles = response ? (await response.json()).tiles : null;
    // Never store third-party tiles or opaque responses. Local coordinates only, no user IDs or query strings.
    tilePattern = localTilePattern(tiles);
  }
  return tilePattern?.test(path) || false;
}
async function tile(request) {
  const cache = await caches.open(TILES), hit = await cache.match(request);
  if (hit) return hit;
  const response = await fetch(request);
  if (response.ok && response.type !== 'opaque' && /^image\/(png|jpeg|webp)$/.test(response.headers.get('content-type')?.split(';')[0] || '')) {
    const bytes = await response.clone().arrayBuffer();
    if (bytes.byteLength <= 512 * 1024) {
      tileQueue = tileQueue.catch(() => {}).then(async () => {
        await cache.put(request, response.clone());
        const keys = await cache.keys();
        for (const old of keys.slice(0, Math.max(0, keys.length - 128))) await cache.delete(old);
      });
      await tileQueue.catch(() => {});
    }
  }
  return response;
}
self.addEventListener('fetch', event => {
  const request = event.request, url = new URL(request.url);
  if (url.origin !== self.location.origin) return;
  if (url.pathname === '/account/logout' && request.method === 'POST') {
    event.respondWith((async () => { await clearDatabase(); await caches.delete(TILES); await notify('OFFLINE_REMOVED'); return fetch(request); })()); return;
  }
  if (['/account/login', '/account/register'].includes(url.pathname) && request.method === 'POST') {
    event.respondWith((async () => {
      const response = await fetch(request), stored = await getMetadata();
      if (stored) {
        const session = await fetch('/api/offline/session', { credentials: 'same-origin', cache: 'no-store' });
        if (session.ok && (await session.json()).ownerId !== stored.ownerId) {
          await clearDatabase(); await caches.delete(TILES); await notify('OFFLINE_REMOVED');
        }
      }
      return response;
    })()); return;
  }
  if (request.method !== 'GET') return;
  if (CORE.has(url.pathname) && !url.search) {
    event.respondWith((async () => {
      const client = event.clientId ? await self.clients.get(event.clientId) : null;
      const offlinePage = url.pathname === '/offline/index.html' || (client && new URL(client.url).pathname.startsWith('/offline/'));
      if (offlinePage) return (await cached(url.pathname)) || fetch(request);
      try { return await fetch(request); }
      catch { return (await cached(url.pathname)) || new Response('This asset is unavailable offline.', { status: 503 }); }
    })()); return;
  }
  if (request.mode === 'navigate') {
    // Owner navigation may fall back to the public locked shell; invitations and login must stay online.
    if (url.pathname !== '/' && url.pathname !== '/offline/index.html') return;
    event.respondWith((async () => {
      try { return await fetch(request); }
      catch { return (await cached('/offline/index.html')) || new Response('Open this app online once to enable offline access.', { status: 503 }); }
    })()); return;
  }
  if (url.pathname.startsWith('/tiles/') && !url.search)
    event.respondWith((async () => await tileAllowed(url.pathname) ? tile(request) : fetch(request))());
  // API/media/shared content is deliberately network-only, including range/download URLs.
});
