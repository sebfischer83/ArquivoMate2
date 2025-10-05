#!/bin/sh
set -e

RUNTIME_CFG="/usr/share/nginx/html/runtime-config.json"
RUNTIME_TEMPLATE="/usr/share/nginx/html/runtime-config.template.json"

VERSION_VALUE="${VERSION:-0.0.0-dev}" 
API_BASE_URL_VALUE="${API_BASE_URL:-http://localhost:5000}" 
OIDC_ISSUER_VALUE="${OIDC_ISSUER:-https://example-issuer.local/realms/app/}" 
OIDC_CLIENT_ID_VALUE="${OIDC_CLIENT_ID:-spa-client}" 
OIDC_SCOPE_VALUE="${OIDC_SCOPE:-openid profile email}" 

if [ -f "$RUNTIME_TEMPLATE" ]; then
  cp "$RUNTIME_TEMPLATE" "$RUNTIME_CFG"
fi

if [ -f "$RUNTIME_CFG" ]; then
  # Replace tokens (global) – tokens expected to appear exactly once
  sed -i "s#__VERSION__#${VERSION_VALUE}#g" "$RUNTIME_CFG"
  sed -i "s#__API_BASE_URL__#${API_BASE_URL_VALUE}#g" "$RUNTIME_CFG"
  sed -i "s#__OIDC_ISSUER__#${OIDC_ISSUER_VALUE}#g" "$RUNTIME_CFG"
  sed -i "s#__OIDC_CLIENT_ID__#${OIDC_CLIENT_ID_VALUE}#g" "$RUNTIME_CFG"
  sed -i "s#__OIDC_SCOPE__#${OIDC_SCOPE_VALUE}#g" "$RUNTIME_CFG"
  echo "[entrypoint] Injected runtime-config: VERSION=$VERSION_VALUE API_BASE_URL=$API_BASE_URL_VALUE OIDC_ISSUER=$OIDC_ISSUER_VALUE OIDC_CLIENT_ID=$OIDC_CLIENT_ID_VALUE OIDC_SCOPE=$OIDC_SCOPE_VALUE"
else
  echo "[entrypoint] runtime-config.json not found (skipped)" >&2
fi

exec "$@"
