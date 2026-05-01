#!/bin/sh
set -e

API_KEY="${ASSTRACK_API_KEY:-}"

# Write runtime config for the frontend SPA
# apiKey is the operator key used by the frontend for X-Api-Key auth
printf '{"apiKey":"%s"}\n' "$(printf '%s' "$API_KEY" | sed 's/\\/\\\\/g; s/"/\\"/g')" > /usr/share/nginx/html/config.json

exec nginx -g 'daemon off;'
