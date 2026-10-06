#!/usr/bin/env python3
"""Run Compose for the currently deployed release without printing credentials."""
import json
import os
import sys
from pathlib import Path

release = Path(__file__).resolve().parents[1]
base = release.parents[1]
state = json.loads((release / 'release.json').read_text())
os.environ['RELEASE_SHA'] = release.name.split('-')[0]
os.environ.update(APP_IMAGE=state['image'], APP_HOSTNAME=state['hostname'], TRUSTED_PROXY_IP=state['trustedProxy'])
os.environ['MALWARE_SCANNING_ENABLED'] = str(state.get('malwareScanningEnabled', True)).lower()
profiles = ['--profile', 'scanner'] if os.environ['MALWARE_SCANNING_ENABLED'] == 'true' else []
os.execvp('docker', ['docker', 'compose', '-p', 'i-have-been', *profiles, '--env-file', str(base / '.env'), '-f', str(release / 'docker-compose.prod.yml'), *sys.argv[1:]])
