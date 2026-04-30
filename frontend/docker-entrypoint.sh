#!/bin/sh
set -e

API_KEY="${ASSTRACK_API_KEY:-}"

# Write runtime config for the frontend SPA
# apiKey is the operator key used by the frontend for X-Api-Key auth
cat > /usr/share/nginx/html/config.json <<EOF
{"apiKey":"${API_KEY}"}
EOF

exec nginx -g 'daemon off;'
