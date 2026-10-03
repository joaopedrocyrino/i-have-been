#!/usr/bin/env bash
set -Eeuo pipefail
umask 077

base=$1
incoming=$2
export APP_IMAGE=$3 APP_HOSTNAME=$4
caddy_container=$5
registry_user=$6
[[ "$base" =~ ^/[a-zA-Z0-9_/-]+$ && "$base" != / && "$base" != /opt ]]
[[ "$incoming" =~ ^[a-f0-9]{40}-[0-9]+-[0-9]+$ ]]
[[ "$APP_IMAGE" =~ ^ghcr\.io/[a-z0-9._/-]+@sha256:[a-f0-9]{64}$ ]]
[[ "$APP_HOSTNAME" =~ ^[a-z0-9][a-z0-9.-]+\.[a-z]{2,}$ && "$caddy_container" =~ ^[a-zA-Z0-9][a-zA-Z0-9_.-]*$ ]]
[[ "$registry_user" =~ ^[a-zA-Z0-9][a-zA-Z0-9-]*$ && -n "${GHCR_TOKEN:-}" ]]
env_file="$base/.env"
registry_config=""
cleanup() {
  [[ -z "$registry_config" ]] || rm -rf -- "$registry_config"
}
trap cleanup EXIT
command -v flock >/dev/null
command -v curl >/dev/null
command -v python3 >/dev/null
command -v awk >/dev/null
if [[ ! -f "$env_file" || -L "$env_file" ]]; then
  echo 'Missing private droplet .env: create it in the deployment directory before deploying.' >&2
  exit 1
fi

# Never operate on the shared Caddy stack. This project owns its lock and volumes.
exec 9>"$base/.deploy.lock"
flock -n 9 || { echo 'Another I Have Been deployment is running.' >&2; exit 1; }
release="$base/releases/$incoming"
mkdir -p "$release" "$base/backups"
tar -xzf "$base/incoming/$incoming/release.tar.gz" -C "$release"
rm -f -- "$base/incoming/$incoming/release.tar.gz"
python3 "$release/scripts/validate-production-env.py" "$env_file"
docker network inspect proxy >/dev/null
export TRUSTED_PROXY_IP
TRUSTED_PROXY_IP=$(docker inspect --format '{{.NetworkSettings.Networks.proxy.IPAddress}}' "$caddy_container")
python3 -c 'import ipaddress,sys; ipaddress.ip_address(sys.argv[1])' "$TRUSTED_PROXY_IP"

export RELEASE_SHA=${incoming%%-*}
export DOCKER_CONFIG
registry_config=$(mktemp -d "$base/.registry-XXXXXX")
DOCKER_CONFIG=$registry_config
previous=""
[[ ! -L "$base/current" ]] || previous=$(readlink -f "$base/current")
[[ -z "$previous" || "$previous" == "$base/releases/"* ]]
app_replaced=false
stage=preflight
report() {
  local timestamp
  timestamp=$(date -u +%Y-%m-%dT%H:%M:%SZ)
  # Persist only our stage/progress and selected host/container fields, never env or raw service logs.
  printf '%s %s\n' "$timestamp" "$*" >> "$release/deployment.log"
  printf '%s %s\n' "$timestamp" "$*"
}
set_stage() { stage=$1; report "Stage: $stage."; }
resources() {
  local snapshot
  if command -v free >/dev/null && snapshot=$(free -m); then report "Host memory (MiB):
$snapshot"; fi
  if snapshot=$(df -h "$base"); then report "Deployment disk:
$snapshot"; fi
}
state_format='{{.State.Status}}|{{if .State.Health}}{{.State.Health.Status}}{{else}}missing{{end}}|{{.State.OOMKilled}}|{{.State.ExitCode}}|{{.RestartCount}}'
compose() { docker compose -p i-have-been --profile deploy --env-file "$env_file" -f "$release/docker-compose.prod.yml" "$@"; }

diagnostics() {
  local service container details
  resources
  for service in postgres garage clamav app; do
    container=$(compose ps -a -q "$service" 2>/dev/null) || continue
    [[ -n "$container" ]] || continue
    details=$(docker inspect --format "$state_format" "$container" 2>/dev/null) || continue
    report "$service: state|health|OOMKilled|exit|restarts=$details"
  done
}
failed() {
  local status=${1:-$?}
  trap - ERR HUP INT TERM
  report "Production deployment failed during $stage (exit $status)." >&2 || true
  diagnostics >&2 || true
  if [[ "$app_replaced" == true && -n "$previous" ]]; then
    echo 'Restoring the previous application image; database migrations are retained.' >&2
    export APP_IMAGE APP_HOSTNAME
    RELEASE_SHA=${previous##*/}
    export RELEASE_SHA=${RELEASE_SHA%%-*}
    APP_IMAGE=$(python3 -c 'import json,sys; print(json.load(open(sys.argv[1]))["image"])' "$previous/release.json")
    APP_HOSTNAME=$(python3 -c 'import json,sys; print(json.load(open(sys.argv[1]))["hostname"])' "$previous/release.json")
    docker compose -p i-have-been --profile deploy --env-file "$env_file" -f "$previous/docker-compose.prod.yml" up -d --no-deps --no-build app || echo 'Application rollback failed; inspect deployment logs.' >&2
  elif [[ "$app_replaced" == true ]]; then
    compose stop app || true
  fi
  [[ -z "${backup:-}" ]] || rm -f -- "$backup.partial"
  exit "$status"
}
trap failed ERR
trap 'failed 129' HUP
trap 'failed 130' INT
trap 'failed 143' TERM
set_stage preflight
compose config --quiet
set_stage resource-preflight
# Linux physical RAM only: swap and Docker's 4g scanner limit do not provide host capacity.
# 3 GiB is a rejection floor, not a sufficient shared-host sizing recommendation.
memory_kib=$(awk '$1 == "MemTotal:" && $3 == "kB" {print $2}' /proc/meminfo)
[[ "$memory_kib" =~ ^[0-9]{1,12}$ ]] || { report 'Cannot determine physical host RAM from /proc/meminfo.' >&2; false; }
if ((10#$memory_kib < 3 * 1024 * 1024)); then
  report "Insufficient physical host RAM: $((10#$memory_kib / 1024)) MiB; this production stack requires at least 3072 MiB before deployment. Resize to a 4 GiB or larger plan, with additional capacity for shared services, or move the scanner to a private host." >&2
  false
fi
report "Physical host RAM: $((10#$memory_kib / 1024)) MiB; minimum-capacity check passed (shared-service headroom still required)."
unset memory_kib
printf '%s' "$GHCR_TOKEN" | docker login ghcr.io --username "$registry_user" --password-stdin
unset GHCR_TOKEN
set_stage pull
telemetry_services=()
case $'\n'$(compose config --services)$'\n' in
  *$'\notel-collector\n'*) telemetry_services+=(otel-collector) ;;
esac
compose pull postgres garage clamav garage-init migrate app "${telemetry_services[@]}"
set_stage infrastructure
resources
compose up -d --no-build postgres garage clamav "${telemetry_services[@]}"
deadline=$((SECONDS + 1200))
for service in postgres garage clamav; do
  next_report=0
  started=$SECONDS
  while true; do
    container=$(compose ps -a -q "$service")
    [[ -n "$container" ]] || { report "Missing $service container." >&2; false; }
    details=$(docker inspect --format "$state_format" "$container")
    IFS='|' read -r state health oom exit_code restarts <<< "$details"
    if ((SECONDS >= next_report)) || [[ "$health" == healthy || "$health" == unhealthy || "$oom" == true ]]; then
      report "$service: state=$state health=$health OOMKilled=$oom exit=$exit_code restarts=$restarts; waited $((SECONDS - started))s."
      next_report=$((SECONDS + 30))
    fi
    [[ "$oom" != true ]] || { report "$service was killed for exceeding available memory; inspect the host kernel logs." >&2; false; }
    case "$state" in
      running|restarting|created) ;;
      *) report "$service is unavailable (state=$state)." >&2; false ;;
    esac
    [[ "$health" != unhealthy && "$health" != missing ]] || { report "$service failed its health check (health=$health)." >&2; false; }
    [[ "$state" != running || "$health" != healthy ]] || break
    ((SECONDS < deadline)) || { report "Timed out waiting for $service." >&2; false; }
    sleep 5
  done
done
set_stage database-backup
backup="$base/backups/deploy-$(date -u +%Y%m%dT%H%M%SZ)-$incoming.dump"
# Expand the connection names inside the database container.
# shellcheck disable=SC2016
compose exec -T postgres sh -c 'exec pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" --format=custom --no-owner' > "$backup.partial"
mv -- "$backup.partial" "$backup"
set_stage storage-provisioning
compose run --rm --no-deps garage-init
set_stage database-migration
compose run --rm --no-deps migrate
set_stage application-start
app_replaced=true
compose up -d --no-deps --no-build app
set_stage https-readiness
ready=false
for ((attempt=0; attempt<30; attempt++)); do
  if curl --fail --silent --show-error --max-time 10 \
    --resolve "$APP_HOSTNAME:443:127.0.0.1" "https://$APP_HOSTNAME/health/ready" > "$release/health.json" && \
    python3 -c 'import json,sys; sys.exit(json.load(open(sys.argv[1])).get("status") != "ready")' "$release/health.json"; then
    ready=true
    break
  fi
  sleep 5
done
[[ "$ready" == true ]]
set_stage record-release
python3 - "$release" "$APP_IMAGE" "$APP_HOSTNAME" "$TRUSTED_PROXY_IP" <<'PY'
import json,sys
from pathlib import Path
release,image,hostname,proxy = sys.argv[1:]
Path(release,'release.json').write_text(json.dumps({'image':image,'hostname':hostname,'trustedProxy':proxy},indent=2)+'\n')
PY
ln -s "$release" "$base/.current.next"
mv -Tf -- "$base/.current.next" "$base/current"
report "Production is ready at https://$APP_HOSTNAME, image $APP_IMAGE."
