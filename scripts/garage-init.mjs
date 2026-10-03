// Run by the tools container; admin credentials never enter the API container.
import { timingSafeEqual } from 'node:crypto';

const required = name => {
  const value = process.env[name];
  if (!value || value.startsWith('replace-')) throw new Error(`${name} must be configured.`);
  return value;
};

async function main() {
  const endpoint = new URL(required('GARAGE_ADMIN_URL'));
  const token = required('GARAGE_ADMIN_TOKEN');
  const bucketName = required('S3_BUCKET');
  const accessKeyId = required('S3_ACCESS_KEY_ID');
  const secretAccessKey = required('S3_SECRET_ACCESS_KEY');
  if (!/^GK[0-9a-f]{24}$/i.test(accessKeyId) || !/^[0-9a-f]{64}$/i.test(secretAccessKey)) {
    throw new Error('Garage requires a GK + 24 hex access key and a 64 hex secret key. See docs/storage.md.');
  }

  const request = async (method, path, body) => {
    const response = await fetch(new URL(path, endpoint), {
      method,
      headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' },
      ...(body === undefined ? {} : { body: JSON.stringify(body) }),
      signal: AbortSignal.timeout(15000),
    });
    if (!response.ok) {
      // The admin response can contain credentials. Report status only.
      throw new Error(`Garage ${method} ${path.split('?')[0]} failed (${response.status}).`);
    }
    return response.json();
  };

  const keys = await request('GET', '/v2/ListKeys');
  if (!keys.some(key => key.id === accessKeyId)) {
    await request('POST', '/v2/ImportKey', { accessKeyId, secretAccessKey, name: 'I Have Been media storage' });
  } else {
    const existing = await request('GET', `/v2/GetKeyInfo?id=${encodeURIComponent(accessKeyId)}&showSecretKey=true`);
    const expected = Buffer.from(secretAccessKey);
    const actual = Buffer.from(existing.secretAccessKey ?? '');
    if (actual.length !== expected.length || !timingSafeEqual(actual, expected)) {
      throw new Error('Existing Garage key has a different secret. Restore the matching secret or rotate to a new key ID.');
    }
  }
  await request('POST', `/v2/UpdateKey?id=${encodeURIComponent(accessKeyId)}`, { deny: { createBucket: true } });

  const buckets = await request('GET', '/v2/ListBuckets');
  let bucket = buckets.find(candidate => candidate.globalAliases.includes(bucketName));
  if (!bucket) bucket = await request('POST', '/v2/CreateBucket', { globalAlias: bucketName });
  const bucketId = bucket.id;
  await request('POST', `/v2/UpdateBucket?id=${encodeURIComponent(bucketId)}`, { websiteAccess: { enabled: false } });
  await request('POST', '/v2/DenyBucketKey', { bucketId, accessKeyId, permissions: { read: false, write: false, owner: true } });
  await request('POST', '/v2/AllowBucketKey', { bucketId, accessKeyId, permissions: { read: true, write: true, owner: false } });
  console.log('Private I Have Been bucket ready. Application key has read/write access without ownership or bucket-creation permissions.');
}

main().catch(error => {
  console.error(error.message);
  process.exitCode = 1;
});
