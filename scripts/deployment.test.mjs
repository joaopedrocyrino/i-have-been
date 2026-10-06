import test from 'node:test';
import assert from 'node:assert/strict';
import {mkdtemp, mkdir, writeFile, readFile, readdir, rm, symlink, realpath, chmod, copyFile, stat} from 'node:fs/promises';
import {tmpdir} from 'node:os';
import {join, resolve} from 'node:path';
import {spawnSync} from 'node:child_process';

const repo = resolve(import.meta.dirname, '..');
const image = `ghcr.io/owner/i-have-been@sha256:${'b'.repeat(64)}`;
const previousImage = `ghcr.io/owner/i-have-been@sha256:${'a'.repeat(64)}`;
const sha = 'c'.repeat(40), incoming = `${sha}-1-1`;
const releaseFiles = ['docker-compose.prod.yml', 'infra/garage/garage.toml', 'infra/clamav/clamd.conf', 'infra/otel/collector.yaml', 'scripts/garage-init.mjs', 'scripts/production-compose.py', 'scripts/validate-production-env.py'];
const mock = `#!/usr/bin/env node
const fs=require('node:fs'), path=require('node:path'), cp=require('node:child_process');
const tool=path.basename(process.argv[1]), args=process.argv.slice(2), mode=process.env.TEST_FAILURE;
fs.appendFileSync(process.env.TEST_LOG,JSON.stringify({tool,args,image:process.env.APP_IMAGE,proxy:process.env.TRUSTED_PROXY_IP,tokenPresent:!!process.env.GHCR_TOKEN,scanning:process.env.MALWARE_SCANNING_ENABLED})+'\\n');
if(tool==='sleep'||tool==='flock') process.exit(0);
if(tool==='awk') {
 if(args.at(-1)!=='/proc/meminfo') process.exit(1);
 console.log({'low-memory':'984064','low-memory-with-scanner':'984064','two-gib':'2097152','invalid-memory':'unavailable','four-gib-plan':'3928064'}[mode]??'8388608');
 process.exit(0);
}
if(tool==='docker') {
 if(args[0]==='ps') {if(mode==='low-memory-with-scanner') console.log('fixture-old-scanner');process.exit(0);}
 if(args[0]==='network') process.exit(mode==='network'?1:0);
 if(args[0]==='login') {if(!fs.readFileSync(0).length) process.exit(1);process.exit(0);}
 if(args[0]==='inspect') {
  if(args[2].includes('NetworkSettings')) {console.log(mode==='proxy'?'not-an-ip':'172.20.0.2');process.exit(0);}
  let state='running|healthy|false|0|0';
  if(args.at(-1)==='fixture-clamav') {
   const states={'scanner-oom':'restarting|starting|true|137|1','scanner-unhealthy':'running|unhealthy|false|0|0','scanner-exited':'exited|starting|false|1|0','scanner-missing-health':'running|missing|false|0|0'};
   if(states[mode]) state=states[mode];
   if(mode==='scanner-starting') {
    const count=Number(fs.existsSync(process.env.TEST_HEALTH_COUNT)?fs.readFileSync(process.env.TEST_HEALTH_COUNT,'utf8'):0);
    fs.writeFileSync(process.env.TEST_HEALTH_COUNT,String(count+1));
    if(count<2) state='running|starting|false|0|0';
   }
  }
  console.log(state);process.exit(0);
 }
 const command=args.slice(args.indexOf('-f')+2);
 if(command[0]==='config' && command.includes('--services')) {
  const config=fs.readFileSync(args[args.indexOf('--env-file')+1],'utf8');
  console.log('app\\npostgres\\ngarage'+(process.env.MALWARE_SCANNING_ENABLED==='false'?'':'\\nclamav')+(config.includes('COMPOSE_PROFILES=observability')?'\\notel-collector':''));
 }

 if(command[0]==='ps' && !(mode==='scanner-missing' && command.at(-1)==='clamav')) console.log('fixture-'+command.at(-1));
 if(command[0]==='exec') {if(mode==='backup') process.exit(1);console.log('PGDMP-test-backup');}
 if(command.includes('run') && command.at(-1)==='migrate' && mode==='migration') process.exit(1);
 if(command.includes('run') && command.at(-1)==='migrate' && mode==='hangup') process.kill(process.ppid,'SIGHUP');
 process.exit(0);
}
if(tool==='curl') {if(mode==='health') process.exit(22);console.log(JSON.stringify({status:mode==='wrong-health'?'up':'ready'}));process.exit(0);}
if(tool==='ssh') {const input=fs.readFileSync(0);fs.writeFileSync(process.env.TEST_SSH_INPUT,input);if(mode==='ssh-disconnect'&&input.length) process.exit(255);const r=cp.spawnSync('bash',['-c',args.at(-1)],{input,env:process.env});process.stdout.write(r.stdout||'');process.stderr.write(r.stderr||'');process.exit(r.status??1);}
if(tool==='scp') {const listing=cp.spawnSync('tar',['-tzf',args.at(-2)],{encoding:'utf8'});fs.writeFileSync(process.env.TEST_UPLOADS,JSON.stringify({status:listing.status,files:listing.stdout.trim().split('\\n')}));fs.copyFileSync(args.at(-2),args.at(-1).split(':').slice(1).join(':'));process.exit(0);}
`;

async function fixture(t, {previous = true, failure = ''} = {}) {
  const root = await mkdtemp(join(tmpdir(), 'ihb-deploy-test-'));
  t.after(() => rm(root, {recursive: true, force: true}));
  const base = join(root, 'i-have-been'), bin = join(root, 'bin'), log = join(root, 'commands.jsonl');
  await mkdir(bin); await mkdir(join(base, 'incoming', incoming), {recursive: true});
  const dotenv = 'POSTGRES_PASSWORD=fixture-db-secret\nGARAGE_RPC_SECRET=fixture-rpc-secret\nGARAGE_ADMIN_TOKEN=fixture-admin-secret\nS3_ACCESS_KEY_ID=fixture-access-key\nS3_SECRET_ACCESS_KEY=fixture-storage-secret\n';
  await writeFile(join(base, '.env'), dotenv, {mode: 0o600});
  for (const tool of ['docker', 'curl', 'sleep', 'flock', 'ssh', 'scp', 'awk']) await writeFile(join(bin, tool), mock, {mode: 0o700});
  if (previous) {
    const old = join(base, 'releases', 'previous');
    await mkdir(old, {recursive: true});
    await writeFile(join(old, 'docker-compose.prod.yml'), 'services: {}\n');
    await writeFile(join(old, 'release.json'), JSON.stringify({image: previousImage, hostname: 'i-have-been.cyrino.dev', trustedProxy: '172.20.0.3'}));
    await symlink(old, join(base, 'current'));
  }
  const env = {...process.env, PATH: `${bin}:${process.env.PATH}`, TEST_LOG: log, TEST_FAILURE: failure, TEST_HEALTH_COUNT: join(root,'health-count'), TEST_SSH_INPUT: join(root,'ssh-input'), TEST_UPLOADS: join(root,'uploads.json'), GHCR_TOKEN: 'fixture-registry-secret'};
  const args = [base, incoming, image, 'i-have-been.cyrino.dev', 'csl-caddy-1', 'owner'];
  function bundle() {
    const result = spawnSync('tar', ['-czf', join(base, 'incoming', incoming, 'release.tar.gz'), ...releaseFiles], {cwd: repo});
    assert.equal(result.status, 0, result.stderr?.toString());
  }
  async function commands() {try {return (await readFile(log, 'utf8')).trim().split('\n').filter(Boolean).map(line => JSON.parse(line));} catch {return [];}}
  function run(overrides = {}) {bundle(); return spawnSync('bash', [join(repo, 'scripts/deploy-remote.sh'), ...args], {env: {...env, ...overrides}, encoding: 'utf8', timeout: 30000});}
  return {root, base, env, args, dotenv, originalStat: await stat(join(base,'.env')), commands, run};
}
const composeCalls = rows => rows.filter(row => row.tool === 'docker' && row.args[0] === 'compose');
const operation = row => row.args.slice(row.args.indexOf('-f') + 2);
const startsApp = rows => composeCalls(rows).filter(row => {const args=operation(row);return args[0]==='up' && args.at(-1)==='app';});

test('successful rollout provisions, backs up and migrates before replacing the app; preserves secrets and other projects', async t => {
  const f = await fixture(t), result = f.run();
  assert.equal(result.status, 0, result.stderr);
  const rows = await f.commands(), calls = composeCalls(rows);
  assert.ok(calls.every(row => row.args.includes('i-have-been')));
  assert.ok(!rows.some(row => row.args.includes('down') || row.args.includes('prune') || row.args.includes('--volumes')));
  assert.ok(calls.findIndex(row => operation(row)[0] === 'exec') < calls.findIndex(row => operation(row).at(-1) === 'migrate'));
  assert.ok(calls.findIndex(row => operation(row).at(-1) === 'garage-init') < calls.findIndex(row => operation(row).at(-1) === 'migrate'));
  assert.ok(calls.findIndex(row => operation(row).at(-1) === 'migrate') < calls.findIndex(row => operation(row)[0] === 'up' && operation(row).at(-1) === 'app'));
  assert.equal(startsApp(rows).length, 1);
  assert.ok(startsApp(rows)[0].args.includes('--no-deps'));
  assert.ok(startsApp(rows)[0].args.includes('--no-build'));
  assert.equal(startsApp(rows)[0].proxy, '172.20.0.2');
  assert.equal(await readFile(join(f.base, '.env'), 'utf8'), f.dotenv);
  const after=await stat(join(f.base,'.env'));
  assert.deepEqual([after.ino,after.mtimeMs,after.mode],[f.originalStat.ino,f.originalStat.mtimeMs,f.originalStat.mode], 'Deployment must not replace or modify the droplet env file');
  const current = await realpath(join(f.base, 'current'));
  assert.equal(JSON.parse(await readFile(join(current, 'release.json'), 'utf8')).image, image);
  assert.equal((await readdir(join(f.base, 'backups'))).filter(name => name.endsWith('.dump')).length, 1);
  assert.ok(!(await readdir(f.base)).some(name => name.startsWith('.registry-')));
  const login = rows.findIndex(row => row.tool === 'docker' && row.args[0] === 'login');
  assert.ok(rows.slice(login + 1).every(row => !row.tokenPresent), 'Registry token must leave the process environment after login');
  const helper=spawnSync('python3',[join(current,'scripts/production-compose.py'),'ps'],{env:f.env,encoding:'utf8'});
  assert.equal(helper.status,0,helper.stderr);
  const call=(await f.commands()).at(-1);
  assert.equal(call.args[call.args.indexOf('--env-file')+1],join(f.base,'.env'));
  assert.equal(call.image,image);
  assert.equal(call.scanning,'true');
});

for(const [failure,message] of [
  ['low-memory',/Insufficient physical host RAM: 961 MiB/],
  ['two-gib',/Insufficient physical host RAM: 2048 MiB/],
  ['invalid-memory',/Cannot determine physical host RAM/]
]) test(`${failure} rejects deployment before login, pulls or service changes`, async t => {
  const f=await fixture(t,{failure}),result=f.run();
  assert.notEqual(result.status,0);
  assert.match(result.stderr,message);
  assert.match(result.stderr,/failed during resource-preflight/);
  const rows=await f.commands();
  assert.ok(!rows.some(row=>row.tool==='docker'&&row.args[0]==='login'));
  assert.ok(!composeCalls(rows).some(row=>['pull','up','exec','run','stop'].includes(operation(row)[0])));
  assert.ok((await realpath(join(f.base,'current'))).endsWith('/previous'));
  assert.equal(await readFile(join(f.base,'.env'),'utf8'),f.dotenv);
  assert.ok(!(await readdir(f.base)).some(name=>name.startsWith('.registry-')));
});

test('a 4 GiB plan passes the floor despite kernel-reserved RAM; swap is not counted',async t=>{
  const f=await fixture(t,{failure:'four-gib-plan'}),result=f.run();
  assert.equal(result.status,0,result.stderr);
  assert.match(result.stdout,/Physical host RAM: 3836 MiB/);
  const query=(await f.commands()).find(row=>row.tool==='awk');
  assert.ok(query.args[0].includes('MemTotal:'));
  assert.ok(!query.args[0].includes('SwapTotal:'));
});

test('disabled scanning deploys on the reported 961 MiB host, stops only its old scanner and preserves runtime secrets', async t=>{
  const f=await fixture(t,{failure:'low-memory-with-scanner'});
  f.args.push('false');
  const result=f.run(); assert.equal(result.status,0,result.stderr);
  const rows=await f.commands(),calls=composeCalls(rows);
  assert.ok(calls.every(row=>row.scanning==='false' && !row.args.includes('scanner')));
  assert.ok(calls.filter(row=>['pull','up','ps'].includes(operation(row)[0])).every(row=>!operation(row).includes('clamav')));
  assert.deepEqual(rows.filter(row=>row.tool==='docker'&&row.args[0]==='stop').map(row=>row.args),[['stop','fixture-old-scanner']]);
  const query=rows.find(row=>row.tool==='docker'&&row.args[0]==='ps');
  assert.ok(query.args.includes('label=com.docker.compose.project=i-have-been') && query.args.includes('label=com.docker.compose.service=clamav'));
  const current=await realpath(join(f.base,'current'));
  assert.equal(JSON.parse(await readFile(join(current,'release.json'),'utf8')).malwareScanningEnabled,false);
  assert.equal(await readFile(join(f.base,'.env'),'utf8'),f.dotenv);
  const helper=spawnSync('python3',[join(current,'scripts/production-compose.py'),'ps'],{env:f.env,encoding:'utf8'});
  assert.equal(helper.status,0,helper.stderr);
  assert.equal((await f.commands()).at(-1).scanning,'false');
  assert.ok(!(result.stdout+result.stderr).includes('fixture-db-secret'));
});

test('invalid scanning flags abort before Docker',async t=>{
  const f=await fixture(t);f.args.push('off');
  assert.notEqual(f.run().status,0);
  assert.ok(!(await f.commands()).some(row=>row.tool==='docker'));
});

test('readiness failure restores the previous release scanning mode',async t=>{
  const f=await fixture(t,{failure:'health'});
  const state=join(f.base,'releases','previous','release.json');
  await writeFile(state,JSON.stringify({image:previousImage,hostname:f.args[3],trustedProxy:'172.20.0.3',malwareScanningEnabled:false}));
  const result=f.run();assert.notEqual(result.status,0);
  const starts=startsApp(await f.commands());
  assert.deepEqual(starts.map(row=>row.scanning),['true','false']);
  assert.ok((await realpath(join(f.base,'current'))).endsWith('/previous'));
});

test('scanner startup reports progress, waits for health and persists only selected diagnostics privately', async t => {
  const f=await fixture(t,{failure:'scanner-starting'}), result=f.run();
  assert.equal(result.status,0,result.stderr);
  assert.match(result.stdout,/clamav: state=running health=starting/);
  assert.match(result.stdout,/clamav: state=running health=healthy/);
  const path=join(f.base,'releases',incoming,'deployment.log'), log=await readFile(path,'utf8');
  assert.match(log,/Stage: infrastructure/);
  assert.match(log,/Stage: database-migration/);
  assert.match(log,/Production is ready/);
  assert.equal((await stat(path)).mode & 0o777,0o600);
  assert.equal(await readFile(f.env.TEST_HEALTH_COUNT,'utf8'),'3');
  const rows=await f.commands();
  assert.equal(rows.filter(row=>row.tool==='sleep').length,2);
  assert.ok(rows.filter(row=>row.tool==='docker'&&row.args[0]==='inspect').every(row=>row.args.includes('--format')));
  assert.ok(!composeCalls(rows).some(row=>operation(row)[0]==='logs'));
  for(const secret of ['fixture-db-secret','fixture-rpc-secret','fixture-admin-secret','fixture-access-key','fixture-storage-secret','fixture-registry-secret']) {
    assert.ok(!(log+result.stdout+result.stderr).includes(secret));
  }
});

for(const [failure,message] of [
  ['scanner-oom',/killed for exceeding available memory/],
  ['scanner-unhealthy',/failed its health check/],
  ['scanner-exited',/is unavailable/],
  ['scanner-missing-health',/failed its health check/],
  ['scanner-missing',/Missing clamav container/]
]) test(`${failure} stops before backup/migrations and preserves the previous release`, async t => {
  const f=await fixture(t,{failure}), result=f.run();
  assert.notEqual(result.status,0);
  assert.match(result.stderr,message);
  assert.match(result.stderr,/failed during infrastructure/);
  const rows=await f.commands();
  assert.equal(startsApp(rows).length,0);
  assert.ok(!composeCalls(rows).some(row=>['exec','run'].includes(operation(row)[0])));
  assert.ok((await realpath(join(f.base,'current'))).endsWith('/previous'));
  assert.equal(await readFile(join(f.base,'.env'),'utf8'),f.dotenv);
  assert.ok(!(await readdir(f.base)).some(name=>name.startsWith('.registry-')));
  const log=await readFile(join(f.base,'releases',incoming,'deployment.log'),'utf8');
  assert.match(log,/failed during infrastructure/);
  if(failure==='scanner-oom') assert.match(log,/OOMKilled=true/);
  assert.ok(!log.includes('fixture-db-secret') && !log.includes('fixture-registry-secret'));
});

test('SSH hangup records the interrupted stage, removes registry credentials and preserves the previous app', async t => {
  const f=await fixture(t,{failure:'hangup'}), result=f.run();
  assert.equal(result.status,129,result.stderr);
  assert.match(result.stderr,/database-migration \(exit 129\)/);
  assert.equal(startsApp(await f.commands()).length,0);
  assert.ok((await realpath(join(f.base,'current'))).endsWith('/previous'));
  assert.ok(!(await readdir(f.base)).some(name=>name.startsWith('.registry-')));
  assert.equal(await readFile(join(f.base,'.env'),'utf8'),f.dotenv);
});

test('migration failure leaves the previous application running and the current release untouched', async t => {
  const f = await fixture(t, {failure: 'migration'}), result = f.run();
  assert.notEqual(result.status, 0);
  assert.match(result.stderr, /database-migration/);
  assert.equal(startsApp(await f.commands()).length, 0);
  assert.ok((await realpath(join(f.base, 'current'))).endsWith('/previous'));
  assert.equal(await readFile(join(f.base, '.env'), 'utf8'), f.dotenv);
});

for (const failure of ['health', 'wrong-health']) test(`${failure} readiness failure restores the previous image without reverting the database`, async t => {
  const f = await fixture(t, {failure}), result = f.run();
  assert.notEqual(result.status, 0);
  assert.match(result.stderr, /Restoring the previous application/);
  const starts = startsApp(await f.commands());
  assert.deepEqual(starts.map(row => row.image), [image, previousImage]);
  assert.ok((await realpath(join(f.base, 'current'))).endsWith('/previous'));
  assert.ok((await f.commands()).every(row => !row.args.includes('down')));
});

test('an unhealthy first deployment stops its app rather than recording a successful release', async t => {
  const f = await fixture(t, {previous: false, failure: 'health'}), result = f.run();
  assert.notEqual(result.status, 0);
  assert.ok(composeCalls(await f.commands()).some(row => operation(row).join(' ') === 'stop app'));
  assert.ok(!(await readdir(f.base)).includes('current'));
});

test('a failed backup prevents provisioning, migration and app replacement', async t => {
  const f = await fixture(t, {failure: 'backup'}), result = f.run();
  assert.notEqual(result.status, 0);
  assert.equal(startsApp(await f.commands()).length, 0);
  assert.ok(!composeCalls(await f.commands()).some(row => operation(row)[0] === 'run'));
  assert.deepEqual(await readdir(join(f.base, 'backups')), []);
});

for (const failure of ['network', 'proxy']) test(`invalid shared ${failure} setup aborts before pulling or migrating`, async t => {
  const f = await fixture(t, {failure}), result = f.run();
  assert.notEqual(result.status, 0);
  assert.equal(composeCalls(await f.commands()).length, 0);
});

test('deployment rejects an unpinned image and a publicly readable production env file', async t => {
  const f = await fixture(t);
  const badArgs = [...f.args]; badArgs[2] = 'ghcr.io/owner/i-have-been:latest';
  assert.notEqual(spawnSync('bash', [join(repo, 'scripts/deploy-remote.sh'), ...badArgs], {env: f.env}).status, 0);
  await chmod(join(f.base, '.env'), 0o644);
  assert.notEqual(f.run().status, 0);
  assert.equal((await f.commands()).filter(row => row.tool === 'docker').length, 0);
});

test('SSH driver needs no runtime config and transfers only public release files, even with dotenv files in the runner workspace', async t => {
  const f = await fixture(t);
  const runner=join(f.root,'runner');await mkdir(runner);
  for(const file of [...releaseFiles,'scripts/deploy-remote.sh']) {
    await mkdir(join(runner,file,'..'),{recursive:true});
    await copyFile(join(repo,file),join(runner,file));
  }
  const forbidden='runner-only-runtime-credentials';
  await writeFile(join(runner,'.env'),forbidden);
  await writeFile(join(runner,'.env.production'),forbidden);
  const result = spawnSync('bash', [join(repo, 'scripts/deploy-production.sh')], {cwd: runner, env: {...f.env,
    DEPLOY_HOST: 'droplet.example.com', DEPLOY_USER: 'deployer', DEPLOY_PATH: f.base,
    DEPLOY_SSH_KEY: 'fixture-private-key', DEPLOY_SSH_KNOWN_HOSTS: 'fixture-pinned-host',
    APP_HOSTNAME: f.args[3], APP_IMAGE: image, RELEASE_SHA: sha, CADDY_CONTAINER: 'csl-caddy-1',
    GHCR_USERNAME: 'owner', GITHUB_RUN_ID: '1', GITHUB_RUN_ATTEMPT: '1'}, encoding: 'utf8', timeout: 30000});
  assert.equal(result.status, 0, result.stderr);
  const rows = await f.commands();
  for (const row of rows.filter(row => ['ssh', 'scp'].includes(row.tool))) {
    assert.ok(row.args.includes('StrictHostKeyChecking=yes'));
    assert.ok(row.args.includes('ServerAliveInterval=30'));
    assert.ok(row.args.includes('ServerAliveCountMax=6'));
    assert.ok(!JSON.stringify(row.args).includes('fixture-registry-secret'));
    assert.ok(!JSON.stringify(row.args).includes('fixture-private-key'));
  }
  assert.equal(rows.filter(row => row.tool === 'scp').length, 1);
  const uploaded=JSON.parse(await readFile(f.env.TEST_UPLOADS,'utf8'));
  assert.equal(uploaded.status,0);
  assert.deepEqual(uploaded.files.sort(),[...releaseFiles].sort());
  const payload=await readFile(f.env.TEST_SSH_INPUT,'utf8');
  assert.ok(!payload.includes(forbidden) && !payload.includes(f.dotenv));
  assert.ok(!(result.stdout+result.stderr).includes('fixture-db-secret'));
  assert.equal(await readFile(join(f.base,'.env'),'utf8'),f.dotenv);
  assert.ok(!(await readdir(join(f.base, 'incoming', incoming))).includes('release.tar.gz'));
});


test('SSH driver sends the nonsecret disabled mode while keeping droplet dotenv private',async t=>{
  const f=await fixture(t,{failure:'low-memory-with-scanner'});
  const result=spawnSync('bash',[join(repo,'scripts/deploy-production.sh')],{cwd:repo,env:{...f.env,
    DEPLOY_HOST:'droplet.example.com',DEPLOY_USER:'deployer',DEPLOY_PATH:f.base,
    DEPLOY_SSH_KEY:'fixture-private-key',DEPLOY_SSH_KNOWN_HOSTS:'fixture-pinned-host',
    APP_HOSTNAME:f.args[3],APP_IMAGE:image,RELEASE_SHA:sha,CADDY_CONTAINER:'csl-caddy-1',
    GHCR_USERNAME:'owner',GITHUB_RUN_ID:'1',GITHUB_RUN_ATTEMPT:'1',MALWARE_SCANNING_ENABLED:'false'},encoding:'utf8',timeout:30000});
  assert.equal(result.status,0,result.stderr);
  assert.equal(startsApp(await f.commands())[0].scanning,'false');
  assert.equal(await readFile(join(f.base,'.env'),'utf8'),f.dotenv);
});

test('SSH disconnect stays failed and points to the droplet status log without retrying deployment', async t => {
  const f=await fixture(t,{failure:'ssh-disconnect'});
  const result=spawnSync('bash',[join(repo,'scripts/deploy-production.sh')],{cwd:repo,env:{...f.env,
    DEPLOY_HOST:'droplet.example.com',DEPLOY_USER:'deployer',DEPLOY_PATH:f.base,
    DEPLOY_SSH_KEY:'fixture-private-key',DEPLOY_SSH_KNOWN_HOSTS:'fixture-pinned-host',
    APP_HOSTNAME:f.args[3],APP_IMAGE:image,RELEASE_SHA:sha,CADDY_CONTAINER:'csl-caddy-1',
    GHCR_USERNAME:'owner',GITHUB_RUN_ID:'1',GITHUB_RUN_ATTEMPT:'1'},encoding:'utf8',timeout:30000});
  assert.equal(result.status,255,result.stderr);
  assert.match(result.stderr,/Deployment completion is unknown/);
  assert.ok(result.stderr.includes(join(f.base,'releases',incoming,'deployment.log')));
  assert.equal((await f.commands()).filter(row=>row.tool==='ssh').length,2);
  assert.ok(!(result.stdout+result.stderr).includes('fixture-registry-secret'));
});

test('missing droplet .env aborts before Docker without accepting CI-provided credentials', async t => {
  const f=await fixture(t,{previous:false});await rm(join(f.base,'.env'));
  const result=f.run({PRODUCTION_ENV:f.dotenv});
  assert.notEqual(result.status,0);
  assert.match(result.stderr,/Missing private droplet .env/);
  assert.equal((await f.commands()).filter(row=>row.tool==='docker').length,0);
  assert.ok(!(await readdir(f.base)).includes('.env'));
});

test('pipeline env files and PRODUCTION_ENV cannot overwrite the droplet configuration', async t => {
  const f=await fixture(t);
  const supplied=f.dotenv.replace('fixture-db-secret','runner-changed-db-secret');
  await writeFile(join(f.base,'incoming',incoming,'production.env'),supplied,{mode:0o600});
  const result=f.run({PRODUCTION_ENV:supplied});
  assert.equal(result.status,0,result.stderr);
  assert.equal(await readFile(join(f.base,'.env'),'utf8'),f.dotenv);
  const after=await stat(join(f.base,'.env'));
  assert.deepEqual([after.ino,after.mtimeMs,after.mode],[f.originalStat.ino,f.originalStat.mtimeMs,f.originalStat.mode]);
  assert.ok(!(result.stdout+result.stderr).includes('runner-changed-db-secret'));
});

test('a symlinked runtime config is rejected before Docker access', async t => {
  const f=await fixture(t);await rm(join(f.base,'.env'));
  await writeFile(join(f.base,'private-env'),f.dotenv,{mode:0o600});
  await symlink(join(f.base,'private-env'),join(f.base,'.env'));
  assert.notEqual(f.run().status,0);
  assert.equal((await f.commands()).filter(row=>row.tool==='docker').length,0);
});

test('settings edited on the droplet are used and preserved by subsequent deployments', async t => {
  const f=await fixture(t), configured=f.dotenv+'ALLOW_REGISTRATION=false\n';
  await writeFile(join(f.base,'.env'),configured,{mode:0o600});
  const result=f.run();assert.equal(result.status,0,result.stderr);
  assert.equal(await readFile(join(f.base,'.env'),'utf8'),configured);
});

test('default development generator still creates a private local .env for disposable CI', async t => {
  const f=await fixture(t), setup=join(f.root,'generator-repo');
  await mkdir(join(setup,'scripts'),{recursive:true});
  await copyFile(join(repo,'scripts/init-env.py'),join(setup,'scripts/init-env.py'));
  await copyFile(join(repo,'.env.example'),join(setup,'.env.example'));
  const result=spawnSync('python3',[join(setup,'scripts/init-env.py')],{encoding:'utf8'});
  assert.equal(result.status,0,result.stderr);
  const content=await readFile(join(setup,'.env'),'utf8');
  assert.ok(content.includes('APP_PORT=4188'));
  assert.ok(/POSTGRES_PASSWORD=[a-f0-9]{48}/.test(content));
  assert.equal((await stat(join(setup,'.env'))).mode & 0o777,0o600);
});

test('production env generation requires an explicit deploy directory and refuses to overwrite existing credentials', async t => {
  const f=await fixture(t,{previous:false});await rm(join(f.base,'.env'));
  const script=join(repo,'scripts/init-env.py');
  const missing=spawnSync('python3',[script,'--production'],{encoding:'utf8'});
  assert.notEqual(missing.status,0);assert.match(missing.stderr,/requires --deploy-path/);
  const args=[script,'--production','--deploy-path',f.base];
  const generated=spawnSync('python3',args,{encoding:'utf8'});
  assert.equal(generated.status,0,generated.stderr);
  const content=await readFile(join(f.base,'.env'),'utf8');
  assert.ok(/POSTGRES_PASSWORD=[a-f0-9]{48}/.test(content));
  assert.equal((await stat(join(f.base,'.env'))).mode & 0o777,0o600);
  assert.ok(!(await readdir(f.base)).includes('.env.production'));
  assert.notEqual(spawnSync('python3',args,{encoding:'utf8'}).status,0);
  assert.equal(await readFile(join(f.base,'.env'),'utf8'),content);
});

test('monitoring opt-in starts only the private collector and requires real cloud configuration', async t => {
  const f = await fixture(t);
  const monitored = f.dotenv + 'TELEMETRY_ENABLED=true\nCOMPOSE_PROFILES=observability\nGRAFANA_CLOUD_OTLP_ENDPOINT=https://otlp-gateway-prod-us-central-0.grafana.net/otlp\nGRAFANA_CLOUD_INSTANCE_ID=123\nGRAFANA_CLOUD_API_TOKEN=fixture-cloud-write-token-123\n';
  await writeFile(join(f.base, '.env'), monitored, {mode: 0o600});
  const result = f.run();
  assert.equal(result.status, 0, result.stderr);
  const rows = await f.commands();
  assert.ok(composeCalls(rows).some(row => operation(row)[0] === 'pull' && operation(row).includes('otel-collector')));
  assert.ok(composeCalls(rows).some(row => operation(row)[0] === 'up' && operation(row).includes('otel-collector')));
});

for (const [name, values] of [
  ['missing profile', 'TELEMETRY_ENABLED=true\n'],
  ['missing credentials', 'TELEMETRY_ENABLED=true\nCOMPOSE_PROFILES=observability\n'],
  ['insecure export', 'TELEMETRY_ENABLED=true\nCOMPOSE_PROFILES=observability\nGRAFANA_CLOUD_OTLP_ENDPOINT=http://untrusted.example/otlp\n']
]) test(`monitoring rejects ${name} without modifying its droplet config or changing services`, async t => {
  const f = await fixture(t);
  await writeFile(join(f.base, '.env'), f.dotenv + values, {mode: 0o600});
  const result = f.run();
  assert.notEqual(result.status, 0);
  assert.equal(composeCalls(await f.commands()).length, 0);
  assert.equal(await readFile(join(f.base, '.env'), 'utf8'), f.dotenv + values);
});
