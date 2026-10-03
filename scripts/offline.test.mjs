import test from 'node:test';
import assert from 'node:assert/strict';
import { randomUUID, webcrypto } from 'node:crypto';
import { DEFAULT_BUDGET, MAX_FILE, mediaPath, planMedia, requirePassphrase, localTilePattern } from '../src/IHaveBeen.Web/wwwroot/offline/policy.js';
import { createCredentials, unlockCredentials, encrypt, decrypt } from '../src/IHaveBeen.Web/wwwroot/offline/crypto.js';
if (!globalThis.crypto) globalThis.crypto = webcrypto;
const owner = randomUUID(), bytes = new Uint8Array([0, 1, 2, 3, 255]), passphrase = 'My-device-passphrase!2026';
const credentials = await createCredentials(owner, passphrase);
const file = (overrides = {}) => { const id = randomUUID(); return { id, url: `/api/media/${id}`, contentType: 'image/png', byteLength: 42, sha256: 'ab'.repeat(32), ...overrides }; };
test('Offline key cannot be exported and its ciphertext preserves every original byte', async () => {
  assert.equal(credentials.key.extractable, false);
  const record = await encrypt(credentials.key, owner, 'media', 'photo', bytes);
  assert.notDeepEqual(new Uint8Array(record.cipher), bytes);
  assert.deepEqual(new Uint8Array(await decrypt(credentials.key, owner, 'media', 'photo', record)), bytes);
});
test('Repeated encryption uses fresh nonces', async () => {
  const a = await encrypt(credentials.key, owner, 'media', 'photo', bytes), b = await encrypt(credentials.key, owner, 'media', 'photo', bytes);
  assert.notDeepEqual(a.iv, b.iv); assert.notDeepEqual(a.cipher, b.cipher);
});
test('Tampered ciphertext is rejected', async () => {
  const record = await encrypt(credentials.key, owner, 'media', 'photo', bytes); new Uint8Array(record.cipher)[0] ^= 1;
  await assert.rejects(decrypt(credentials.key, owner, 'media', 'photo', record));
});
test('Ciphertext is bound to account, record ID, and purpose', async () => {
  const record = await encrypt(credentials.key, owner, 'media', 'photo', bytes);
  for (const [account, purpose, id] of [[randomUUID(), 'media', 'photo'], [owner, 'journal', 'photo'], [owner, 'media', 'other']])
    await assert.rejects(decrypt(credentials.key, account, purpose, id, record));
});
test('Correct passphrase unlocks after a restart; wrong passphrase fails', async () => {
  const key = await unlockCredentials(credentials.meta, passphrase);
  const record = await encrypt(credentials.key, owner, 'journal', 'journal', bytes);
  assert.deepEqual(new Uint8Array(await decrypt(key, owner, 'journal', 'journal', record)), bytes);
  await assert.rejects(unlockCredentials(credentials.meta, 'Incorrect-device-passphrase!'));
});
test('Too short and unbounded passphrases are rejected', () => {
  for (const value of ['', 'short', 'x'.repeat(1025), null]) assert.throws(() => requirePassphrase(value));
});
test('Only owner media routes can be stored; invitations and external URLs are rejected', () => {
  const fixture = file(); assert.equal(mediaPath(fixture), fixture.url);
});
test('Untrusted media descriptions and size spoofing cannot create download targets', () => {
  for (const overrides of [{ id: '../secret' }, { url: 'https://evil.test/image' }, { url: '/api/shared/x/media/y' }, { url: '/api/media/a?download=1' },
    { contentType: 'image/svg+xml' }, { byteLength: 0 }, { byteLength: MAX_FILE + 1 }, { byteLength: 1.5 }, { sha256: 'bad' }])
    assert.throws(() => mediaPath(file(overrides)));
});
test('Media selections exclude unchecked memories/videos and deduplicate originals', () => {
  const photo = file(), video = file({ contentType: 'video/mp4' });
  const logs = [{ id: 'a', media: [photo, video] }, { id: 'b', media: [file()] }];
  assert.deepEqual(planMedia(logs, ['a'], true, false), [photo]);
  assert.deepEqual(planMedia(logs, ['a'], false, true), [video]);
  assert.deepEqual(planMedia(logs, [], true, true), []);
  assert.deepEqual(planMedia([...logs, { id: 'c', media: [photo] }], ['a', 'c'], true, false), [photo]);
});
test('Storage planning includes encrypted record overhead and rejects oversized selections', () => {
  const logs = [{ id: 'a', media: [file({ byteLength: MAX_FILE })] }];
  assert.throws(() => planMedia(logs, ['a'], true, false, MAX_FILE));
  assert.equal(planMedia(logs, ['a'], true, false, DEFAULT_BUDGET).length, 1);
});

test('Only explicitly opted-in self-hosted coordinate tiles can enter the public tile cache', () => {
  const pattern = localTilePattern({ url: '/tiles/world/{z}/{x}/{y}.webp', offlineCache: true });
  assert.ok(pattern.test('/tiles/world/3/4/5.webp'));
  assert.ok(!pattern.test('/tiles/world/3/4/5.webp?user=secret'));
  for (const url of ['https://tile.openstreetmap.org/{z}/{x}/{y}.png', '//evil.test/{z}/{x}/{y}.png', '/tiles/../api/{z}/{x}/{y}.png', '/api/media/{z}/{x}/{y}.png', '/tiles/{z}/{x}/{y}.svg'])
    assert.equal(localTilePattern({ url, offlineCache: true }), null);
  assert.equal(localTilePattern({ url: '/tiles/{z}/{x}/{y}.png', offlineCache: false }), null);
});
