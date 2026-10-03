import { chromium } from 'playwright';
import assert from 'node:assert/strict';
import { randomUUID, createHash } from 'node:crypto';
import { mkdir } from 'node:fs/promises';
const base = process.env.BASE_URL || 'http://localhost:4193';
const address = new URL(base);
if (!((address.hostname === 'localhost' && address.port === '4193') || (address.hostname === 'localhost' && address.port === '8080' && /(?:ci|check|test)/i.test(process.env.OFFLINE_TEST_PROJECT || ''))))
  throw new Error('Run this suite only against the isolated offline test stack.');
const browser = await chromium.launch({ headless: true, args: ['--no-sandbox'] });
const context = await browser.newContext({ viewport: { width: 1440, height: 960 } });
await context.route(/tile\.openstreetmap\.org/, route => route.abort());
const page = await context.newPage(); page.setDefaultTimeout(30000);
const errors = []; page.on('pageerror', error => errors.push(error.message));
let count = 0; const check = (value, label) => { assert.ok(value, label); count++; console.log('PASS', label); };
const passphrase = 'Device-offline-passphrase!2026';
const png = Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIHWP4z8DwHwAFgAI/ScLbtAAAAABJRU5ErkJggg==','base64');
const emailA = randomUUID() + '@example.test';
async function register(email, target = page) {
  await target.goto(base + '/register'); await target.getByLabel('Your name').fill('Offline traveler');
  await target.getByLabel('Email', { exact: true }).fill(email); await target.getByLabel('Password', { exact: true }).fill('Travel-Test!2026');
  await target.getByRole('button', { name: 'Start my journal', exact: false }).click(); await target.waitForURL(base + '/');
}
async function api(method, path, data) {
  return page.evaluate(async ({ method, path, data }) => {
    const headers = {};
    if (method !== 'GET') { headers['X-CSRF-TOKEN'] = (await (await fetch('/api/csrf', { cache: 'no-store' })).json()).token; headers['Content-Type'] = 'application/json'; }
    const response = await fetch(path, { method, headers, cache: 'no-store', ...(data === undefined ? {} : { body: JSON.stringify(data) }) });
    return { status: response.status, body: response.status === 204 ? null : response.headers.get('content-type')?.includes('json') ? await response.json() : await response.text() };
  }, { method, path, data });
}
async function unlock(value = passphrase) {
  await page.getByLabel('Offline passphrase', { exact: true }).fill(value);
  await page.getByRole('button', { name: 'Unlock journal', exact: true }).click();
  await page.waitForFunction(() => document.getElementById('gate').hidden);
}
async function openSaved() {
  await page.getByRole('button', { name: 'Saved copy', exact: true }).click();
  await page.waitForFunction(() => !document.getElementById('settings').hidden);
}
async function closeSettings() { await page.getByRole('button', { name: 'Close offline settings' }).click(); }
async function cachePaths() {
  return page.evaluate(async () => (await Promise.all((await caches.keys()).map(async key => (await (await caches.open(key)).keys()).map(request => new URL(request.url).pathname)))).flat());
}
try {
  for (let attempt = 0; attempt < 120; attempt++) {
    try { if ((await fetch(base + '/health/ready')).ok) break; } catch {}
    await new Promise(resolve => setTimeout(resolve, 1000));
  }
  check((await fetch(base + '/api/offline/session')).status === 401, 'Offline identity requires authentication');
  const manifest = await (await fetch(base + '/offline-assets.json')).json();
  check(/^[a-f0-9]{64}$/.test(manifest.version) && manifest.assets.length > 20, 'Public asset manifest has a content-derived revision');
  check(!manifest.assets.some(path => /api\/|account\/|^\/$|^\/s\//.test(path)), 'Asset manifest excludes private routes and authenticated HTML');
  await register(emailA);
  const input = { title: 'Offline Rio memory <img src=x onerror=alert(1)>', description: 'Saved private details', city: 'Rio de Janeiro', country: 'Brazil', latitude: -22.9068, longitude: -43.1729, visitedOn: '2026-05-24', includeInShares: true };
  const created = await api('POST', '/api/logs', input); check(created.status === 201, 'Owner creates a real travel log'); const log = created.body;
  check((await api('POST', `/api/logs/${log.id}/experiences`, { title: 'Museum offline', description: 'A saved city experience', category: 4, rating: 5, address: 'Centro' })).status === 201, 'Experience and star rating created');
  check((await api('POST', '/api/logs', { ...input, title: 'Kyoto someday', description: '', city: 'Kyoto', country: 'Japan', latitude: 35, longitude: 135, isWishlist: true, visitedOn: null })).status === 201, 'Wishlist created');
  const upload = await page.evaluate(async ({ id, bytes }) => {
    const form = new FormData(); form.append('file', new Blob([new Uint8Array(bytes)], { type: 'image/png' }), 'original.png');
    const token = (await (await fetch('/api/csrf')).json()).token;
    const response = await fetch(`/api/logs/${id}/media`, { method: 'POST', headers: { 'X-CSRF-TOKEN': token }, body: form });
    return { status: response.status, body: await response.json() };
  }, { id: log.id, bytes: [...png] });
  check(upload.status === 201, 'Original image uploaded through mandatory antivirus into real Garage');
  const share = await api('POST', '/api/shares', { label: 'Offline exclusion check', expiresAt: null }); check(share.status === 200, 'Live invitation created for exclusion checks');
  await page.goto(base + '/offline/index.html'); await page.waitForFunction(() => document.getElementById('gate-title').textContent.includes('Take your memories'));
  await page.waitForFunction(() => !!navigator.serviceWorker.controller);
  check(await page.locator('#map .country-shape').count() >= 298, 'Country map loads in the standalone browser shell');
  await page.getByLabel('Offline passphrase', { exact: true }).fill(passphrase); await page.getByLabel('Confirm offline passphrase', { exact: true }).fill(passphrase);
  await page.getByRole('button', { name: 'Enable offline access' }).click();
  await page.waitForFunction(() => !document.getElementById('settings').hidden);
  check(await page.locator('#logs .log-card').count() === 2, 'Encrypted initial snapshot saves complete logs and wishes');
  await page.getByRole('button', { name: 'Update saved copy', exact: true }).click();
  await page.waitForFunction(() => document.getElementById('save-notice').textContent.includes('Ready for offline use'));
  check((await page.locator('#save-notice').textContent()).includes('1 originals'), 'Selected original is saved locally');
  const cipherCheck = await page.evaluate(async () => {
    const { getMetadata, readRecord } = await import('/offline/database.js');
    const meta = await getMetadata(), record = await readRecord(meta, 'journal');
    const contents = new TextDecoder().decode(await record.cipher.arrayBuffer());
    return { version: meta.version, keys: Object.keys(meta), containsPrivate: /Offline Rio|Rio de Janeiro|Museum offline|Saved private/.test(contents), used: meta.usedBytes };
  });
  check(!cipherCheck.containsPrivate && !cipherCheck.keys.some(key => /password|passphrase|key|logs|title/i.test(key)), 'IndexedDB stores encrypted journal bytes with no persisted key or private text');
  check(cipherCheck.used < 512 * 1048576, 'Encrypted storage remains within its byte budget');
  const paths = await cachePaths();
  check(paths.every(path => !/^\/api\/|^\/account\/|^\/s\//.test(path)) && !paths.includes('/'), 'CacheStorage contains only public files; owner and invitation content stay out');
  check(!paths.some(path => path.includes('openstreetmap')), 'Third-party street tiles are never copied into offline caches');
  await closeSettings(); await page.getByRole('button', { name: 'Offline Rio memory', exact: false }).click();
  await page.locator('#detail img').waitFor();
  const original = await page.evaluate(async () => [...new Uint8Array(await (await fetch(document.querySelector('#detail img').src)).arrayBuffer())]);
  check(Buffer.from(original).equals(png), 'Unlocked original is byte-for-byte identical to the uploaded image');
  check(await page.locator('#detail [aria-label="5 out of 5 stars"]').count() === 1 && !await page.locator('#detail h2 img').count(), 'Experiences retain ratings and unsafe titles render as text');
  await page.getByRole('button', { name: 'Lock', exact: true }).click();
  await page.waitForFunction(() => !document.getElementById('gate').hidden && document.getElementById('logs').children.length === 0);
  check(await page.locator('#detail img').count() === 0 && !await page.locator('#logs .log-card').count(), 'Lock removes private DOM, map pins and decrypted media');
  await page.getByLabel('Offline passphrase', { exact: true }).fill('Wrong-device-passphrase!'); await page.getByRole('button', { name: 'Unlock journal', exact: true }).click();
  await page.waitForFunction(() => document.getElementById('gate-notice').textContent.includes('incorrect'));
  check(await page.locator('#logs .log-card').count() === 0, 'Wrong passphrase cannot reveal saved logs');
  await context.setOffline(true); await page.reload();
  await page.waitForFunction(() => document.getElementById('gate-title').textContent.includes('Unlock your saved'));
  check(await page.locator('#map .country-shape').count() >= 298, 'Reload succeeds without a connection, including boundaries and assets');
  await unlock(); await page.getByRole('button', { name: 'Offline Rio memory', exact: false }).click(); await page.locator('#detail img').waitFor();
  check(await page.locator('#detail').textContent().then(text => text.includes('Museum offline')), 'Saved experiences and original photos open completely offline');
  check(await page.locator('#map .wishlist-pin').count() === 1 && await page.locator('#map .visited-pin').count() === 1, 'Offline map preserves visited and wishlist pins');
  const offlineOriginal = await page.evaluate(async () => [...new Uint8Array(await (await fetch(document.querySelector('#detail img').src)).arrayBuffer())]);
  check(createHash('sha256').update(Buffer.from(offlineOriginal)).digest('hex') === upload.body.sha256, 'Offline original passes the stored SHA-256 integrity check');
  await page.getByRole('button', { name: 'Dark', exact: true }).click(); check(await page.locator('html').getAttribute('data-theme') === 'dark', 'Theme controls work offline');
  await page.setViewportSize({ width: 390, height: 844 });
  await page.waitForFunction(() => !document.querySelector('[data-journal-panel]').open);
  check(await page.locator('.journal-summary').isVisible(), 'Mobile journal remains a compact metrics card offline');
  await page.locator('.journal-summary').click(); check(await page.locator('#logs').isVisible(), 'Mobile card expands into the full offline journal');
  await mkdir('output', { recursive: true }); await page.screenshot({ path: 'output/offline-mobile.png' });
  await page.setViewportSize({ width: 1440, height: 960 });
  await page.goto(base + '/'); await page.waitForFunction(() => document.getElementById('gate-title').textContent.includes('Unlock your saved'));
  check(await page.locator('#logs .log-card').count() === 0, 'Owner URL navigation falls back to a locked offline shell');
  await unlock(); check(await page.locator('#logs .log-card').count() === 2, 'Offline owner URL can unlock the saved snapshot');
  const sharePage = await context.newPage(); let denied = false;
  try { await sharePage.goto(base + share.body.path); } catch { denied = true; }
  check(denied && !await sharePage.locator('#logs .log-card').count(), 'Offline invitation never receives the owner snapshot'); await sharePage.close();
  await context.setOffline(false); await page.goto(base + '/offline/index.html'); await unlock(); await openSaved();
  // Failed sync must preserve the last complete snapshot.
  await context.route('**/api/logs', route => route.fulfill({ status: 503, contentType: 'application/json', body: '{}' }));
  await page.getByRole('button', { name: 'Update saved copy', exact: true }).click();
  await page.waitForFunction(() => document.getElementById('save-notice').textContent.includes('previous saved copy'));
  check(await page.locator('#logs .log-card').count() === 2, 'Failed refresh keeps the previous complete journal');
  await context.unroute('**/api/logs'); await closeSettings();
  // Database quota and epoch changes must be atomic across tabs.
  const databaseChecks = await page.evaluate(async () => {
    const db = await import('/offline/database.js'), meta = await db.getMetadata();
    let quota = false;
    try { await db.putRecord(meta, 'impossible', { iv: new Uint8Array(12), cipher: new ArrayBuffer(100) }, 1); } catch { quota = true; }
    return { quota, unchanged: !(await db.readRecord(meta, 'impossible')) };
  });
  check(databaseChecks.quota && databaseChecks.unchanged, 'IndexedDB byte cap rejects a write atomically');
  const second = await context.newPage(); await second.goto(base + '/offline/index.html');
  await second.getByLabel('Offline passphrase', { exact: true }).fill(passphrase); await second.getByRole('button', { name: 'Unlock journal', exact: true }).click();
  await second.waitForFunction(() => document.getElementById('gate').hidden); await second.getByRole('button', { name: 'Lock', exact: true }).click();
  await page.waitForFunction(() => !document.getElementById('gate').hidden && !document.getElementById('logs').children.length);
  check(true, 'Lock propagates across browser tabs'); await second.close(); await unlock();
  const other = await context.newPage(); await register(randomUUID() + '@example.test', other);
  await page.waitForFunction(() => !document.getElementById('gate').hidden && !document.getElementById('logs').children.length);
  check(await page.evaluate(async () => !(await (await import('/offline/database.js')).getMetadata())), 'Signing in to another account removes the previous account vault');
  await other.close();
  await page.reload(); await page.waitForFunction(() => document.getElementById('gate-title').textContent.includes('Take your memories'));
  await page.getByLabel('Offline passphrase', { exact: true }).fill(passphrase); await page.getByLabel('Confirm offline passphrase', { exact: true }).fill(passphrase);
  await page.getByRole('button', { name: 'Enable offline access' }).click(); await page.waitForFunction(() => document.getElementById('gate').hidden);
  check(await page.locator('#logs .log-card').count() === 0, 'New account never inherits old logs, media or experience details');
  const removed = await page.evaluate(async () => {
    const db = await import('/offline/database.js'), old = await db.getMetadata(); await db.clearDatabase();
    try { await db.putRecord(old, 'late-upload', { iv: new Uint8Array(12), cipher: new ArrayBuffer(100) }, 512 * 1048576); return false; } catch { return true; }
  });
  check(removed, 'An in-flight save cannot recreate data after the vault is removed');
  await page.reload(); await page.waitForFunction(() => document.getElementById('gate-title').textContent.includes('Take your memories'));
  await page.getByLabel('Offline passphrase', { exact: true }).fill(passphrase); await page.getByLabel('Confirm offline passphrase', { exact: true }).fill(passphrase);
  await page.getByRole('button', { name: 'Enable offline access' }).click(); await page.waitForFunction(() => document.getElementById('gate').hidden);
  await api('POST', '/account/logout', {});
  await page.waitForFunction(() => !document.getElementById('gate').hidden);
  check(await page.evaluate(async () => !(await (await import('/offline/database.js')).getMetadata())), 'Sign-out clears persisted private data and locks open tabs');
  check(!errors.length, 'No unexpected browser script errors: ' + errors.join(', '));
  console.log(`${count} offline browser checks passed.`);
} finally { await browser.close(); }
