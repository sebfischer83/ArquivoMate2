# Multi-stage Dockerfile for Angular (ArquivoMate2.Ui)
# Targets: build (Node) -> production (Nginx)
# Supports multi-arch (linux/amd64, linux/arm64) via buildx

# ---- Build stage -----------------------------------------------------------
FROM --platform=$BUILDPLATFORM node:20-alpine AS build

ARG TARGETPLATFORM
ARG BUILDPLATFORM
ARG NODE_ENV=production
ENV NODE_ENV=$NODE_ENV

WORKDIR /app

# Install deps separately for better layer caching
COPY package.json package-lock.json* npm-shrinkwrap.json* ./
RUN npm ci --no-audit --no-fund

# Copy sources
COPY angular.json tsconfig*.json ./
COPY src ./src
COPY public ./public

# Build (production by default)
RUN npm run build --if-present || npx ng build --configuration production

# ---- Runtime stage --------------------------------------------------------
FROM --platform=$TARGETPLATFORM nginx:1.27-alpine AS runtime

# Minimal security hardening
RUN addgroup -S app && adduser -S app -G app

# Copy built app (Angular CLI outputs to dist/<project-name>/browser by default for application builder)
COPY --from=build /app/dist/ArquivoMate2.Ui/browser /usr/share/nginx/html

# Custom Nginx config with SPA fallback
COPY nginx.conf /etc/nginx/conf.d/default.conf

# Entrypoint script for dynamic VERSION injection
COPY docker-entrypoint.sh /docker-entrypoint.sh
RUN chmod +x /docker-entrypoint.sh \
	&& chown -R app:app /usr/share/nginx/html /docker-entrypoint.sh

USER app

EXPOSE 8080
STOPSIGNAL SIGTERM

# Use 8080 for compatibility with some platforms that block 80
ENV PORT=8080

ENTRYPOINT ["/docker-entrypoint.sh"]
CMD ["nginx", "-g", "daemon off;"]
