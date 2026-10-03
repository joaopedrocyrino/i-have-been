import assert from 'node:assert/strict';
import {execFileSync} from 'node:child_process';

// Render with dummy example values; never print a resolved production environment.
const config = JSON.parse(execFileSync('docker', ['compose', '-p', 'i-have-been', '--env-file', '.env.example', '-f', 'docker-compose.prod.yml', '--profile', 'deploy', 'config', '--format', 'json'], {
  encoding: 'utf8', env: {...process.env, APP_IMAGE: `ghcr.io/example/i-have-been@sha256:${'0'.repeat(64)}`, APP_HOSTNAME: 'i-have-been.example.com', TRUSTED_PROXY_IP: '172.20.0.2'}
}));
assert.deepEqual(Object.keys(config.services).sort(), ['app', 'clamav', 'garage', 'garage-init', 'migrate', 'postgres']);
for (const service of Object.values(config.services)) {
  assert.ok(!service.build, 'Production must use the tested registry image');
  assert.ok(!service.ports?.length, 'No production service publishes host ports');
}
assert.equal(config.services.app.environment.ASPNETCORE_ENVIRONMENT, 'Production');
assert.equal(config.services.app.environment.Security__TrustedProxies__0, '172.20.0.2');
assert.equal(config.services.app.labels.caddy, 'i-have-been.example.com');
assert.equal(config.services.app.labels.caddy_ingress_network, 'proxy');
assert.equal(config.services.app.labels['caddy.reverse_proxy'], '{{upstreams 8080}}');
assert.equal(config.networks.proxy.external, true);
assert.equal(config.networks.proxy.name, 'proxy');
assert.equal(config.networks.private.internal, true);
for (const name of ['postgres', 'garage', 'clamav', 'migrate', 'garage-init']) assert.ok(!config.services[name].networks.proxy, `${name} must stay off the shared proxy network`);
assert.equal(config.services.migrate.image, config.services.app.image);
assert.deepEqual(config.services.migrate.profiles, ['deploy']);
assert.deepEqual(config.services['garage-init'].profiles, ['deploy']);
assert.equal(config.volumes.protection_keys.name, 'i-have-been_protection_keys');
const monitored = JSON.parse(execFileSync('docker', ['compose', '-p', 'i-have-been', '--env-file', '.env.example', '-f', 'docker-compose.prod.yml', '--profile', 'deploy', '--profile', 'observability', 'config', '--format', 'json'], {
  encoding: 'utf8', env: {...process.env, APP_IMAGE: `ghcr.io/example/i-have-been@sha256:${'0'.repeat(64)}`, APP_HOSTNAME: 'i-have-been.example.com', TRUSTED_PROXY_IP: '172.20.0.2', TELEMETRY_ENABLED: 'true', GRAFANA_CLOUD_API_TOKEN: 'fixture-private-token'}
}));
const collector = monitored.services['otel-collector'];
assert.ok(collector && !collector.networks.proxy && !collector.ports?.length);
assert.ok('private' in collector.networks && 'telemetry_egress' in collector.networks);
assert.equal(collector.mem_limit, '268435456');
assert.equal(collector.read_only, true);
assert.equal(String(collector.cpu_period), "100000");
assert.equal(String(collector.cpu_quota), "50000");
assert.equal(monitored.services.app.environment.Telemetry__Endpoint, 'http://otel-collector:4318');
assert.ok(!Object.keys(monitored.services.app.environment).some(key => key.includes('GRAFANA')));
assert.ok(!collector.volumes.some(volume => volume.source.includes('docker.sock')));
console.log('Production Compose validation passed: shared Caddy, private services, persistent keys, no builds or test containers.');

// The disposable override must preserve app security and private service isolation.
const ci = JSON.parse(execFileSync('docker', ['compose', '-p', 'ihb-ci', '--env-file', '.env.example', '-f', 'docker-compose.yml', '-f', 'docker-compose.ci.yml', '--profile', 'observability', 'config', '--format', 'json'], {encoding: 'utf8'}));
assert.equal(ci.services.clamav.image, config.services.clamav.image.replace(/_base$/, ''));
assert.equal(ci.services.clamav.mem_limit, config.services.clamav.mem_limit);
assert.ok(!ci.services.clamav.ports?.length);
assert.ok(ci.networks.private.internal && !ci.services.clamav.networks.edge);
assert.equal(ci.services.clamav.volumes.find(volume => volume.target === '/var/lib/clamav').type, 'bind');
assert.equal(ci.services.clamav.volumes.find(volume => volume.target === '/etc/clamav/clamd.conf').read_only, true);
assert.deepEqual(ci.services.clamav.entrypoint, ['/bin/sh', '/etc/clamav/ci-entrypoint.sh']);
assert.equal(ci.services.clamav.volumes.find(volume => volume.target === '/etc/clamav/ci-entrypoint.sh').read_only, true);
assert.deepEqual(ci.services.clamav.healthcheck.test, ['CMD', 'clamdcheck.sh']);
assert.equal(ci.services.clamav.volumes.find(volume => volume.target === '/usr/local/bin/clamdcheck.sh').read_only, true);
assert.equal(ci.services.app.depends_on.clamav.condition, 'service_healthy');
assert.equal(ci.services.app.environment.MalwareScanning__Host, 'clamav');
assert.ok(!Object.keys(ci.services.app.environment).some(key => /DefinitionAge|Disable.*Scan|Skip.*Scan/i.test(key)));
console.log('CI scanner validation passed: same engine, real scanning, freshness gate, private network and isolated cache.');
