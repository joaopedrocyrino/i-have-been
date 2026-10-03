import { KDF_ITERATIONS, requirePassphrase } from './policy.js';
const encode = value => new TextEncoder().encode(value);
function context(owner, kind, id) { return encode(JSON.stringify(['ihb-offline', 1, owner, kind, id])); }
export async function deriveKey(passphrase, salt) {
  requirePassphrase(passphrase);
  const material = await crypto.subtle.importKey('raw', encode(passphrase), 'PBKDF2', false, ['deriveKey']);
  return crypto.subtle.deriveKey({ name: 'PBKDF2', salt, iterations: KDF_ITERATIONS, hash: 'SHA-256' },
    material, { name: 'AES-GCM', length: 256 }, false, ['encrypt', 'decrypt']);
}
export async function encrypt(key, owner, kind, id, bytes) {
  const iv = crypto.getRandomValues(new Uint8Array(12));
  const cipher = await crypto.subtle.encrypt({ name: 'AES-GCM', iv, additionalData: context(owner, kind, id) }, key, bytes);
  return { iv, cipher };
}
export async function decrypt(key, owner, kind, id, record) {
  const cipher = record.cipher instanceof Blob ? await record.cipher.arrayBuffer() : record.cipher;
  return crypto.subtle.decrypt({ name: 'AES-GCM', iv: record.iv, additionalData: context(owner, kind, id) }, key, cipher);
}
export async function createCredentials(owner, passphrase) {
  const salt = crypto.getRandomValues(new Uint8Array(32)), key = await deriveKey(passphrase, salt);
  const verifier = await encrypt(key, owner, 'verifier', 'vault', encode('i-have-been:offline:v1'));
  return { key, meta: { version: 1, ownerId: owner, epoch: crypto.randomUUID(), salt, verifier, usedBytes: 0 } };
}
export async function unlockCredentials(meta, passphrase) {
  if (meta.version !== 1) throw new Error('This saved journal needs a newer version of the app.');
  const key = await deriveKey(passphrase, meta.salt);
  try {
    const bytes = await decrypt(key, meta.ownerId, 'verifier', 'vault', meta.verifier);
    if (new TextDecoder().decode(bytes) !== 'i-have-been:offline:v1') throw new Error();
  } catch { throw new Error('The offline passphrase is incorrect or the saved data is damaged.'); }
  return key;
}
