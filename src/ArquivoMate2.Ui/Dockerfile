# Multi-stage Dockerfile for Angular (ArquivoMate2.Ui)
# Targets: build (Node) -> production (Nginx)
# Supports multi-arch (linux/amd64, linux/arm64) via buildx

# ---- Build stage -----------------------------------------------------------
FROM --platform=$BUILDPLATFORM node:22-alpine AS build

ARG TARGETPLATFORM
ARG BUILDPLATFORM
ARG NODE_ENV=production
## Do NOT set NODE_ENV as an environment variable here globally -
## if NODE_ENV=production is exported npm will skip devDependencies
## which include the Angular build packages. We'll install deps
## with NODE_ENV=development to ensure devDependencies are present
## for the build, and keep production settings for the runtime stage.

WORKDIR /app

# Install deps separately for better layer caching
# Copy package files
COPY package.json package-lock.json* npm-shrinkwrap.json* ./
# Install including devDependencies (angular build tooling lives in devDependencies).
# We explicitly set NODE_ENV=development here so npm will install devDependencies
# which contain the Angular build tooling (@angular/build, @angular/cli, etc.).
RUN NODE_ENV=development npm ci --prefer-offline --no-audit --no-fund
# Prefer local build tooling (npx will use project node_modules). Installing
# global CLIs is optional but harmless; keep it as a fallback.
RUN npm install -g @angular/cli@^20 || true

# Copy sources
COPY angular.json tsconfig*.json ./
COPY . .


# Build (production configuration)
# Use the local ng (from node_modules) via npx to ensure project tools are used
RUN npx ng build --configuration production

# Pre-compress static assets: create .br and .gz alongside original files so nginx can
# serve pre-compressed files when available. Install brotli CLI in the build image.
RUN apk add --no-cache brotli && \
		echo "Precompressing JS/CSS/HTML/SVG/JSON files with brotli and gzip..." && \
		find /app/dist/ArquivoMate2.Ui -type f \( -iname '*.js' -o -iname '*.css' -o -iname '*.html' -o -iname '*.svg' -o -iname '*.json' \) -print0 \
			| xargs -0 -n1 -P4 sh -c 'f="$0"; brotli -q 11 -f -o "${f}.br" "${f}" || echo "brotli failed for $f"; gzip -9 -c "${f}" > "${f}.gz" || echo "gzip failed for $f"'

# ---- Runtime stage --------------------------------------------------------
FROM --platform=$TARGETPLATFORM nginx:1.27-alpine AS runtime

# Minimal security hardening
RUN addgroup -S app && adduser -S app -G app

# Copy built app (Angular CLI outputs to dist/<project-name>/browser by default for application builder)
COPY --from=build --chown=app:app /app/dist/ArquivoMate2.Ui/browser /usr/share/nginx/html

# Custom Nginx config with SPA fallback
COPY --chown=app:app nginx.conf /etc/nginx/conf.d/default.conf

# Entrypoint script for dynamic VERSION injection (copy as app user)
COPY --chown=app:app docker-entrypoint.sh /docker-entrypoint.sh
# Only set the executable bit; ownership is already correct
RUN chmod +x /docker-entrypoint.sh

USER app

EXPOSE 8080
STOPSIGNAL SIGTERM

# Use 8080 for compatibility with some platforms that block 80
ENV PORT=8080

ENTRYPOINT ["/docker-entrypoint.sh"]
CMD ["nginx", "-g", "daemon off;"]
