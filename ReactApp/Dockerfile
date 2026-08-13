# Multi-stage build for the Vite/React SPA.
# Stage 1: install deps and produce the static bundle.
FROM node:22-alpine AS build
WORKDIR /app

# Install against the lockfile first so this layer caches unless deps change.
# Uses `npm install`, not `npm ci`: the lockfile is generated on Windows, which prunes
# Linux-only optional deps (e.g. @emnapi/runtime pulled in by @napi-rs/wasm-runtime).
# `npm ci` demands an exact lockfile match and hard-fails on those; `npm install`
# resolves the missing platform packages at build time.
COPY package*.json ./
RUN npm install --no-audit --no-fund

COPY . .
RUN npm run build

# Stage 2: serve the static bundle with nginx.
FROM nginx:1.27-alpine AS final
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html

# On container start, write the runtime API URL into config.js from the VITE_API_URL env var.
# The nginx base image runs /docker-entrypoint.d/*.sh before starting nginx. Authored via
# printf in the build layer so the script is guaranteed LF-terminated and executable
# regardless of the host that checked out the repo.
RUN printf '#!/bin/sh\necho "window.__APP_CONFIG__ = { apiUrl: \\"${VITE_API_URL:-}\\" };" > /usr/share/nginx/html/config.js\n' > /docker-entrypoint.d/40-app-config.sh \
    && chmod +x /docker-entrypoint.d/40-app-config.sh
# Listen on 5173 to match the Aspire endpoint's target port (ACA routes ingress and the
# startup probe there); the default nginx port 80 would leave the probe hitting nothing.
EXPOSE 5173
CMD ["nginx", "-g", "daemon off;"]
