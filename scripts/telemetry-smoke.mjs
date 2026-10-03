import assert from 'node:assert/strict';
import {execFileSync, spawnSync} from 'node:child_process';

const project = process.argv[2];
if (!/^ihb-[a-z0-9-]*(?:ci|check|test)[a-z0-9-]*$/.test(project ?? '')) throw new Error('Use a disposable ihb-* CI/check/test stack.');
const docker = args => execFileSync('docker', args, {encoding: 'utf8', timeout: 30000});
const app = `${project}-app-1`, collector = `${project}-otel-collector-1`;
const marker = 'PRIVATE_TELEMETRY_CANARY_7d4d';
function request(path) {
  return JSON.parse(docker(['run', '--rm', '--network', `${project}_private`, 'node:22-alpine', 'node', '-e',
    `fetch(${JSON.stringify('http://app:8080' + path)}, {headers: {baggage: ${JSON.stringify('private=' + marker)}}}).then(async r => console.log(JSON.stringify({status:r.status,body:await r.text()})))`]));
}
let ready = false;
for (let attempt = 0; attempt < 30; attempt++) {
  try { ready = request('/health/ready').status === 200; } catch {}
  if (ready) break;
  await new Promise(resolve => setTimeout(resolve, 1000));
}
assert.ok(ready, 'Isolated app did not become ready');
assert.equal(request('/api/logs?secret=' + marker).status, 401);
assert.equal(request('/unknown-' + marker).status, 404);
let output = '';
for (let attempt = 0; attempt < 20; attempt++) {
  const result = spawnSync('docker', ['logs', '--since', '1m', collector], {encoding: 'utf8', maxBuffer: 10 * 1024 * 1024});
  assert.equal(result.status, 0, 'Cannot read isolated collector logs');
  output = result.stdout + result.stderr;
  if (output.includes('ihb.http.request.duration') && output.includes('/api/logs') && output.includes('ihb.database.duration')) break;
  await new Promise(resolve => setTimeout(resolve, 1000));
}
assert.ok(output.includes('ihb.http.request.duration'), 'Missing exported HTTP histogram');
assert.ok(output.includes('ihb.database.duration'), 'Missing exported DB histogram');
assert.ok(output.includes('dotnet.process.memory.working_set'), 'Missing runtime memory metric');
assert.ok(/http.route: Str\(\/api\/logs\/?\)/.test(output), 'Missing route template');
assert.ok(!output.includes(marker), 'Private request data entered exported telemetry');
const prometheus = JSON.parse(docker(['run', '--rm', '--network', `${project}_private`, 'node:22-alpine', 'node', '-e',
  "fetch('http://otel-collector:8889/metrics').then(async r=>console.log(JSON.stringify(await r.text())))"]));
for (const name of ['ihb_http_request_duration_seconds_bucket', 'ihb_database_duration_seconds_bucket', 'dotnet_process_memory_working_set_bytes', 'ihb_dependency_healthy', 'ihb_http_active_requests', 'dotnet_process_cpu_time_seconds_total', 'dotnet_gc_pause_time_seconds_total'])
  assert.ok(prometheus.includes(name), `Dashboard metric name ${name} did not match exported series`);
assert.ok(prometheus.includes('deployment_environment_name="development"'));
assert.ok(prometheus.includes('job="i-have-been"'));
assert.ok(!prometheus.includes(marker));
const settings = JSON.parse(docker(['inspect', '--format', '{{json .HostConfig}}', collector]));
assert.equal(settings.Memory, 256 * 1024 * 1024);
assert.equal(settings.ReadonlyRootfs, true);
assert.equal(settings.CpuPeriod, 100000);
assert.equal(settings.CpuQuota, 50000);
assert.ok(!settings.PortBindings || Object.keys(settings.PortBindings).length === 0);
// A broken collector must not enter readiness dependencies or block journal requests.
docker(['stop', collector]);
try {
  assert.equal(request('/health/ready').status, 200);
  assert.equal(request('/api/logs?secret=' + marker).status, 401);
  assert.equal(docker(['inspect', '--format', '{{.State.Running}}', app]).trim(), 'true');
} finally { docker(['start', collector]); }
console.log('Telemetry smoke passed: real OTLP export, HTTP/DB/runtime metrics, privacy, private bounded collector, exporter outage isolation.');
