# syntax=docker/dockerfile:1
# ---------------------------------------------------------------------------
# TallyG Tax Web (Next.js 14 App Router) — multi-stage build
#   deps  : install node_modules from the lockfile (cached layer)
#   build : compile the production Next.js bundle (`next build`)
#   final : run `next start` on :3000
# Build context is the frontend/ folder (see infra/docker-compose.yml).
#
# NEXT_PUBLIC_API_URL is a build-time public env (inlined into the client bundle
# by Next.js) AND a runtime env, so it is passed as a build ARG and re-exported.
# ---------------------------------------------------------------------------

# ---- deps ------------------------------------------------------------------
FROM node:20-alpine AS deps
WORKDIR /app
# Copy only manifests so dependency install is cached independently of source.
COPY package.json package-lock.json* ./
# Use a clean, reproducible install when a lockfile is present; fall back otherwise.
RUN if [ -f package-lock.json ]; then npm ci; else npm install; fi

# ---- build -----------------------------------------------------------------
FROM node:20-alpine AS build
WORKDIR /app
ARG NEXT_PUBLIC_API_URL=http://localhost:5080/api/v1
ENV NEXT_PUBLIC_API_URL=$NEXT_PUBLIC_API_URL
ENV NEXT_TELEMETRY_DISABLED=1
COPY --from=deps /app/node_modules ./node_modules
COPY . .
RUN npm run build

# ---- runtime ---------------------------------------------------------------
FROM node:20-alpine AS final
WORKDIR /app
ENV NODE_ENV=production
ENV NEXT_TELEMETRY_DISABLED=1
ENV PORT=3000
ARG NEXT_PUBLIC_API_URL=http://localhost:5080/api/v1
ENV NEXT_PUBLIC_API_URL=$NEXT_PUBLIC_API_URL

# Copy the fully built app (app source + .next bundle + node_modules) and start
# the Next.js production server. Copying the whole /app tree keeps this robust
# regardless of whether the project ships a public/ dir or a next.config file.
COPY --from=build /app ./

EXPOSE 3000
CMD ["npm", "run", "start"]
