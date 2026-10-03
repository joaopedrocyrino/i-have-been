import { test } from 'node:test';
import assert from 'node:assert/strict';
import { validateFiles } from '../src/IHaveBeen.Web/wwwroot/media-preflight.js';
const png=Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIHWP4z8DwHwAFgAI/ScLbtAAAAABJRU5ErkJggg==','base64');
const usage={limit:100,count:0,photoMaxBytes:1024,videoMaxBytes:2048};
test('valid originals are inspected without modifying their bytes',async()=>{
  const file=new File([png],'photo.png',{type:'image/png'});await validateFiles([file],usage);assert.deepEqual(Buffer.from(await file.arrayBuffer()),png);
});
test('an entire selection must fit available account slots',async()=>{
  const file=new File([png],'photo.png',{type:'image/png'});await assert.rejects(validateFiles([file,file],{...usage,count:99}),/1 slots available/);
});
test('a manager has no file-count ceiling',async()=>{
  await validateFiles([new File([png],'photo.png',{type:'image/png'})],{...usage,count:200,limit:null});
});
test('a photo cannot pass the configured photo byte ceiling',async()=>{
  await assert.rejects(validateFiles([new File([png],'photo.png',{type:'image/png'})],{...usage,photoMaxBytes:32}),/must be between/);
});
test('claimed types must match the actual media header',async()=>{
  await assert.rejects(validateFiles([new File([png],'photo.mp4',{type:'video/mp4'})],usage),/matching file type/);
});
test('active content and empty files are rejected before uploading',async()=>{
  await assert.rejects(validateFiles([new File(['<svg onload="alert(1)">'],'photo.svg',{type:'image/svg+xml'})],usage),/matching file type/);
  await assert.rejects(validateFiles([new File([],'empty.png',{type:'image/png'})],usage),/must be between/);
});
