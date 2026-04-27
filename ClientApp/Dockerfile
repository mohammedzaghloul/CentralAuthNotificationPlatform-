FROM node:22-alpine AS build
WORKDIR /app

COPY package*.json ./
RUN npm ci

COPY . .
RUN npm run build

FROM nginx:1.27-alpine AS final
COPY nginx.conf.template /etc/nginx/templates/default.conf.template
COPY --from=build /app/dist/central-auth-dashboard /usr/share/nginx/html

CMD ["/bin/sh", "-c", "envsubst '$PORT $API_BASE_URL' < /etc/nginx/templates/default.conf.template > /etc/nginx/conf.d/default.conf && printf 'window.__AUTH_HUB_CONFIG__ = { apiBaseUrl: \"%s\" };\\n' \"$API_BASE_URL\" > /usr/share/nginx/html/runtime-config.js && nginx -g 'daemon off;'"]
