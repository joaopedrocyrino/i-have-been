#!/usr/bin/env python3
"""Validate droplet-owned runtime configuration without changing or printing it."""
from pathlib import Path
import os
import re
import stat
import sys

path = Path(sys.argv[1])

def read(path):
    if not path.exists():
        raise SystemExit('Create the private droplet .env before deploying.')
    metadata = path.stat()
    if path.is_symlink() or not stat.S_ISREG(metadata.st_mode) or stat.S_IMODE(metadata.st_mode) != 0o600 or metadata.st_uid != os.geteuid():
        raise SystemExit('The droplet .env must be a regular mode-0600 file owned by the deployment user.')
    values = {}
    for line in path.read_text().splitlines():
        if not line.strip() or line.lstrip().startswith('#'):
            continue
        key, separator, value = line.partition('=')
        if not separator or not re.fullmatch(r'[A-Z][A-Z0-9_]*', key) or key in values:
            raise SystemExit('Use the unquoted KEY=value production environment template, without duplicate keys.')
        values[key] = value
    return values

new = read(path)
required = ['POSTGRES_PASSWORD', 'GARAGE_RPC_SECRET', 'GARAGE_ADMIN_TOKEN', 'S3_ACCESS_KEY_ID', 'S3_SECRET_ACCESS_KEY']
for key in required:
    value = new.get(key, '')
    if not value or value.startswith('replace-') or any(character in value for character in '\"\'\\$ \t'):
        raise SystemExit(f'Configure a real unquoted production value for {key}.')
enabled = new.get('TELEMETRY_ENABLED', 'false')
profiles = new.get('COMPOSE_PROFILES', '').split(',')
if enabled not in ('true', 'false') or (enabled == 'true') != ('observability' in profiles):
    raise SystemExit('Set TELEMETRY_ENABLED=true together with COMPOSE_PROFILES=observability, or disable both.')
if enabled == 'true':
    # Restrict credentials to the official Grafana Cloud OTLP gateway over verified HTTPS.
    from urllib.parse import urlsplit
    try:
        endpoint = urlsplit(new.get('GRAFANA_CLOUD_OTLP_ENDPOINT', ''))
        port = endpoint.port
    except ValueError:
        raise SystemExit('Use a valid Grafana Cloud HTTPS OTLP URL.')
    if endpoint.scheme != 'https' or not (endpoint.hostname or '').endswith('.grafana.net') or endpoint.username or endpoint.password or endpoint.query or endpoint.fragment or endpoint.path != '/otlp' or port not in (None, 443):
        raise SystemExit('Use the HTTPS /otlp endpoint supplied by Grafana Cloud.')
    if not new.get('GRAFANA_CLOUD_INSTANCE_ID', '').isdigit():
        raise SystemExit('Configure the Grafana Cloud numeric instance ID.')
    token = new.get('GRAFANA_CLOUD_API_TOKEN', '')
    if len(token) < 20 or token.startswith('replace-') or any(c in token for c in "\"'\\$ \t"):
        raise SystemExit('Configure a private Grafana Cloud write token.')
