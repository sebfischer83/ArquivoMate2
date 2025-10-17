# Runtime Configuration Guide

## Overview

The ArquivoMate2 UI uses a **runtime configuration system** that allows environment-specific settings to be injected at container startup, rather than being baked into the build. This enables the same Docker image to be deployed across different environments (development, staging, production) without rebuilding.

## Architecture

### Configuration Files

#### `runtime-config.template.json` (Template with Placeholders)
Located in `public/runtime-config.template.json`, this file contains placeholder tokens that will be replaced during container startup:

```json
{
  "apiBaseUrl": "__API_BASE_URL__",
  "version": "__VERSION__",
  "auth": {
    "issuer": "__OIDC_ISSUER__",
    "clientId": "__OIDC_CLIENT_ID__",
    "scope": "__OIDC_SCOPE__"
  }
}
```

**Placeholders:**
- `__API_BASE_URL__` → Backend API base URL (e.g., `https://api.example.com`)
- `__VERSION__` → Application version (e.g., `1.2.3`)
- `__OIDC_ISSUER__` → OpenID Connect issuer URL (e.g., `https://auth.example.com/realms/app/`)
- `__OIDC_CLIENT_ID__` → OAuth 2.0 / OIDC client ID
- `__OIDC_SCOPE__` → OAuth scopes (e.g., `openid profile email roles`)

#### `runtime-config.json` (Generated at Runtime)
This file is generated from the template during container startup and loaded by the Angular application at runtime.

### Configuration Loading Flow

```
Development / Build Time:
  runtime-config.template.json
           ↓ (copied to dist/)
  
Docker Build:
  ✓ Image created with template file
  ✓ Entrypoint script copied
  
Container Startup:
  docker-entrypoint.sh executes:
  1. Copy template → runtime-config.json
  2. Read environment variables
  3. Use sed to replace placeholders
  4. Nginx serves updated config
  
Browser / Angular App:
  fetch('runtime-config.json')
  → Parse & use for API calls, auth config
```

## Configuration Process

### 1. Docker Build Stage

The `Dockerfile` multi-stage build process:

```dockerfile
# Build stage - Angular compilation
FROM node:20-alpine AS build
WORKDIR /app
COPY package.json package-lock.json* ./
RUN npm ci
COPY src ./src
COPY public ./public
RUN npm run build

# Runtime stage - Nginx
FROM nginx:1.27-alpine AS runtime
COPY --from=build /app/dist/ArquivoMate2.Ui/browser /usr/share/nginx/html
COPY docker-entrypoint.sh /docker-entrypoint.sh
COPY nginx.conf /etc/nginx/conf.d/default.conf
ENTRYPOINT ["/docker-entrypoint.sh"]
CMD ["nginx", "-g", "daemon off;"]
```

The `runtime-config.template.json` is included in the `public/` directory and ends up in `/usr/share/nginx/html/` during the runtime stage.

### 2. Container Startup - Entrypoint Script

When the container starts, `/docker-entrypoint.sh` executes:

```bash
#!/bin/sh
set -e

RUNTIME_CFG="/usr/share/nginx/html/runtime-config.json"
RUNTIME_TEMPLATE="/usr/share/nginx/html/runtime-config.template.json"

# 1. Read environment variables with defaults
VERSION_VALUE="${VERSION:-0.0.0-dev}"
API_BASE_URL_VALUE="${API_BASE_URL:-http://localhost:5000}"
OIDC_ISSUER_VALUE="${OIDC_ISSUER:-https://example-issuer.local/realms/app/}"
OIDC_CLIENT_ID_VALUE="${OIDC_CLIENT_ID:-spa-client}"
OIDC_SCOPE_VALUE="${OIDC_SCOPE:-openid profile email}"

# 2. Copy template to generate final config
if [ -f "$RUNTIME_TEMPLATE" ]; then
  cp "$RUNTIME_TEMPLATE" "$RUNTIME_CFG"
fi

# 3. Replace placeholders using sed
if [ -f "$RUNTIME_CFG" ]; then
  sed -i "s#__VERSION__#${VERSION_VALUE}#g" "$RUNTIME_CFG"
  sed -i "s#__API_BASE_URL__#${API_BASE_URL_VALUE}#g" "$RUNTIME_CFG"
  sed -i "s#__OIDC_ISSUER__#${OIDC_ISSUER_VALUE}#g" "$RUNTIME_CFG"
  sed -i "s#__OIDC_CLIENT_ID__#${OIDC_CLIENT_ID_VALUE}#g" "$RUNTIME_CFG"
  sed -i "s#__OIDC_SCOPE__#${OIDC_SCOPE_VALUE}#g" "$RUNTIME_CFG"
  
  echo "[entrypoint] Injected runtime-config: ..."
else
  echo "[entrypoint] runtime-config.json not found" >&2
fi

# 4. Start Nginx
exec "$@"
```

### 3. Angular Application Loading

The application loads the configuration in `src/app/app.config.ts`:

```typescript
// Load runtime configuration
const response = await fetch('runtime-config.json');
const runtimeConfig = await response.json();

// Use configuration values
const apiBaseUrl = runtimeConfig.apiBaseUrl;
const oidcConfig = {
  issuer: runtimeConfig.auth.issuer,
  clientId: runtimeConfig.auth.clientId,
  scope: runtimeConfig.auth.scope
};
```

## Using the Configuration

### Local Development

Edit `public/runtime-config.json` directly:

```json
{
  "apiBaseUrl": "http://localhost:5000",
  "version": "0.0.0-dev",
  "auth": {
    "issuer": "https://auth2.modellfrickler.online/application/o/arquivomate2/",
    "clientId": "egrVGZZH9GkuULNmnpux9Yr9neRhHXyaVup0pEUh",
    "scope": "openid profile email roles"
  }
}
```

### Docker Deployment

#### Docker CLI

Pass environment variables at runtime:

```bash
docker run \
  -e VERSION="1.2.3" \
  -e API_BASE_URL="https://api.example.com" \
  -e OIDC_ISSUER="https://auth.example.com/realms/arquivomate2/" \
  -e OIDC_CLIENT_ID="my-spa-client" \
  -e OIDC_SCOPE="openid profile email roles" \
  -p 8080:8080 \
  arquivomate2-ui:latest
```

#### Docker Compose

Define environment variables in `docker-compose.yml`:

```yaml
version: '3.8'
services:
  ui:
    build: ./src/ArquivoMate2.Ui
    ports:
      - "8080:8080"
    environment:
      VERSION: "1.2.3"
      API_BASE_URL: "https://api.example.com"
      OIDC_ISSUER: "https://auth.example.com/realms/arquivomate2/"
      OIDC_CLIENT_ID: "my-spa-client"
      OIDC_SCOPE: "openid profile email roles"
    depends_on:
      - api
```

#### Kubernetes / Helm

Define via ConfigMap or environment variables:

```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: ui-config
data:
  VERSION: "1.2.3"
  API_BASE_URL: "https://api.example.com"
  OIDC_ISSUER: "https://auth.example.com/realms/arquivomate2/"
  OIDC_CLIENT_ID: "my-spa-client"
  OIDC_SCOPE: "openid profile email roles"

---
apiVersion: apps/v1
kind: Deployment
metadata:
  name: ui
spec:
  template:
    spec:
      containers:
      - name: ui
        image: arquivomate2-ui:1.2.3
        envFrom:
        - configMapRef:
            name: ui-config
        ports:
        - containerPort: 8080
```

Or use Secrets for sensitive values:

```yaml
env:
- name: OIDC_CLIENT_ID
  valueFrom:
    secretKeyRef:
      name: oauth-secrets
      key: client-id
```

## Environment Variables Reference

| Variable | Default | Description | Example |
|----------|---------|-------------|---------|
| `VERSION` | `0.0.0-dev` | Application version | `1.2.3` |
| `API_BASE_URL` | `http://localhost:5000` | Backend API base URL | `https://api.example.com` |
| `OIDC_ISSUER` | `https://example-issuer.local/realms/app/` | OpenID Connect issuer | `https://auth.example.com/realms/app/` |
| `OIDC_CLIENT_ID` | `spa-client` | OAuth 2.0 Client ID | `egrVGZZH9GkuULNmnpux9Yr9neRhHXyaVup0pEUh` |
| `OIDC_SCOPE` | `openid profile email` | OAuth scopes | `openid profile email roles` |

## Fallback Behavior

If an environment variable is **not set**, the entrypoint script uses the default value specified in the script:

```bash
API_BASE_URL_VALUE="${API_BASE_URL:-http://localhost:5000}"
```

The syntax `${VAR:-DEFAULT}` means: "Use `$VAR` if set, otherwise use `DEFAULT`".

## Verification

After container startup, verify the configuration was injected correctly:

```bash
# From inside the container
cat /usr/share/nginx/html/runtime-config.json

# Expected output:
# {
#   "apiBaseUrl": "https://api.example.com",
#   "version": "1.2.3",
#   "auth": {
#     "issuer": "https://auth.example.com/realms/app/",
#     "clientId": "my-spa-client",
#     "scope": "openid profile email roles"
#   }
# }
```

Or check the browser console after loading the app to verify the config was loaded:

```javascript
// In browser console
fetch('runtime-config.json').then(r => r.json()).then(console.log);
```

## Troubleshooting

### Configuration values are not being replaced

1. **Check environment variables are set:**
   ```bash
   docker exec <container-id> env | grep OIDC_ISSUER
   ```

2. **Verify entrypoint script ran:**
   ```bash
   docker logs <container-id> | grep "entrypoint"
   ```

3. **Check the generated runtime-config.json:**
   ```bash
   docker exec <container-id> cat /usr/share/nginx/html/runtime-config.json
   ```

### API calls fail with wrong URL

- Verify `API_BASE_URL` environment variable is correctly set
- Check that the generated `runtime-config.json` contains the correct URL
- Ensure the backend API is accessible from the container network

### OIDC authentication fails

- Verify `OIDC_ISSUER` and `OIDC_CLIENT_ID` are correct
- Ensure the issuer is accessible from the browser (CORS may apply)
- Check browser console for detailed OAuth errors

## Best Practices

1. **Use Secrets for Sensitive Data**
   - Store `OIDC_CLIENT_ID` and similar values in Kubernetes Secrets or Docker Secrets
   - Never commit sensitive values to the repository

2. **Version the Image Separately**
   - Use Docker image tags for versioning (e.g., `arquivomate2-ui:1.2.3`)
   - Use the `VERSION` environment variable for the application version display

3. **Test Configuration Loading**
   - Always verify the config is correctly injected after deployment
   - Use health checks to ensure the app loaded correctly

4. **Document Environment Requirements**
   - Keep this guide updated when adding new configuration parameters
   - Document all required OIDC endpoints and client IDs

## Summary

| Stage | File | Action | Timing |
|-------|------|--------|--------|
| **Development** | `runtime-config.json` | Manual edit (local testing) | Development time |
| **Build** | `runtime-config.template.json` | Copied into Docker image | `docker build` |
| **Runtime** | `docker-entrypoint.sh` | Replaces placeholders from environment variables | `docker run` (container startup) |
| **Browser** | `runtime-config.json` (generated) | Loaded by Angular app | App initialization |

This architecture enables **true environment-agnostic deployments** where the same Docker image can be deployed across multiple environments by simply changing the environment variables passed to the container.
