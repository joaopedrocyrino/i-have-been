import assert from 'node:assert/strict';
import {createHash, randomUUID} from 'node:crypto';
import {request as httpRequest} from 'node:http';
const base = process.env.BASE_URL || 'http://app:8080';
let checks = 0;
function check(value, message) {assert.ok(value, message); checks++;}
class Session {
  constructor(url=base) {this.base=url;}
  cookies = new Map();
  async call(path, options = {}) {
    const response = await fetch(this.base + path, {...options, redirect: 'manual', headers: {...options.headers, Cookie: [...this.cookies.values()].filter(c => path.startsWith(c.path)).map(c => `${c.name}=${c.value}`).join('; ')}});
    for (const entry of response.headers.getSetCookie()) {
      const [pair, ...attrs] = entry.split(';'); const split = pair.indexOf('='); const name = pair.slice(0, split), value = pair.slice(split + 1);
      const path = attrs.find(x => x.trim().toLowerCase().startsWith('path='))?.trim().slice(5) || '/';
      if (value === '') this.cookies.delete(name); else this.cookies.set(name, {name, value, path});
    }
    return response;
  }
  async mutate(path, method, body, form = false) {
    const csrf = await (await this.call('/api/csrf')).json();
    return this.call(path, {method, headers: {'X-CSRF-TOKEN': csrf.token, ...(!form && body != null ? {'Content-Type': 'application/json'} : {})}, body: body == null ? undefined : form ? body : JSON.stringify(body)});
  }
  async register(name) {
    const form = new URLSearchParams({name, email: `${randomUUID()}@example.test`, password: 'Travel-Test!2026'});
    const response = await this.mutate('/account/register', 'POST', form, true);
    check(response.status === 302 && response.headers.get('location') === '/', 'Account registration creates an authenticated session');
    check(response.headers.getSetCookie().some(x => x.startsWith('ihb.account=') && /httponly/i.test(x) && /samesite=strict/i.test(x)), 'Account cookie is HttpOnly and SameSite Strict');
  }
}
for (let attempt=0; attempt<60; attempt++) { try { if ((await fetch(base+'/health/ready')).status===200) break; } catch {} await new Promise(r=>setTimeout(r,1000)); }
const anonymous = new Session(), alice = new Session(), bob = new Session(), visitor = new Session();
if (process.env.PRODUCTION_URL) {
  const production = process.env.PRODUCTION_URL;
  for(let attempt=0;attempt<60;attempt++){try{await fetch(production+'/health/live');break;}catch{}await new Promise(r=>setTimeout(r,1000));}
  check((await fetch(production+'/health/live')).status===503, 'Production rejects plaintext HTTP');
  check((await fetch(production+'/health/live', {headers:{'X-Forwarded-Proto':'https'}})).status===503, 'Untrusted forwarded HTTPS headers cannot bypass production protection');
}
check((await anonymous.call('/health/ready')).status === 200, 'PostgreSQL and Garage are ready');
check((await anonymous.call('/api/logs')).status === 401, 'Anonymous journal reads are denied');
check((await anonymous.call('/api/media/' + randomUUID())).status === 401, 'Anonymous media reads are denied');
check((await anonymous.call('/api/shares')).status === 401, 'Anonymous share management is denied');
await alice.register('Alice'); await bob.register('Bob');
const input = {title: 'Rio sunset', description: 'A memory with <script>ordinary text</script>', city: 'Rio de Janeiro', country: 'Brazil', latitude: -22.9068, longitude: -43.1729, visitedOn: '2026-05-24', endedOn: '2026-05-27', includeInShares: true};
check((await alice.call('/api/logs', {method:'POST', headers:{'Content-Type':'application/json'}, body:JSON.stringify(input)})).status === 400, 'Mutations without CSRF protection are rejected');
const badCoordinates=await alice.mutate('/api/logs','POST',{...input,latitude:91});check(badCoordinates.status===400,'Invalid coordinates are rejected');
const problem=await badCoordinates.json();check(badCoordinates.headers.get('content-type').includes('application/problem+json') && problem.status===400 && typeof problem.detail==='string','Validation errors use Problem Details without infrastructure data');
check((await alice.mutate('/api/logs','POST',{...input,endedOn:'2026-05-01'})).status === 400, 'Invalid date ranges are rejected');
const created = await alice.mutate('/api/logs','POST',input); check(created.status === 201, 'Detailed log created'); const log = await created.json();
check((await bob.call('/api/logs')).status === 200 && (await (await bob.call('/api/logs')).json()).length === 0, 'Accounts have separate journals');
check((await bob.mutate(`/api/logs/${log.id}`,'PUT',input)).status === 404, 'Another account cannot edit a log');
check((await bob.mutate(`/api/logs/${log.id}`,'DELETE')).status === 404, 'Another account cannot delete a log');
const experienceInput = {title:'Dinner in Rio',description:'Wonderful seafood',category:1,rating:5,visitedOn:'2026-05-25',address:'Centro'};
const experiencePath = `/api/logs/${log.id}/experiences`;
check((await anonymous.mutate(experiencePath,'POST',experienceInput)).status===401,'Anonymous experience writes denied');
check((await alice.call(experiencePath,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(experienceInput)})).status===400,'Experience writes require CSRF');
check((await bob.mutate(experiencePath,'POST',experienceInput)).status===404,'Foreign log cannot receive experiences');
for(const rating of [-1,6]) check((await alice.mutate(experiencePath,'POST',{...experienceInput,rating})).status===400,'Ratings outside 0–5 rejected');
check((await alice.mutate(experiencePath,'POST',{...experienceInput,category:99})).status===400,'Unknown experience category rejected');
const experienceResponse=await alice.mutate(experiencePath,'POST',experienceInput);check(experienceResponse.status===201,'City experience created');const experience=await experienceResponse.json();
check(experience.rating===5 && experience.category==='Restaurant','Rating and category returned');
check((await bob.mutate(`${experiencePath}/${experience.id}`,'PUT',experienceInput)).status===404,'Foreign experience edit denied');
check((await bob.mutate(`${experiencePath}/${experience.id}`,'DELETE')).status===404,'Foreign experience deletion denied');
check((await alice.mutate(`/api/logs/${randomUUID()}/experiences/${experience.id}`,'PUT',experienceInput)).status===404,'Experience cannot be accessed through another parent');
check((await alice.mutate(`${experiencePath}/${experience.id}`,'PUT',{...experienceInput,rating:0})).status===200,'Zero-star rating supported');
const withExperience=await (await alice.call('/api/logs')).json();check(withExperience.find(x=>x.id===log.id).experiences[0].rating===0,'Nested experience returned with its city log');
const wishInput={...input,title:'Visit Japan',country:'Japan',city:'',latitude:36,longitude:138,isWishlist:true,visitedOn:null,endedOn:null,plannedOn:null,includeInShares:false};
const wishResponse=await alice.mutate('/api/logs','POST',wishInput);check(wishResponse.status===201,'Country-only wishlist entry needs no city or visit date');const wish=await wishResponse.json();
check(wish.isWishlist && wish.visitedOn===null && wish.plannedOn===null,'Wishlist status and optional planned date preserved');
check((await alice.mutate(`/api/logs/${wish.id}/experiences`,'POST',experienceInput)).status===400,'Country-only wish needs a city before adding experiences');
check((await alice.mutate(`/api/logs/${wish.id}`,'PUT',{...wishInput,isWishlist:false,visitedOn:'2026-09-01'})).status===400,'Marking country wish visited requires city');
check((await alice.mutate(`/api/logs/${wish.id}`,'PUT',{...wishInput,city:'Kyoto',isWishlist:false,visitedOn:'2026-09-01'})).status===200,'Wishlist can be marked visited');
const converted=(await (await alice.call('/api/logs')).json()).find(x=>x.id===wish.id);check(!converted.isWishlist && converted.id===wish.id && converted.city==='Kyoto','Conversion retains entry identity');
const cityWish=await (await alice.mutate('/api/logs','POST',{...wishInput,title:'Paris someday',city:'Paris',country:'France',plannedOn:'2027-05-01'})).json();check(cityWish.isWishlist && cityWish.plannedOn==='2027-05-01','City wish supports a planned date');
const png = Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIHWP4z8DwHwAFgAI/ScLbtAAAAABJRU5ErkJggg==','base64');
async function upload(session, id, bytes, type, filename) {const form = new FormData();form.append('file',new Blob([bytes],{type}),filename); return session.mutate(`/api/logs/${id}/media`,'POST',form,true);}
check((await upload(bob,log.id,png,'image/png','secret.png')).status === 404, 'Another account cannot attach media');
check((await upload(alice,log.id,Buffer.from('<svg onload="evil()"></svg>'),'image/svg+xml','bad.svg')).status === 400, 'Active SVG files are rejected');
check((await upload(alice,log.id,png,'image/jpeg','fake.jpg')).status === 400, 'MIME spoofing is rejected');
const uploaded = await upload(alice,log.id,png,'image/png','sunset.png'); check(uploaded.status === 201,'Media uploaded to real Garage');const media = await uploaded.json();
check(media.sha256 === createHash('sha256').update(png).digest('hex'), 'Original checksum is stored');
const retrieved = await alice.call(media.url); check(retrieved.status === 200,'Owner media read succeeds');check(Buffer.from(await retrieved.arrayBuffer()).equals(png),'Retrieved original is byte-for-byte identical');
check(retrieved.headers.get('cache-control').includes('no-store') && retrieved.headers.get('x-content-type-options') === 'nosniff','Private media cannot be cached or MIME-sniffed');
check((await bob.call(media.url)).status === 404,'Another account cannot read owner media');check((await bob.mutate(media.url,'DELETE')).status === 404,'Another account cannot delete owner media');
const range = await alice.call(media.url,{headers:{Range:'bytes=8-15'}});check(range.status===206 && Buffer.from(await range.arrayBuffer()).equals(png.subarray(8,16)),'Authorized byte ranges work without changing the original');
check((await alice.call(media.url,{headers:{Range:'bytes=999999-'}})).status===416,'Invalid range rejected');
const privateResponse = await alice.mutate('/api/logs','POST',{...input,title:'Private memory',includeInShares:false});const privateLog = await privateResponse.json();
const privateMedia = await (await upload(alice,privateLog.id,png,'image/png','private.png')).json();
const bobLog = await (await bob.mutate('/api/logs','POST',{...input,title:'Bob only'})).json();const bobMedia = await (await upload(bob,bobLog.id,png,'image/png','bob.png')).json();
async function share(expiration) { const response = await alice.mutate('/api/shares','POST',{label:'Friends',expiresAt:expiration}); check(response.status===200,'Share link created'); return response.json(); }
async function exchange(session, link) {return session.mutate(`/api/shared/${link.id}/session`,'POST',{token:link.path.split('#')[1]});}
const forever = await share(null);
check((await anonymous.call(`/api/shared/${forever.id}/logs`)).status===404,'Share ID alone grants no access');
check((await visitor.mutate(`/api/shared/${forever.id}/session`,'POST',{token:'A'.repeat(43)})).status===404,'Invalid capability rejected');
check((await exchange(visitor,forever)).status===204,'Valid invitation grants read-only session');
const shared = await (await visitor.call(`/api/shared/${forever.id}/logs`)).json();
check(shared.logs.length===1 && shared.logs[0].id===log.id && shared.expiresAt===null,'Only selected owner memories are shared; no-expiration link supported');
check(shared.logs[0].experiences.length===1 && shared.logs[0].experiences[0].rating===0,'Opted-in experience is included in read-only shared journal');
check((await visitor.mutate(experiencePath,'POST',experienceInput)).status===401,'Invitation cannot create experiences');
const sharedUrl = `/api/shared/${forever.id}/media/${media.id}`;
check(Buffer.from(await (await visitor.call(sharedUrl)).arrayBuffer()).equals(png),'Share media preserves original bytes');
check((await visitor.call(`/api/shared/${forever.id}/media/${privateMedia.id}`)).status===404,'Share cannot read private owner media');
check((await visitor.call(`/api/shared/${forever.id}/media/${bobMedia.id}`)).status===404,'Share cannot read a different account media');
check((await visitor.call('/api/logs')).status===401,'Share session is not an owner session');
check((await visitor.mutate(`/api/logs/${log.id}`,'PUT',input)).status===401,'Share cannot edit logs');
check((await bob.mutate(`/api/shares/${forever.id}`,'DELETE')).status===404,'Another account cannot revoke owner invitations');
await alice.mutate(`/api/logs/${log.id}`,'PUT',{...input,includeInShares:false});
check((await visitor.call(sharedUrl)).status===404,'Removing a memory from sharing immediately denies its media');
await alice.mutate(`/api/logs/${log.id}`,'PUT',input);
const tampered = new Session();for(const [name,cookie] of visitor.cookies) tampered.cookies.set(name,{...cookie,value:cookie.value+'tampered'});
check((await tampered.call(sharedUrl)).status===404,'Tampered session cookie rejected');
const expiring = await share(new Date(Date.now()+2500).toISOString());const timedVisitor = new Session();await exchange(timedVisitor,expiring);
check((await timedVisitor.call(`/api/shared/${expiring.id}/logs`)).status===200,'Unexpired link works');
await new Promise(r=>setTimeout(r,2700));
check((await timedVisitor.call(`/api/shared/${expiring.id}/logs`)).status===404,'Previously authenticated share session expires');
check((await timedVisitor.call(`/api/shared/${expiring.id}/media/${media.id}`)).status===404,'Expired session cannot fetch media');
check((await exchange(new Session(),expiring)).status===404,'Expired link cannot mint a new session');
check((await alice.mutate(`/api/shares/${forever.id}`,'DELETE')).status===204,'Owner can revoke invitation');
check((await visitor.call(sharedUrl)).status===404,'Revocation immediately denies an existing media session');
check((await exchange(new Session(),forever)).status===404,'Revoked link cannot mint a session');
if (process.env.GARAGE_URL) {
  for (const path of ['/', '/i-have-been-media', '/i-have-been-media/anything']) { const response = await fetch(process.env.GARAGE_URL+path);check([400,401,403,404].includes(response.status),'Garage denies unsigned access'); }
}
check((await alice.mutate(`${experiencePath}/${experience.id}`,'DELETE')).status===204,'Owner can delete experience');
check((await alice.mutate(`${experiencePath}/${experience.id}`,'PUT',experienceInput)).status===404,'Deleted experience cannot be edited');
check((await alice.mutate(media.url,'DELETE')).status===204,'Owner can delete media');check((await alice.call(media.url)).status===404,'Deleted media immediately inaccessible');
check((await alice.mutate(`/api/logs/${log.id}`,'DELETE')).status===204,'Owner can delete log');
const stale = new Session(); stale.cookies = new Map(alice.cookies);
check((await alice.mutate('/account/logout','POST',new URLSearchParams(),true)).status===302,'Logout succeeds');
check((await stale.call('/api/logs')).status===401,'Logout invalidates a copied account cookie using its security stamp');
if (process.env.UPLOAD_TIMEOUT_URL) {
  const timeoutSession=new Session(process.env.UPLOAD_TIMEOUT_URL);await timeoutSession.register('Timeout fixture');
  const timeoutLog=await (await timeoutSession.mutate('/api/logs','POST',input)).json();
  const token=(await (await timeoutSession.call('/api/csrf')).json()).token;
  const started=Date.now();
  const timed=await new Promise((resolve,reject)=>{
    const connection=httpRequest(new URL(`/api/logs/${timeoutLog.id}/media`,process.env.UPLOAD_TIMEOUT_URL),{method:'POST',headers:{
      'X-CSRF-TOKEN':token,'Content-Type':'multipart/form-data; boundary=deadline-test',Cookie:[...timeoutSession.cookies.values()].map(c=>`${c.name}=${c.value}`).join('; ')
    }},response=>{response.resume();response.on('end',()=>{connection.destroy();resolve(response);});});
    connection.on('error',error=>{
      if(error.code==='ECONNRESET' && Date.now()-started>=700) resolve({aborted:true});else reject(error);
    });connection.setTimeout(10000,()=>connection.destroy(new Error('Upload deadline was not enforced')));
    connection.write('--deadline-test\r\nContent-Disposition: form-data; name="file"; filename="held.png"\r\nContent-Type: image/png\r\n\r\n');
    connection.write(png); // Leave the multipart body incomplete to occupy an upload slot.
  });
  check(timed.aborted || (timed.statusCode===504 && timed.headers['content-type'].includes('application/problem+json')),'Slow uploads are canceled or receive a clear timeout response');
  check(Date.now()-started<5000,'Configured upload deadlines release resources promptly');
  check((await (await timeoutSession.call('/api/logs')).json())[0].media.length===0,'Timed-out multipart uploads never publish media');
}
console.log(`Passed ${checks} integration checks against PostgreSQL, .NET and Garage.`);
