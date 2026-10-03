#!/bin/sh
set -eu

# Finish updates before clamd loads its database. Starting both concurrently can
# lose FreshClam's reload notification while the daemon is still initializing.
chown -R clamav:clamav /var/lib/clamav
if ! freshclam --foreground --stdout --no-warnings; then
  echo 'FreshClam update failed; CI still requires fresh cached definitions.' >&2
fi
exec /init "$@"
