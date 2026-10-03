#!/bin/sh
# BusyBox date is provided by the official Alpine ClamAV image.
set -eu

if [ "$(printf 'PING\n' | nc -w 2 127.0.0.1 3310)" != PONG ]; then
  echo 'ClamAV daemon is not responding.' >&2
  exit 1
fi
version=$(printf 'VERSION\n' | nc -w 2 127.0.0.1 3310)
case "$version" in
  'ClamAV '*/*/*) database_date=${version##*/} ;;
  *) echo 'ClamAV did not report a dated signature database.' >&2; exit 1 ;;
esac
if ! updated=$(date -u -D '%a %b %e %H:%M:%S %Y' -d "$database_date" +%s); then
  echo 'ClamAV signature date could not be parsed.' >&2
  exit 1
fi
now=$(date -u +%s)
age=$((now - updated))
# Match the application defaults: at most 72 hours old, at most 5 minutes ahead.
if [ "$age" -lt -300 ] || [ "$age" -gt 259200 ]; then
  echo 'ClamAV definitions are outside the 72-hour freshness window; waiting for FreshClam.' >&2
  exit 1
fi
echo 'ClamAV is ready with fresh signature definitions.'
