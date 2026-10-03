// Host-side tests: intentionally mutate SQL fixtures and stop ONLY an isolated scanner.
import assert from 'node:assert/strict';
import { randomUUID, createHash } from 'node:crypto';
import { execFileSync, execFile } from 'node:child_process';
import { promisify } from 'node:util';
import { readFile } from 'node:fs/promises';
import { request as httpRequest } from 'node:http';

const project = process.argv[2];
assert.ok(project && /(?:^|-)(?:ci|check|test)(?:-|$)/.test(project), 'Pass an isolated Compose project name (ci/check/test); never run against your real database.');
const environment = Object.fromEntries((await readFile('.env','utf8')).split('\n').filter(line => line.includes('=')).map(line => [line.slice(0,line.indexOf('=')),line.slice(line.indexOf('=')+1)]));
const base = process.env.BASE_URL || `http://localhost:${environment.APP_PORT || 4188}`;
const compose = ['compose','-p',project];
const sqlArgs = [...compose,'exec','-T','postgres','psql','-U',environment.POSTGRES_USER || 'ihb','-d',environment.POSTGRES_DB || 'ihb','-v','ON_ERROR_STOP=1','-v','VERBOSITY=verbose','-tAc'];
const literal = value => `'${String(value).replaceAll("'","''")}'`;
const sql = statement => execFileSync('docker',[...sqlArgs,statement],{encoding:'utf8',stdio:['ignore','pipe','pipe']}).trim();
let checks = 0;
function check(value, message) { assert.ok(value,message); checks++; }
class Session {
  cookies = new Map(); email = `${randomUUID()}@example.test`;
  async call(path, options = {}) {
    const response = await fetch(base+path,{...options,redirect:'manual',headers:{...options.headers,Cookie:[...this.cookies].map(([name,value])=>`${name}=${value}`).join('; ')}});
    for (const cookie of response.headers.getSetCookie()) { const pair=cookie.split(';')[0], split=pair.indexOf('=');this.cookies.set(pair.slice(0,split),pair.slice(split+1)); }
    return response;
  }
  async mutate(path, method, body, form=false) {
    const token=(await (await this.call('/api/csrf')).json()).token;
    return this.call(path,{method,headers:{'X-CSRF-TOKEN':token,...(form?{}:{'Content-Type':'application/json'})},body:form?body:JSON.stringify(body)});
  }
  async register() {
    const response=await this.mutate('/account/register','POST',new URLSearchParams({name:'Security fixture',email:this.email,password:'Travel-Test!2026',userType:'manager'}),true);
    check(response.status===302,'Registration succeeds while ignoring an injected manager type');
    this.id=sql(`SELECT "Id" FROM "AspNetUsers" WHERE "Email"=${literal(this.email)}`);
  }
  async usage() { const response=await this.call('/api/media/usage');assert.equal(response.status,200);return response.json(); }
}
const png=Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIHWP4z8DwHwAFgAI/ScLbtAAAAABJRU5ErkJggg==','base64');
const eicar=Buffer.from('WDVPIVAlQEFQWzRcUFpYNTQoUF4pN0NDKTd9JEVJQ0FSLVNUQU5EQVJELUFOVElWSVJVUy1URVNULUZJTEUhJEgrSCo=','base64');
assert.equal(createHash('md5').update(eicar).digest('hex'),'44d88612fea8a8f36de82e1278abb02f','Antivirus fixture must be the standard harmless EICAR test file');
// A structured embedded archive exercises scanning of content within an allowed media container.
const eicarArchive=Buffer.from('UEsDBBQAAAAIAAAAIQA8z1FoRgAAAEQAAAAJAAAAZWljYXIudHh0izD1VwxQdXAMiDaJCYiKMDXRCIjTNHd21jSvVXH1dHYM0g0OcfRzcQxy0XX0C/EM8wwKDdYNcQ0O0XXz9HFVVPHQ9tACAFBLAQIUAxQAAAAIAAAAIQA8z1FoRgAAAEQAAAAJAAAAAAAAAAAAAACAAQAAAABlaWNhci50eHRQSwUGAAAAAAEAAQA3AAAAbQAAAAAA','base64');
async function upload(session, log, bytes, type='image/png') {
  const form=new FormData();form.append('file',new Blob([bytes],{type}),'fixture.png');
  return session.mutate(`/api/logs/${log}/media`,'POST',form,true);
}
const input={title:'Security fixture',description:'Disposable',city:'Rio de Janeiro',country:'Brazil',latitude:-22.9,longitude:-43.1,visitedOn:'2026-05-24',includeInShares:false};
async function log(session) {const response=await session.mutate('/api/logs','POST',input);assert.equal(response.status,201);return (await response.json()).id;}
const account=new Session(), fresh=new Session();await account.register();await fresh.register();
check((await account.usage()).userType==='user','The database defaults new accounts to user despite injected form fields');
check((await account.usage()).limit===100,'Regular accounts expose the 100-file account-wide quota');
check((await account.usage()).photoMaxBytes===20971520 && (await account.usage()).videoMaxBytes===104857600,'Photo and video limits are exposed to the frontend');
try {sql(`UPDATE "AspNetUsers" SET "UserType"='admin' WHERE "Id"=${literal(account.id)}`);assert.fail('Invalid account type was accepted');}
catch(error) {check(/23514/.test(error.stderr),'SQL rejects unsupported account types');}
const first=await log(account),second=await log(account),freshLog=await log(fresh);
function seed(logId,count) {
  sql(`INSERT INTO "MediaAssets" ("Id","TravelLogId","ObjectKey","OriginalName","ContentType","ByteLength","Sha256","Caption","SortOrder","CreatedAt")
    SELECT gen_random_uuid(),${literal(logId)},'security-fixture/'||gen_random_uuid(),'fixture.png','image/png',${png.length},repeat('a',64),'',n,now() FROM generate_series(1,${count}) n`);
}
seed(first,49);seed(second,50);
check((await account.usage()).count===99,'Media quota counts files across multiple journals');
const insert=logId=>`BEGIN; INSERT INTO "MediaAssets" ("Id","TravelLogId","ObjectKey","OriginalName","ContentType","ByteLength","Sha256","Caption","SortOrder","CreatedAt") VALUES (gen_random_uuid(),${literal(logId)},'race/'||gen_random_uuid(),'race.png','image/png',${png.length},repeat('a',64),'',100,now()); SELECT pg_sleep(0.3); COMMIT;`;
const race=await Promise.allSettled([first,second].map(id=>promisify(execFile)('docker',[...sqlArgs,insert(id)],{encoding:'utf8'})));
check(race.filter(r=>r.status==='fulfilled').length===1 && race.some(r=>r.status==='rejected' && /CK_MediaAssets_UserQuota/.test(r.reason.stderr)),'Concurrent SQL inserts allow exactly the 100th file, rejecting the 101st');
check((await account.usage()).count===100,'Concurrent uploads cannot overflow the account quota');
check((await upload(account,first,png)).status===409,'The API rejects uploads at the quota');
const doomed=sql(`SELECT "Id" FROM "MediaAssets" WHERE "TravelLogId"=${literal(first)} LIMIT 1`);
check((await account.mutate(`/api/media/${doomed}`,'DELETE')).status===204,'Deleting media frees a quota slot');
check((await upload(account,second,png)).status===201 && (await account.usage()).count===100,'A freed slot can be used in another journal');
const oversized=Buffer.concat([png,Buffer.alloc(20971520+1-png.length)]);
check((await upload(fresh,freshLog,oversized)).status===413,'Regular accounts cannot upload photos larger than 20 MiB');
check((await upload(fresh,freshLog,png,'video/mp4')).status===400,'A photo cannot bypass its size/type policy by claiming to be a video');
check((await upload(fresh,freshLog,Buffer.concat([png,eicarArchive]))).status===400,'Real ClamAV rejects an allowed media header containing an embedded harmless EICAR test archive');
check((await fresh.usage()).count===0,'Rejected files never create stored media records');
const originalResponse=await upload(fresh,freshLog,png);check(originalResponse.status===201,'Clean media passes the scanner');
const original=await originalResponse.json();
check(original.sha256===createHash('sha256').update(png).digest('hex') && Buffer.from(await (await fresh.call(original.url)).arrayBuffer()).equals(png),'Scanning preserves original bytes and checksum');
// Direct SQL simulates a manual database operation; application routes cannot do this.
sql(`UPDATE "AspNetUsers" SET "UserType"='manager' WHERE "Id"=${literal(account.id)}`);
check((await account.usage()).userType==='manager' && (await account.usage()).limit===null,'A manual database promotion takes effect without signing in again');
check((await upload(account,first,png)).status===201 && (await account.usage()).count===101,'Managers can store more than 100 files');
check((await upload(account,first,oversized)).status===201,'Managers use the global safety cap rather than the regular photo cap');
check((await upload(account,first,Buffer.concat([png,eicarArchive]))).status===400,'Managers cannot bypass malware scanning');
check((await account.mutate('/account/logout','POST',new URLSearchParams(),true)).status===302,'Identity security-stamp updates succeed');
check(sql(`SELECT "UserType" FROM "AspNetUsers" WHERE "Id"=${literal(account.id)}`)==='manager','Identity saves never reset a manually assigned manager type');
check((await account.mutate('/account/login','POST',new URLSearchParams({email:account.email,password:'Travel-Test!2026'}),true)).status===302,'Manager can sign back in');
sql(`UPDATE "AspNetUsers" SET "UserType"='user' WHERE "Id"=${literal(account.id)}`);
check((await account.usage()).userType==='user' && (await upload(account,first,png)).status===409,'A manual downgrade immediately enforces the quota while retaining existing files');
try {
  execFileSync('docker',[...compose,'stop','clamav'],{stdio:'pipe'});
  check((await upload(fresh,freshLog,png)).status===503,'An offline scanner fails closed rather than storing unverified media');
  check((await fresh.usage()).count===1,'Scanner outages do not publish new media');
} finally {
  execFileSync('docker',[...compose,'start','clamav'],{stdio:'pipe'});
  let ready=false;
  for(let i=0;i<180;i++) {
    try {execFileSync('docker',[...compose,'exec','-T','clamav','clamdcheck.sh'],{stdio:'pipe'});ready=true;break;} catch {}
    await new Promise(resolve=>setTimeout(resolve,1000));
  }
  assert.ok(ready,'The isolated scanner did not recover after its outage test');
}
const csrf=(await (await fresh.call('/api/csrf')).json()).token;
function holdBody(path, contentType, partial) {
  const connection=httpRequest(new URL(path,base),{method:'POST',headers:{'X-CSRF-TOKEN':csrf,'Content-Type':contentType,Cookie:[...fresh.cookies].map(([name,value])=>`${name}=${value}`).join('; ')}},response=>response.resume());
  connection.on('error',()=>{});connection.write(partial);return connection;
}
const waitingUploads=[];
try {
  for(let i=0;i<2;i++) waitingUploads.push(holdBody(`/api/logs/${freshLog}/media`,'multipart/form-data; boundary=ihb-test', '--ihb-test\r\nContent-Disposition: form-data; name="file"; filename="held.png"\r\nContent-Type: image/png\r\n\r\n'));
  await new Promise(resolve=>setTimeout(resolve,300));
  const third=await fresh.call(`/api/logs/${freshLog}/media`,{method:'POST',headers:{'X-CSRF-TOKEN':csrf}});
  check(third.status===429 && third.headers.get('retry-after')==='1','Two pending uploads prevent a third concurrent upload from consuming scanner/storage resources');
} finally {for(const connection of waitingUploads) connection.destroy();}
await new Promise(resolve=>setTimeout(resolve,300));
const waitingRequests=[];
let concurrencyRejected=false;
try {
  // Previous canceled uploads may still be releasing permits. Observe rejected
  // burst requests as well as probes; a rejected burst request frees its permit.
  for(let i=0;i<64;i++) {
    const connection=holdBody('/api/logs','application/json','{"title":"incomplete');
    connection.on('response',response=>{
      if(response.statusCode===429 && response.headers['retry-after']==='1') concurrencyRejected=true;
    });
    waitingRequests.push(connection);
  }
  for(let i=0;i<30;i++) {
    await new Promise(resolve=>setTimeout(resolve,100));
    const response=await fetch(base+'/health/live');await response.arrayBuffer();
    if(response.status===429 && response.headers.get('retry-after')==='1') concurrencyRejected=true;
    if(concurrencyRejected) break;
  }
  check(concurrencyRejected,'The global concurrent HTTP budget rejects bursts before authentication or database access');
} finally {for(const connection of waitingRequests) connection.destroy();}
await new Promise(resolve=>setTimeout(resolve,300));
const uploadStatuses=[];
for(let i=0;i<14;i++) uploadStatuses.push((await fresh.call(`/api/logs/${randomUUID()}/media`,{method:'POST',headers:{'X-CSRF-TOKEN':csrf}})).status);
check(uploadStatuses.includes(429),'Upload attempts are rate limited even when varying journal IDs');
let accountRejected;
for(let i=0;i<340;i++) { const response=await fresh.call('/api/me');await response.arrayBuffer();if(response.status===429){accountRejected=response;break;} }
check(accountRejected?.headers.get('retry-after') && accountRejected.headers.get('content-type').includes('application/problem+json'),'Account throttling returns 429 Problem Details with Retry-After');
check((await fetch(base+'/api/logs')).status===401,'An exhausted account budget does not exhaust the IP budget');
let ipRejected;
for(let i=0;i<620;i++) { const response=await fetch(base+'/api/csrf',{headers:{'X-Forwarded-For':`203.0.113.${i%254+1}`}});await response.arrayBuffer();if(response.status===429){ipRejected=response;break;} }
check(ipRejected?.headers.get('retry-after'),'IP throttling cannot be bypassed by sending arbitrary forwarded addresses');
console.log(`Passed ${checks} account, quota, antivirus and throttling checks on isolated project ${project}.`);
