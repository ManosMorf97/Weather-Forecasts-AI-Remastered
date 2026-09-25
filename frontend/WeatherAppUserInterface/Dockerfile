# Build context: frontend/WeatherAppUserInterface
#   docker build -t weatherappuserinterface \
#     --build-arg YOUR_APPWRITE_ENDPOINT=... --build-arg YOUR_APPWRITE_PROJECT_ID=... \
#     frontend/WeatherAppUserInterface
#
# Vite bakes env vars into the JS at BUILD time (the browser has no env vars), so the Appwrite
# settings are build args, not runtime env. They are public anyway - every visitor's browser sees them.
# YOUR_API_URL is left unset: API calls go to /api on the same address (see Deployment/ingress).

# ---- Stage 1: build (Node + dev dependencies, discarded after the build) ----
FROM node:22-alpine AS build
WORKDIR /app

# Install first, using only the package files, so this layer stays cached until dependencies change.
COPY package.json package-lock.json ./
RUN npm ci

COPY . .

ARG YOUR_APPWRITE_ENDPOINT
ARG YOUR_APPWRITE_PROJECT_ID
# Fail the build (not the user's browser) if the Appwrite settings are missing.
RUN test -n "$YOUR_APPWRITE_ENDPOINT" && test -n "$YOUR_APPWRITE_PROJECT_ID" \
    || (echo "YOUR_APPWRITE_ENDPOINT and YOUR_APPWRITE_PROJECT_ID build args are required" && exit 1)
RUN npm run build

# ---- Stage 2: runtime (nginx serving static files) ----
# The unprivileged nginx image runs as a non-root user and listens on 8080.
FROM nginxinc/nginx-unprivileged:stable-alpine AS runtime

COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html

EXPOSE 8080
