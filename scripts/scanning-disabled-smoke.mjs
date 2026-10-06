import assert from 'node:assert/strict';
import {execFileSync} from 'node:child_process';
import {createHash, randomUUID} from 'node:crypto';

const project=process.argv[2];
assert.ok(project && /(?:^|-)(?:ci|check|test)(?:-|$)/.test(project),'Use only an isolated ci/check/test Compose project.');
const compose=['compose','-p',project];
const app=execFileSync('docker',[...compose,'ps','-q','app'],{encoding:'utf8'}).trim();
assert.ok(app,'The isolated application must be running.');
assert.equal(execFileSync('docker',['inspect','--format','{{range .Config.Env}}{{if eq . "MalwareScanning__Enabled=false"}}disabled{{end}}{{end}}',app],{encoding:'utf8'}).trim(),'disabled');
const scanner=execFileSync('docker',['ps','-aq','--filter',`label=com.docker.compose.project=${project}`,'--filter','label=com.docker.compose.service=clamav'],{encoding:'utf8'}).trim();
if(scanner) assert.equal(execFileSync('docker',['inspect','--format','{{.State.Running}}',scanner],{encoding:'utf8'}).trim(),'false','ClamAV must be stopped during this test.');
const base=process.env.BASE_URL || 'http://localhost:4188';
let ready=false;
for(let attempt=0;attempt<60;attempt++) {
  try {const response=await fetch(base+'/health/ready');if(response.ok && (await response.json()).status==='ready'){ready=true;break;}} catch {}
  await new Promise(resolve=>setTimeout(resolve,1000));
}
assert.ok(ready,'Disabled-scanning readiness must succeed without a scanner.');
class Session {
  cookies=new Map();
  async call(path,options={}) {
    const response=await fetch(base+path,{...options,redirect:'manual',headers:{...options.headers,Cookie:[...this.cookies].map(([name,value])=>`${name}=${value}`).join('; ')}});
    for(const cookie of response.headers.getSetCookie()) {const pair=cookie.split(';')[0],split=pair.indexOf('=');this.cookies.set(pair.slice(0,split),pair.slice(split+1));}
    return response;
  }
  async mutate(path,method,body,form=false) {
    const token=(await (await this.call('/api/csrf')).json()).token;
    return this.call(path,{method,headers:{'X-CSRF-TOKEN':token,...(form?{}:{'Content-Type':'application/json'})},body:form?body:JSON.stringify(body)});
  }
  async register() {
    assert.equal((await this.mutate('/account/register','POST',new URLSearchParams({name:'Disabled-scanning fixture',email:`${randomUUID()}@example.test`,password:'Travel-Test!2026'}),true)).status,302);
  }
}
const owner=new Session(),other=new Session();await owner.register();await other.register();
const input={title:'Scanner disabled',description:'Disposable fixture',city:'Rio',country:'Brazil',latitude:-22.9,longitude:-43.1,visitedOn:'2026-05-24',includeInShares:false};
const created=await owner.mutate('/api/logs','POST',input);assert.equal(created.status,201);const log=await created.json();
const png=Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIHWP4z8DwHwAFgAI/ScLbtAAAAABJRU5ErkJggg==','base64');
function form(bytes=png,type='image/png') {const body=new FormData();body.append('file',new Blob([bytes],{type}),'fixture.png');return body;}
const path=`/api/logs/${log.id}/media`;
assert.equal((await fetch(base+'/api/logs')).status,401);
assert.equal((await fetch(base+path,{method:'POST',body:form()})).status,401);
assert.equal((await owner.call(path,{method:'POST',body:form()})).status,400,'CSRF still enforced.');
assert.equal((await other.mutate(path,'POST',form(),true)).status,404,'Ownership still enforced.');
assert.equal((await owner.mutate(path,'POST',form(png,'image/jpeg'),true)).status,400,'MIME mismatch still rejected.');
assert.equal((await owner.mutate(path,'POST',form(Buffer.from('<svg/>'),'image/svg+xml'),true)).status,400,'Active formats still rejected.');
assert.equal((await owner.mutate(path,'POST',form(Buffer.concat([png,Buffer.alloc(20971520)])),true)).status,413,'Size limits still enforced.');
const upload=await owner.mutate(path,'POST',form(),true);assert.equal(upload.status,201,'Original upload works with no ClamAV.');
const media=await upload.json();
assert.equal(media.sha256,createHash('sha256').update(png).digest('hex'));
assert.deepEqual(Buffer.from(await (await owner.call(media.url)).arrayBuffer()),png);
assert.equal((await fetch(base+media.url)).status,401,'Originals remain private.');
assert.equal((await other.call(media.url)).status,404,'Other accounts cannot read originals.');
assert.equal((await owner.mutate(`/api/logs/${log.id}`,'DELETE')).status,204);
console.log('Disabled-scanning integration passed: scanner stopped, readiness/uploads work, auth/CSRF/ownership/type/size checks and original bytes preserved.');
