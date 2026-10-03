#!/usr/bin/env bash
set -euo pipefail

[[ "${CI:-}" == true ]] || { echo 'Signature cache seeding is restricted to disposable CI.' >&2; exit 1; }
signature_dir="$PWD/output/clamav"
mkdir -p "$signature_dir"
image=$(docker compose config --format json | python3 -c 'import json,sys; print(json.load(sys.stdin)["services"]["clamav"]["image"])')
[[ "$image" =~ ^clamav/clamav:[0-9]+\.[0-9]+\.[0-9]+$ ]] || { echo 'CI must use an official patch-version image with bundled signatures.' >&2; exit 1; }

# Only public, signed AV databases enter the cache. Never copy freshclam.dat,
# runtime dotenv files, Docker credentials or application data.
# Run as the host user so the cache remains readable by the Actions runner.
docker run --rm --pull always --platform linux/amd64 --network none --read-only \
  --cap-drop ALL --security-opt no-new-privileges:true --user "$(id -u):$(id -g)" \
  --entrypoint sh -v "$signature_dir:/export" "$image" -c '
    set -eu
    for database in main daily bytecode; do
      if [ ! -s "/export/$database.cvd" ] && [ ! -s "/export/$database.cld" ]; then
        cp "/var/lib/clamav/$database.cvd" /export/
      fi
    done
    for signature in /var/lib/clamav/*.sign; do
      [ -f "$signature" ] || continue
      destination="/export/${signature##*/}"
      [ -e "$destination" ] || cp "$signature" "$destination"
    done
  '
