# Build context: backend/PredictionUpdater
#   docker build -t predictionupdater backend/PredictionUpdater
#
# Run-once job: the container starts, runs one poll -> store -> notify cycle, and exits.
# The Kubernetes CronJob decides how often it starts.

# ---- Stage 1: build (dev dependencies + TypeScript compiler, discarded after the build) ----
FROM node:22-alpine AS build
WORKDIR /app

# Install first, using only the package files, so this layer stays cached until dependencies change.
# --ignore-scripts skips the postinstall `prisma generate`: the schema isn't copied in yet.
COPY package.json package-lock.json ./
RUN npm ci --ignore-scripts

COPY prisma ./prisma
COPY prisma.config.ts tsconfig.json tsconfig.build.json ./
COPY src ./src

# Generate the Prisma client into src/generated (does not connect to a database), then compile to dist/.
RUN npx prisma generate && npm run build

# ---- Stage 2: runtime (production dependencies + compiled JS only) ----
FROM node:22-alpine AS runtime
WORKDIR /app

# JSON logs instead of pino-pretty (a dev dependency that isn't installed here).
ENV NODE_ENV=production

COPY package.json package-lock.json ./
RUN npm ci --omit=dev --ignore-scripts && npm cache clean --force

COPY --from=build /app/dist ./dist

# The node image ships an unprivileged "node" user; the job needs no root rights.
USER node
CMD ["node", "dist/main.js"]
