# syntax=docker/dockerfile:1
# Backend Señas (NestJS + TypeORM -> Supabase PostgreSQL). Build context: backend/
# No secrets here: all configuration comes from the environment (Render > Environment).

# ---- deps: production dependencies only ----
FROM node:22-slim AS prod-deps
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci --omit=dev && npm cache clean --force

# ---- build: full install + nest build ----
FROM node:22-slim AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci
COPY nest-cli.json tsconfig.json tsconfig.build.json ./
COPY src ./src
RUN npm run build

# ---- runtime ----
FROM node:22-slim AS runtime
ENV NODE_ENV=production
WORKDIR /app
COPY --from=prod-deps --chown=node:node /app/node_modules ./node_modules
COPY --from=build --chown=node:node /app/dist ./dist
COPY --chown=node:node package.json ./
USER node
# Render injects PORT (default 10000); the app reads it from process.env.PORT.
EXPOSE 10000
CMD ["node", "dist/main"]
