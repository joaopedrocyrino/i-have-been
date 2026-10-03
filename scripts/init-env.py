#!/usr/bin/env python3
from pathlib import Path
import secrets, os, argparse
parser = argparse.ArgumentParser()
parser.add_argument('--production', action='store_true', help='Generate the droplet .env from the production template.')
parser.add_argument('--deploy-path', type=Path, help='Existing droplet deployment directory; required with --production.')
args = parser.parse_args()
repo = Path(__file__).resolve().parents[1]
if args.production and args.deploy_path is None:
    parser.error('--production requires --deploy-path pointing to the droplet deployment directory')
if not args.production and args.deploy_path is not None:
    parser.error('--deploy-path is only supported with --production')
filename = '.env'
path = (args.deploy_path.resolve() if args.production else repo) / filename
if not path.parent.is_dir():
    parser.error('Create the deployment directory before generating its .env')
if path.exists():
    raise SystemExit(f'{filename} already exists; refusing to overwrite credentials.')
values = {'POSTGRES_PASSWORD': secrets.token_hex(24), 'GARAGE_RPC_SECRET': secrets.token_hex(32), 'GARAGE_ADMIN_TOKEN': secrets.token_hex(32), 'S3_ACCESS_KEY_ID': 'GK' + secrets.token_hex(12), 'S3_SECRET_ACCESS_KEY': secrets.token_hex(32)}
content = (repo / ('.env.production.example' if args.production else '.env.example')).read_text()
for key, value in values.items(): content = content.replace(key + '=replace-me', key + '=' + value)
fd = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
with os.fdopen(fd, 'w') as f: f.write(content)
print(f'Created private {filename} with fresh credentials in {path.parent}.')
