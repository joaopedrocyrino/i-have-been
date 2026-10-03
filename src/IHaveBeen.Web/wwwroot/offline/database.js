export const DATABASE_NAME = 'i-have-been-offline-v1';
let connection;
function open() {
  if (!connection) connection = new Promise((resolve, reject) => {
    const request = indexedDB.open(DATABASE_NAME, 1);
    request.onupgradeneeded = () => {
      request.result.createObjectStore('metadata');
      request.result.createObjectStore('records', { keyPath: 'id' });
    };
    request.onerror = () => { connection = null; reject(new Error('Offline storage is unavailable in this browser.')); };
    request.onsuccess = () => {
      request.result.onversionchange = () => { request.result.close(); connection = null; };
      resolve(request.result);
    };
  });
  return connection;
}
async function transaction(stores, mode, run) {
  const db = await open();
  return new Promise((resolve, reject) => {
    const tx = db.transaction(stores, mode); let result, failure;
    const fail = error => { failure = error; tx.abort(); };
    tx.oncomplete = () => resolve(result);
    tx.onerror = tx.onabort = () => reject(failure || tx.error || new Error('The offline save did not finish.'));
    try { run(tx, value => { result = value; }, fail); } catch (error) { fail(error); }
  });
}
export function getMetadata() {
  return transaction(['metadata'], 'readonly', (tx, done) => {
    tx.objectStore('metadata').get('vault').onsuccess = event => done(event.target.result || null);
  });
}
function check(current, expected) {
  if (!current || current.epoch !== expected.epoch || current.ownerId !== expected.ownerId)
    throw new Error('The offline journal was locked or removed. Unlock it again.');
}
export function createVault(meta) {
  return transaction(['metadata'], 'readwrite', (tx, done, fail) => {
    const store = tx.objectStore('metadata');
    store.get('vault').onsuccess = event => {
      if (event.target.result) { fail(new Error('A saved journal already exists. Unlock or remove it first.')); return; }
      store.put(meta, 'vault'); done(meta);
    };
  });
}
export function readRecord(meta, id) {
  return transaction(['metadata', 'records'], 'readonly', (tx, done, fail) => {
    tx.objectStore('metadata').get('vault').onsuccess = event => {
      try { check(event.target.result, meta); } catch (error) { fail(error); return; }
      tx.objectStore('records').get(id).onsuccess = event => done(event.target.result || null);
    };
  });
}
export function putRecord(meta, id, encrypted, budget) {
  const record = { id, iv: encrypted.iv, cipher: new Blob([encrypted.cipher]), byteLength: encrypted.cipher.byteLength + encrypted.iv.byteLength };
  return transaction(['metadata', 'records'], 'readwrite', (tx, done, fail) => {
    const metadata = tx.objectStore('metadata'), records = tx.objectStore('records');
    metadata.get('vault').onsuccess = event => {
      const current = event.target.result;
      try { check(current, meta); } catch (error) { fail(error); return; }
      records.get(id).onsuccess = event => {
        const used = current.usedBytes - (event.target.result?.byteLength || 0) + record.byteLength;
        if (used > budget) { fail(new Error('The offline storage limit was reached. Remove the saved copy or choose fewer files.')); return; }
        current.usedBytes = used; records.put(record); metadata.put(current, 'vault'); done(record);
      };
    };
  });
}
export function commitJournal(meta, encrypted, keepIds, budget) {
  const record = { id: 'journal', iv: encrypted.iv, cipher: new Blob([encrypted.cipher]), byteLength: encrypted.cipher.byteLength + encrypted.iv.byteLength };
  const keep = new Set(keepIds);
  return transaction(['metadata', 'records'], 'readwrite', (tx, done, fail) => {
    const metadata = tx.objectStore('metadata'), records = tx.objectStore('records');
    metadata.get('vault').onsuccess = event => {
      const current = event.target.result;
      try { check(current, meta); } catch (error) { fail(error); return; }
      records.getAll().onsuccess = event => {
        let used = record.byteLength;
        for (const item of event.target.result) {
          if (item.id === 'journal') continue;
          if (keep.has(item.id)) used += item.byteLength;
          else records.delete(item.id);
        }
        if (used > budget) { fail(new Error('The offline storage limit was reached.')); return; }
        current.usedBytes = used; records.put(record); metadata.put(current, 'vault'); done();
      };
    };
  });
}
export function clearDatabase() {
  return transaction(['metadata', 'records'], 'readwrite', tx => {
    tx.objectStore('metadata').clear(); tx.objectStore('records').clear();
  });
}
