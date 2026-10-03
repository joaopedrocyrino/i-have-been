// Early local validation saves bandwidth; every rule is enforced again by the API.
export async function validateFiles(files, usage) {
  if (usage.limit !== null && usage.count + files.length > usage.limit)
    throw new Error(`Your account can store ${usage.limit} photos/videos. You have ${Math.max(0, usage.limit - usage.count)} slots available.`);
  for (const file of files) {
    const limit = file.type.startsWith('image/') ? usage.photoMaxBytes : usage.videoMaxBytes;
    if (!file.size || file.size > limit)
      throw new Error(`${file.name} must be between 1 byte and ${limit / 1048576} MiB.`);
    const bytes = new Uint8Array(await file.slice(0, 64).arrayBuffer());
    if (detectType(bytes) !== file.type)
      throw new Error(`${file.name} must be a JPEG, PNG, WebP, MP4, MOV or WebM with a matching file type.`);
  }
}

function detectType(b) {
  const ascii = (start, length) => String.fromCharCode(...b.slice(start, start + length));
  if (b.length >= 3 && b[0] === 255 && b[1] === 216 && b[2] === 255) return 'image/jpeg';
  if (b.length >= 8 && [137,80,78,71,13,10,26,10].every((v,i) => b[i] === v)) return 'image/png';
  if (b.length >= 12 && ascii(0,4) === 'RIFF' && ascii(8,4) === 'WEBP') return 'image/webp';
  if (b.length >= 12 && ascii(4,4) === 'ftyp') return ascii(8,4) === 'qt  ' ? 'video/quicktime' : 'video/mp4';
  if (b.length >= 4 && [0x1a,0x45,0xdf,0xa3].every((v,i) => b[i] === v)) return 'video/webm';
  return null;
}
