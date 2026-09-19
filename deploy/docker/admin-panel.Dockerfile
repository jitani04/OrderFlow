# syntax=docker/dockerfile:1
#
# Builds the React panel and serves it from nginx, which also proxies the API.
# Serving the app and the API from one origin means the browser makes same-origin
# requests, so CORS never enters the picture in a deployed environment.

FROM node:24-alpine AS build

WORKDIR /app

# Lockfile first so `npm ci` caches; editing a .tsx file does not reinstall node_modules.
COPY web/admin-panel/package.json web/admin-panel/package-lock.json ./
RUN npm ci

COPY web/admin-panel/ ./

# Empty means "use relative URLs", which nginx proxies. Vite inlines this at build time,
# so it is a build argument rather than a runtime environment variable.
ARG VITE_API_BASE_URL=""
ENV VITE_API_BASE_URL=${VITE_API_BASE_URL}

RUN npm run build

FROM nginx:1.29-alpine AS runtime

# templates/ rather than conf.d/: the image's entrypoint runs envsubst over anything here
# and writes the result into conf.d before nginx starts.
COPY deploy/docker/admin-panel.nginx.conf.template /etc/nginx/templates/default.conf.template
COPY --from=build /app/dist /usr/share/nginx/html

ENV API_UPSTREAM=api:8080 \
    NGINX_ENVSUBST_FILTER=^API_

EXPOSE 80
