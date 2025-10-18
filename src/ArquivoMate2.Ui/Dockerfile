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

# Ensure runtime-config.json is not baked into the dist output. We rely on
# runtime-config.template.json + the entrypoint to produce runtime-config.json
# at container startup. Remove any accidentally present runtime-config.json.
RUN rm -f /app/dist/ArquivoMate2.Ui/runtime-config.json || true

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
# Convert entrypoint to Unix line endings inside the runtime image to avoid CRLF issues
# (dos2unix is small; install, convert, then remove)
RUN apk add --no-cache dos2unix && \
	dos2unix /docker-entrypoint.sh || true && \
	apk del dos2unix || true
# Only set the executable bit; ownership is already correct
RUN chmod +x /docker-entrypoint.sh

# Ensure the html directory itself is writable by the 'app' user so the entrypoint
# can create runtime-config.json at container start. We avoid recursive chown to
# keep the copy operation fast (files are already owned by app via --chown above).
RUN chown app:app /usr/share/nginx/html || true

EXPOSE 80
STOPSIGNAL SIGTERM

# Use 80 as the container HTTP port (nginx default). The host can map any external port
# to container port 80 when running the container (docker run -p HOST:80 ...).
ENV PORT=80

ENTRYPOINT ["sh", "/docker-entrypoint.sh"]
CMD ["nginx", "-g", "daemon off;"]
