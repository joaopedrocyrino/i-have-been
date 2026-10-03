import assert from 'node:assert/strict';
import {randomUUID} from 'node:crypto';
import {mkdir, readFile, writeFile} from 'node:fs/promises';
const base = process.env.BASE_URL || 'http://app:8080';
const path = 'output/upgrade-fixture.json';
let cookies = new Map();
async function call(path, options = {}) {
  const response = await fetch(base+path, {...options,redirect:'manual',headers:{...options.headers,Cookie:[...cookies].map(([name,value])=>`${name}=${value}`).join('; ')}});
  for(const entry of response.headers.getSetCookie()) {const pair=entry.split(';')[0], split=pair.indexOf('=');cookies.set(pair.slice(0,split),pair.slice(split+1));}
  return response;
}
async function mutate(path,body,form=false) {
  const token=(await (await call('/api/csrf')).json()).token;
  const response=await call(path,{method:'POST',headers:{'X-CSRF-TOKEN':token,...(form?{}:{'Content-Type':'application/json'})},body:form?body:JSON.stringify(body)});
  assert.ok(response.status<400,`Upgrade fixture request failed: ${path} ${response.status}`);return response;
}
if(process.argv[2]==='seed') {
  await mutate('/account/register',new URLSearchParams({name:'Upgrade traveler',email:randomUUID()+'@example.test',password:'Travel-Test!2026'}),true);
  const log=await (await mutate('/api/logs',{title:'Existing city memory',description:'Must survive upgrade',city:'Rio de Janeiro',country:'Brazil',latitude:-22.9,longitude:-43.1,visitedOn:'2026-05-24',includeInShares:true})).json();
  const bytes=Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIHWP4z8DwHwAFgAI/ScLbtAAAAABJRU5ErkJggg==','base64');
  const form=new FormData();form.append('file',new Blob([bytes],{type:'image/png'}),'existing.png');
  const media=await (await mutate(`/api/logs/${log.id}/media`,form,true)).json();
  const share=await (await mutate('/api/shares',{label:'Existing invitation',expiresAt:null})).json();
  await mkdir('output',{recursive:true});await writeFile(path,JSON.stringify({cookies:[...cookies],log,media,share,bytes:bytes.toString('base64')}),{mode:0o600});
  console.log('Seeded previous application schema with an account, memory, original and invitation.');
} else if(process.argv[2]==='verify') {
  const fixture=JSON.parse(await readFile(path,'utf8'));cookies=new Map(fixture.cookies);
  const logs=await (await call('/api/logs')).json();const log=logs.find(x=>x.id===fixture.log.id);
  assert.equal(log.title,fixture.log.title);assert.equal(log.visitedOn,fixture.log.visitedOn);assert.equal(log.isWishlist,false);assert.deepEqual(log.experiences,[]);
  assert.ok(Buffer.from(await (await call(fixture.media.url)).arrayBuffer()).equals(Buffer.from(fixture.bytes,'base64')));
  cookies=new Map();const token=fixture.share.path.split('#')[1];await mutate(`/api/shared/${fixture.share.id}/session`,{token});
  const shared=await (await call(`/api/shared/${fixture.share.id}/logs`)).json();assert.equal(shared.logs[0].id,fixture.log.id);
  assert.ok(Buffer.from(await (await call(shared.logs[0].media[0].url)).arrayBuffer()).equals(Buffer.from(fixture.bytes,'base64')));
  console.log('Passed upgrade checks: existing session, memory, visited status, byte-exact owner/shared media and invitation.');
} else throw new Error('Use seed against the previous image, then verify against the new image on an isolated stack.');
