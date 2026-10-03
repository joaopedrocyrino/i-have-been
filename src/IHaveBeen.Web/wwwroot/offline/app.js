import { initialize } from '/map.js';
import { registerWorker } from './register.js';
import { getMetadata, createVault, readRecord, putRecord, commitJournal, clearDatabase } from './database.js';
import { createCredentials, unlockCredentials, encrypt, decrypt } from './crypto.js';
import { DEFAULT_BUDGET, MAX_JOURNAL, LOCK_AFTER_MS, planMedia, mediaPath, equalHash } from './policy.js';
import { $, clearJournal, showJournal, showDetail, showSelection } from './render.js';
let meta, key, snapshot, session, settingsLogs = [], generation = 0, transfer, locked = true, busy = false;
let lastActivity = Date.now();
const channel = typeof BroadcastChannel === 'function' ? new BroadcastChannel('ihb-offline-lock') : null;
const encode = value => new TextEncoder().encode(JSON.stringify(value));
function notice(id, message = '') { $(id).textContent = message; $(id).hidden = !message; }
function assertCurrent(token) { if (locked || token !== generation) throw new Error('The offline journal was locked. Unlock it again.'); }
function setBusy(value) {
  busy = value;
  for (const id of ['save-button', 'unlock-button', 'settings-button', 'close-settings']) $(id).disabled = value;
}
async function checkSession(signal) {
  let response;
  try { response = await fetch('/api/offline/session', { credentials: 'same-origin', cache: 'no-store', signal: signal || AbortSignal.timeout(10000) }); }
  catch (error) { if (signal?.aborted) throw error; return null; }
  if (response.status === 401) throw new Error('Sign in online before accessing your saved journal on this connection.');
  if (!response.ok) throw new Error('Your account could not be verified. Try again later.');
  const value = await response.json();
  if (!value.ownerId) throw new Error('Your account could not be verified.');
  return value;
}
async function ownedJson(path, expectedOwner, signal) {
  const owner = await checkSession(signal);
  if (!owner || owner.ownerId !== expectedOwner) throw new Error('Sign in with the account that owns this saved journal.');
  const response = await fetch(path, { credentials: 'same-origin', cache: 'no-store', signal });
  if (!response.ok) throw new Error(response.status === 429 ? 'Too many requests. Try again in a minute.' : 'Your journal could not be refreshed. Sign in and try again.');
  return response.json();
}
async function readSnapshot() {
  const record = await readRecord(meta, 'journal');
  return record ? JSON.parse(new TextDecoder().decode(await decrypt(key, meta.ownerId, 'journal', 'journal', record))) : null;
}
async function media(file) {
  if (locked) return null;
  const token = generation, activeKey = key, activeMeta = meta;
  mediaPath(file);
  const record = await readRecord(activeMeta, 'media:' + file.id);
  if (!record) return null;
  const bytes = await decrypt(activeKey, activeMeta.ownerId, 'media', file.id, record); assertCurrent(token);
  if (bytes.byteLength !== file.byteLength || !equalHash(await crypto.subtle.digest('SHA-256', bytes), file.sha256)) throw new Error('Damaged original');
  assertCurrent(token); return new Blob([bytes], { type: file.contentType });
}
function render() {
  $('gate').hidden = true; $('lock-button').hidden = false; $('settings-button').hidden = false;
  if (snapshot) showJournal(snapshot, log => showDetail(log, media));
}
async function gate() {
  meta = await getMetadata();
  $('gate').hidden = false; $('unlock-form').hidden = true; $('forget-button').hidden = !meta;
  $('confirm-label').hidden = !!meta; $('confirm-passphrase').required = !meta;
  try {
    session = await checkSession();
    if (meta && session && session.ownerId !== meta.ownerId) {
      await clearDatabase(); channel?.postMessage('REMOVED'); meta = null;
      $('confirm-label').hidden = false; $('confirm-passphrase').required = true; $('forget-button').hidden = true;
    }
    $('gate-title').textContent = meta ? 'Unlock your saved journal.' : 'Take your memories offline.';
    $('gate-description').textContent = meta ? 'Enter the device passphrase to view your encrypted saved copy.'
      : session ? 'Create a separate device passphrase to save your logs and select original media for offline use.' : 'Sign in with a connection, then return here to save your journal on this device.';
    $('unlock-button').textContent = meta ? 'Unlock journal' : 'Enable offline access';
    $('unlock-form').hidden = !meta && !session; $('sign-in').hidden = !!session || !!meta;
  } catch (error) { $('gate-title').textContent = 'Sign in to your journal.'; notice('gate-notice', error.message); $('sign-in').hidden = false; }
}
function lock(broadcast = true) {
  generation++; transfer?.abort(); transfer = null; key = null; snapshot = null; settingsLogs = []; locked = true;
  clearJournal(); notice('save-notice'); $('settings').hidden = true; $('media-selection').replaceChildren(); $('storage-status').textContent = '';
  $('lock-button').hidden = true; $('settings-button').hidden = true; $('passphrase').value = ''; $('confirm-passphrase').value = '';
  if (broadcast) channel?.postMessage('LOCK');
  setBusy(false); gate().catch(error => notice('gate-notice', error.message));
}
async function forget() {
  if (!window.confirm('Remove the encrypted journal and all saved originals from this device? Your server journal will remain available.')) return;
  generation++; transfer?.abort(); await clearDatabase(); channel?.postMessage('REMOVED');
  navigator.serviceWorker.controller?.postMessage({ type: 'FORGET_OFFLINE' }); lock(false);
}
async function saveMetadata(logs, token) {
  const value = { logs, savedAt: new Date().toISOString(), mediaIds: [], selectedLogIds: logs.map(log => log.id), photos: true, videos: false };
  const bytes = encode(value); if (bytes.byteLength > MAX_JOURNAL) throw new Error('This journal is too large for the offline copy.');
  const encrypted = await encrypt(key, meta.ownerId, 'journal', 'journal', bytes); assertCurrent(token);
  await commitJournal(meta, encrypted, [], DEFAULT_BUDGET); assertCurrent(token); snapshot = value;
}
$('unlock-form').onsubmit = async event => {
  event.preventDefault(); if (busy) return; setBusy(true); notice('gate-notice');
  const token = generation;
  try {
    if (!window.isSecureContext || !crypto.subtle) throw new Error('Offline access requires HTTPS or localhost.');
    session = await checkSession(); meta = await getMetadata();
    if (meta && session && meta.ownerId !== session.ownerId) throw new Error('Remove this device copy before enabling another account.');
    const passphrase = $('passphrase').value;
    if (meta) key = await unlockCredentials(meta, passphrase);
    else {
      if (!session) throw new Error('A connection and sign-in are required to enable offline access.');
      if (passphrase !== $('confirm-passphrase').value) throw new Error('The two offline passphrases do not match.');
      const credentials = await createCredentials(session.ownerId, passphrase);
      if (token !== generation) throw new Error('Please try unlocking again.');
      meta = await createVault(credentials.meta); key = credentials.key;
    }
    if (token !== generation) { key = null; return; }
    locked = false; lastActivity = Date.now(); $('passphrase').value = ''; $('confirm-passphrase').value = '';
    snapshot = await readSnapshot(); assertCurrent(token);
    if (!snapshot) {
      const logs = await ownedJson('/api/logs', meta.ownerId); assertCurrent(token);
      await saveMetadata(logs, token);
    }
    render();
    if (!snapshot.mediaIds.length && session) await openSettings();
  } catch (error) { key = null; locked = true; clearJournal(); notice('gate-notice', error.message); }
  finally { setBusy(false); }
};
async function openSettings() {
  if (locked) return;
  const token = generation; $('settings').hidden = false; notice('save-notice');
  settingsLogs = snapshot.logs;
  try { const fresh = await ownedJson('/api/logs', meta.ownerId); assertCurrent(token); settingsLogs = fresh; }
  catch (error) { if (!locked) notice('save-notice', error.message + ' Your previous copy is still available.'); }
  if (locked) return;
  $('include-photos').checked = snapshot.photos; $('include-videos').checked = snapshot.videos;
  showSelection(settingsLogs, snapshot.selectedLogIds);
  const stored = await getMetadata(); assertCurrent(token);
  const estimate = await navigator.storage?.estimate?.(); assertCurrent(token);
  $('storage-status').textContent = `${(stored.usedBytes / 1048576).toFixed(1)} MiB saved · 512 MiB app limit${estimate?.quota ? ` · browser allowance ${(estimate.quota / 1048576).toFixed(0)} MiB` : ''}`;
}
async function downloadOriginal(file, signal) {
  const response = await fetch(mediaPath(file), { credentials: 'same-origin', cache: 'no-store', signal });
  if (response.status !== 200 || response.headers.get('content-type')?.split(';')[0] !== file.contentType)
    throw new Error(response.status === 429 ? 'Too many requests. Try updating again in a minute.' : 'An original could not be downloaded. Sign in and try again.');
  const reader = response.body.getReader(), chunks = []; let length = 0;
  try {
    for (;;) {
      const { done, value } = await reader.read(); if (done) break;
      length += value.byteLength;
      if (length > file.byteLength) throw new Error('The original length did not match the journal.');
      chunks.push(value);
    }
  } catch (error) { await reader.cancel().catch(() => {}); throw error; }
  if (length !== file.byteLength) throw new Error('The original download was incomplete.');
  const bytes = new Uint8Array(length); let offset = 0;
  for (const chunk of chunks) { bytes.set(chunk, offset); offset += chunk.byteLength; }
  if (!equalHash(await crypto.subtle.digest('SHA-256', bytes), file.sha256)) throw new Error('The original integrity check failed.');
  return bytes;
}
$('save-button').onclick = async () => {
  if (locked || busy) return;
  const token = generation; setBusy(true); transfer = new AbortController();
  const signal = AbortSignal.any([transfer.signal, AbortSignal.timeout(10 * 60 * 1000)]);
  try {
    const logs = await ownedJson('/api/logs', meta.ownerId, signal); assertCurrent(token);
    const selectedLogIds = [...document.querySelectorAll('#media-selection input:checked')].map(input => input.value);
    const photos = $('include-photos').checked, videos = $('include-videos').checked;
    const files = planMedia(logs, selectedLogIds, photos, videos), estimate = await navigator.storage?.estimate?.();
    const missing = [];
    for (const file of files) { assertCurrent(token); if (!await readRecord(meta, 'media:' + file.id)) missing.push(file); }
    const required = missing.reduce((sum, file) => sum + file.byteLength + 28, 0) + encode(logs).byteLength + 65536;
    if (estimate?.quota && estimate.quota - estimate.usage < required) throw new Error('The browser has insufficient free storage. Select fewer originals or free device space.');
    await navigator.storage?.persist?.().catch(() => false); assertCurrent(token);
    let count = 0;
    for (const file of missing) {
      notice('save-notice', `Saving original ${++count}/${missing.length}: ${file.originalName}`);
      const bytes = await downloadOriginal(file, signal); assertCurrent(token);
      const encrypted = await encrypt(key, meta.ownerId, 'media', file.id, bytes); assertCurrent(token);
      await putRecord(meta, 'media:' + file.id, encrypted, DEFAULT_BUDGET); assertCurrent(token);
    }
    const owner = await checkSession(signal); assertCurrent(token);
    if (!owner || owner.ownerId !== meta.ownerId) throw new Error('Your account changed or expired during the download. Sign in and update again.');
    const value = { logs, savedAt: new Date().toISOString(), mediaIds: files.map(file => file.id), selectedLogIds, photos, videos };
    const bytes = encode(value); if (bytes.byteLength > MAX_JOURNAL) throw new Error('The journal exceeded the offline limit.');
    const encrypted = await encrypt(key, meta.ownerId, 'journal', 'journal', bytes); assertCurrent(token);
    await commitJournal(meta, encrypted, files.map(file => 'media:' + file.id), DEFAULT_BUDGET); assertCurrent(token);
    snapshot = value; render(); notice('save-notice', `Saved ${logs.length} memories and ${files.length} originals. Ready for offline use.`);
    const stored = await getMetadata(); assertCurrent(token);
    $('storage-status').textContent = `${(stored.usedBytes / 1048576).toFixed(1)} MiB saved · 512 MiB app limit`;
  } catch (error) { if (!locked) notice('save-notice', error.name === 'QuotaExceededError' ? 'The browser storage limit was reached. Your previous saved copy is preserved.' : error.message + ' Your previous saved copy is preserved.'); }
  finally { if (token === generation) { transfer = null; setBusy(false); } }
};
$('settings-button').onclick = () => openSettings().catch(error => notice('save-notice', error.message));
$('close-settings').onclick = () => { $('settings').hidden = true; };
$('lock-button').onclick = () => lock(); $('forget-button').onclick = forget; $('remove-button').onclick = forget;
for (const [id, checked] of [['select-all', true], ['select-none', false]]) $(id).onclick = () => {
  for (const input of document.querySelectorAll('#media-selection input')) input.checked = checked;
};
function connection() { $('connection-status').textContent = navigator.onLine ? 'Saved journal · changes require online mode' : 'Offline · saved on this device'; }
window.addEventListener('online', () => {
  connection(); if (!locked) checkSession().then(owner => { if (owner && owner.ownerId !== meta.ownerId) { clearDatabase().then(() => lock()); } }).catch(() => lock());
});
window.addEventListener('offline', connection);
for (const event of ['pointerdown', 'keydown']) document.addEventListener(event, () => { lastActivity = Date.now(); }, { passive: true });
const checkIdle = () => { if (!locked && Date.now() - lastActivity >= LOCK_AFTER_MS) lock(); };
setInterval(checkIdle, 30000); document.addEventListener('visibilitychange', checkIdle);
window.addEventListener('pagehide', () => { generation++; transfer?.abort(); key = null; snapshot = null; clearJournal(); });
window.addEventListener('pageshow', event => { if (event.persisted) lock(false); });
channel?.addEventListener('message', () => lock(false));
navigator.serviceWorker?.addEventListener('message', event => { if (['OFFLINE_LOCK', 'OFFLINE_REMOVED'].includes(event.data?.type)) lock(false); });
try {
  const registration = await registerWorker();
  if (!registration) throw new Error('This browser cannot save an offline app. Use HTTPS or localhost in a browser with service-worker support.');
  await Promise.race([navigator.serviceWorker.ready, new Promise((_, reject) => setTimeout(() => reject(new Error('Offline setup did not complete. Reload with a connection.')), 20000))]);
  await initialize('map', null, { offline: true }); connection(); await gate();
} catch (error) { $('gate-title').textContent = 'Offline access is unavailable.'; notice('gate-notice', error.message); }
