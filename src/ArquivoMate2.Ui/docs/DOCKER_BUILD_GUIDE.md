# Docker Build Guide

## Quick Start

### Build the Docker Image

```bash
# Navigate to the project root
cd src/ArquivoMate2.Ui

# Build the image (will take ~3-5 minutes on first build)
docker build -t arquivomate2-ui:latest .

# Or with a specific tag
docker build -t arquivomate2-ui:1.0.0 .
```

### Run the Container

```bash
# Basic run (uses default config)
docker run -p 8080:8080 arquivomate2-ui:latest

# With environment variables (production example)
docker run \
  -e VERSION="1.0.0" \
  -e API_BASE_URL="https://api.example.com" \
  -e OIDC_ISSUER="https://auth.example.com/realms/app/" \
  -e OIDC_CLIENT_ID="my-spa-client" \
  -e OIDC_SCOPE="openid profile email roles" \
  -p 8080:8080 \
  arquivomate2-ui:latest
```

Then open http://localhost:8080 in your browser.

## What Should Work

✅ **Docker Build Process:**
- Multi-stage build (Node → Nginx)
- Node 20 Alpine for building
- Nginx 1.27 Alpine for runtime
- All dependencies installed
- Angular production build compiled
- Entrypoint script copied and executable

✅ **Runtime:**
- Nginx starts and serves static files
- SPA routing (fallback to index.html)
- Static asset caching (js, css, fonts)
- Runtime config injection via environment variables
- Runtime config NOT cached (no-cache headers)

✅ **Configuration Injection:**
- Template file copied to runtime image
- Entrypoint script replaces placeholders with env vars
- Final config.json generated at container startup
- Nginx serves updated config

## Build Checklist

Use this checklist to verify everything is in place:

```bash
# 1. Verify all necessary files exist
[ -f "Dockerfile" ] && echo "✓ Dockerfile exists" || echo "✗ Dockerfile missing"
[ -f "docker-entrypoint.sh" ] && echo "✓ entrypoint.sh exists" || echo "✗ entrypoint.sh missing"
[ -f "nginx.conf" ] && echo "✓ nginx.conf exists" || echo "✗ nginx.conf missing"
[ -f "package.json" ] && echo "✓ package.json exists" || echo "✗ package.json missing"
[ -f "angular.json" ] && echo "✓ angular.json exists" || echo "✗ angular.json missing"
[ -f "public/runtime-config.template.json" ] && echo "✓ runtime template exists" || echo "✗ runtime template missing"

# 2. Verify entrypoint script is executable
file docker-entrypoint.sh | grep -q "shell script" && echo "✓ entrypoint is shell script" || echo "✗ entrypoint format issue"

# 3. Check package.json has build script
grep -q '"build"' package.json && echo "✓ build script exists" || echo "✗ build script missing"

# 4. Verify Dockerfile syntax
docker build --dry-run . >/dev/null 2>&1 && echo "✓ Dockerfile syntax OK" || echo "✗ Dockerfile has syntax errors"
```

## Known Issues & Solutions

### Issue 1: "npm ci: command not found"
**Error:** Build fails with `npm ci: command not found`

**Solution:**
- Ensure Node 20+ is in the Dockerfile
- The `FROM node:20-alpine` line should be present
- Check: `docker build --progress=plain . 2>&1 | grep "npm ci"`

### Issue 2: "Angular build fails"
**Error:** Build stops at `npm run build`

**Solution:**
1. Check Node version: `docker run node:20-alpine node --version` (should be 20.x)
2. Verify dependencies install: Check if `npm ci` succeeds in the build logs
3. Check for TypeScript errors: Run `ng build` locally first
   ```bash
   npm install
   npm run build
   ```

### Issue 3: "dist folder not found"
**Error:** `COPY --from=build /app/dist/ArquivoMate2.Ui/browser` fails

**Possible Causes:**
- Angular build failed silently
- Output directory mismatch with `angular.json`

**Solution:**
1. Check `angular.json` for correct output path:
   ```json
   {
     "projects": {
       "ArquivoMate2.Ui": {
         "architect": {
           "build": {
             "options": {
               "outputPath": "dist/ArquivoMate2.Ui/browser"
             }
           }
         }
       }
     }
   }
   ```

2. Verify locally:
   ```bash
   npm run build
   ls -la dist/ArquivoMate2.Ui/browser/
   ```

### Issue 4: "entrypoint script: permission denied"
**Error:** `exec docker-entrypoint.sh: permission denied`

**Solution:**
The Dockerfile should have:
```dockerfile
RUN chmod +x /docker-entrypoint.sh
```

Verify it's in the `runtime` stage.

### Issue 5: "404 Not Found" for runtime-config.json
**Error:** Browser console: `Failed to fetch runtime-config.json`

**Possible Causes:**
- Template file not in `public/` directory
- Nginx not serving the file

**Solution:**
1. Verify template exists:
   ```bash
   docker exec <container-id> ls -la /usr/share/nginx/html/runtime-config.*
   ```

2. Check Nginx can read it:
   ```bash
   docker exec <container-id> cat /usr/share/nginx/html/runtime-config.json
   ```

3. Verify Nginx config allows access:
   ```bash
   docker exec <container-id> nginx -T 2>&1 | grep runtime
   ```

### Issue 6: "Placeholders not replaced"
**Error:** runtime-config.json still contains `__PLACEHOLDER__` values

**Possible Causes:**
- Environment variables not set
- sed command syntax issue (special characters in values)
- Entrypoint script didn't run

**Solution:**
1. Check environment variables were passed:
   ```bash
   docker exec <container-id> env | grep OIDC_ISSUER
   ```

2. Check entrypoint logs:
   ```bash
   docker logs <container-id> | grep entrypoint
   ```

3. Verify the config:
   ```bash
   docker exec <container-id> cat /usr/share/nginx/html/runtime-config.json
   ```

4. If sed has issues with special characters, the entrypoint uses `#` as delimiter:
   ```bash
   sed -i "s#__API_BASE_URL__#${API_BASE_URL_VALUE}#g"
   ```
   This allows `/` in URLs without escaping.

## Testing the Build

### 1. Local Build Test

```bash
# Build with build output
docker build --progress=plain -t arquivomate2-ui:test .

# Check image size
docker images arquivomate2-ui

# Expected: ~50-100MB for final image
```

### 2. Container Runtime Test

```bash
# Start container
docker run -d --name test-ui -p 8080:8080 arquivomate2-ui:test

# Wait for startup
sleep 2

# Check container is running
docker ps | grep test-ui

# Check logs
docker logs test-ui

# Test HTTP endpoint
curl http://localhost:8080 | head -20

# Check configuration injection
curl http://localhost:8080/runtime-config.json | jq '.'

# Stop container
docker stop test-ui
docker rm test-ui
```

### 3. Full Integration Test

```bash
# Build
docker build -t arquivomate2-ui:test .

# Run with specific config
docker run -d \
  --name test-ui \
  -e VERSION="1.0.0" \
  -e API_BASE_URL="https://api.test.local" \
  -e OIDC_ISSUER="https://auth.test.local/" \
  -e OIDC_CLIENT_ID="test-client" \
  -e OIDC_SCOPE="openid profile" \
  -p 8080:8080 \
  arquivomate2-ui:test

# Wait
sleep 2

# Verify config was injected
docker exec test-ui cat /usr/share/nginx/html/runtime-config.json

# Expected output:
# {
#   "apiBaseUrl": "https://api.test.local",
#   "version": "1.0.0",
#   "auth": {
#     "issuer": "https://auth.test.local/",
#     "clientId": "test-client",
#     "scope": "openid profile"
#   }
# }

# Cleanup
docker stop test-ui && docker rm test-ui
```

## Build Performance

### First Build (Cold Cache)
- **Typical duration:** 3-5 minutes
- **Bottleneck:** `npm ci` + Angular build compilation
- **Download:** ~200MB+ dependencies

### Subsequent Builds (Warm Cache)
- **Typical duration:** 30-60 seconds
- **Note:** If only Angular sources changed, Node layer is cached
- **Tip:** Use `.dockerignore` to exclude unnecessary files

### Optimize Build Time

1. **Use BuildKit** (faster, better caching):
   ```bash
   DOCKER_BUILDKIT=1 docker build -t arquivomate2-ui:latest .
   ```

2. **Multi-build optimization:**
   ```bash
   docker build --cache-from arquivomate2-ui:latest -t arquivomate2-ui:latest .
   ```

3. **Node modules caching** (if using advanced Docker setup):
   - Use `RUN npm ci --cache=/build-cache` (requires BuildKit)

## Multi-Architecture Builds

Build for multiple architectures (AMD64 + ARM64):

```bash
# Enable BuildKit
export DOCKER_BUILDKIT=1

# Build for multiple platforms
docker buildx build \
  --platform linux/amd64,linux/arm64 \
  -t arquivomate2-ui:latest \
  --push \
  .

# Or build locally without push
docker buildx build \
  --platform linux/amd64,linux/arm64 \
  -t arquivomate2-ui:latest \
  --load \
  .
```

## Deployment Checklist

Before pushing to production:

- [ ] Docker image builds without errors
- [ ] Image runs with default environment
- [ ] Runtime config injection works
- [ ] Nginx serves static files correctly
- [ ] SPA routing works (no 404 for routes)
- [ ] Static assets are cached (check headers)
- [ ] Runtime config is NOT cached
- [ ] Environment variables are respected
- [ ] Image is security-scanned (if using registry scanning)
- [ ] Image size is reasonable (~50-100MB)

## CI/CD Integration

### GitHub Actions Example

```yaml
name: Build and Push UI

on:
  push:
    branches:
      - main
    paths:
      - 'src/ArquivoMate2.Ui/**'

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      
      - name: Set up Docker Buildx
        uses: docker/setup-buildx-action@v2
      
      - name: Build Docker image
        uses: docker/build-push-action@v4
        with:
          context: ./src/ArquivoMate2.Ui
          push: false
          tags: arquivomate2-ui:latest
          cache-from: type=gha
          cache-to: type=gha,mode=max
```

### Docker Compose for Local Testing

```yaml
# docker-compose.test.yml
version: '3.8'

services:
  ui:
    build:
      context: ./src/ArquivoMate2.Ui
      dockerfile: Dockerfile
    ports:
      - "8080:8080"
    environment:
      VERSION: "1.0.0"
      API_BASE_URL: "http://host.docker.internal:5000"
      OIDC_ISSUER: "https://auth.example.com/"
      OIDC_CLIENT_ID: "local-client"
      OIDC_SCOPE: "openid profile email"
```

Run with:
```bash
docker-compose -f docker-compose.test.yml up
```

## Summary

**Yes, the Docker build should work!** All necessary files are in place:

✓ `Dockerfile` - Multi-stage build configured  
✓ `docker-entrypoint.sh` - Entrypoint for env var injection  
✓ `nginx.conf` - Nginx SPA configuration  
✓ `package.json` - Dependencies and build script  
✓ `angular.json` - Angular build config  
✓ `public/runtime-config.template.json` - Template with placeholders  

The workflow is:
1. **Build:** Angular app + dependencies compile in Node container
2. **Package:** Built app + Nginx + entrypoint → Final image
3. **Run:** Entrypoint injects env vars → Nginx serves app
4. **Browse:** Angular loads runtime config → Uses injected values

Try building with: `docker build -t arquivomate2-ui:latest .`
