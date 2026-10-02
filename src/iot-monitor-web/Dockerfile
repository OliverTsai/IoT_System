# syntax=docker/dockerfile:1

FROM node:24-alpine AS build
WORKDIR /app

COPY src/iot-monitor-web/package.json src/iot-monitor-web/package-lock.json ./
RUN npm ci

COPY src/iot-monitor-web/ ./
ARG VITE_API_BASE_URL=""
ENV VITE_API_BASE_URL=$VITE_API_BASE_URL
RUN npm run build

FROM alpine:3.22 AS certificates
RUN apk add --no-cache openssl \
    && mkdir -p /certificates \
    && openssl req -x509 -nodes -newkey rsa:2048 -days 825 \
        -keyout /certificates/localhost.key \
        -out /certificates/localhost.crt \
        -subj "/CN=localhost" \
        -addext "subjectAltName=DNS:localhost,IP:127.0.0.1"

FROM nginxinc/nginx-unprivileged:1.28-alpine AS final

ARG WEB_HTTPS_PORT=8443
COPY --chown=101:101 src/iot-monitor-web/nginx/default.conf.template /etc/nginx/conf.d/default.conf
RUN sed -i "s/__WEB_HTTPS_PORT__/$WEB_HTTPS_PORT/g" /etc/nginx/conf.d/default.conf
COPY --from=certificates --chown=101:101 /certificates/ /etc/nginx/certificates/
COPY --from=build --chown=101:101 /app/dist/ /usr/share/nginx/html/

USER 101:101
EXPOSE 8080 8443
