# Señas: El mundo de las manos — Backend

REST API for the Colombian Sign Language (LSC) educational game. It manages users,
authentication, profiles, the catalog, progress, XP, unlocks and history. Unity is its
only client. The recognition AI runs inside Unity; the backend only receives the
**result** of each evaluation (never images or landmarks).

**Stack:** NestJS 12 · TypeScript (strict) · TypeORM 0.3 · PostgreSQL (Supabase) ·
JWT + rotating refresh tokens · argon2id · Swagger · Jest.

More documentation:

- [docs/integration-contract.md](docs/integration-contract.md) — for the Unity team (endpoints, errors, tokens, pending items).
- [docs/decisions.md](docs/decisions.md) — design decisions and their rationale.

## Requirements

- **Node.js 22 or later** (`.nvmrc` pins 22) and npm.
- A **Supabase** project (PostgreSQL).
- **Docker**, only for the e2e tests.

## Installation

```bash
cd backend
npm ci
cp .env.example .env     # on Windows: copy .env.example .env
# edit .env (see below)
npm run start:dev
```

On startup the app **creates/updates the tables** in Supabase and **loads the catalog**
(4 levels, 81 signs). The API listens on `http://localhost:3000/api/v1` and Swagger is
at `http://localhost:3000/docs`.

> If port 3000 is taken by another program, change `PORT` in `.env`.

## Environment variables

They are validated at startup: if one is missing or invalid, the app does not start and
says which. Example values are in [.env.example](.env.example). **`.env` is in
`.gitignore`; never commit it.**

| Variable | Default | Description |
| --- | --- | --- |
| `NODE_ENV` | `development` | `development`, `production` or `test` (with `test` `.env` is not read) |
| `PORT` | `3000` | HTTP port |
| `API_PREFIX` | `api/v1` | Route prefix |
| `SWAGGER_ENABLED` | `true` | Enables `/docs` |
| `CORS_ORIGINS` | *(empty)* | Comma-separated list; empty = CORS disabled (the client is a desktop app) |
| `DB_URL` | — | `postgresql://host:port/database` **without** user, password or `sslmode` |
| `DB_USER` | — | Pooler user: `postgres.<project-ref>` |
| `DB_PASSWORD` | — | Password in plain text |
| `DB_SSL` | `true` | SSL (`rejectUnauthorized: false`) |
| `DB_POOL_MAX` | `5` | Maximum connections (the free pooler is limited) |
| `DB_SYNCHRONIZE` | `true` | Creates/updates tables from the entities |
| `DB_SEED_ON_START` | `true` | Loads the catalog on startup (idempotent) |
| `JWT_ACCESS_SECRET` | — | **≥ 32 characters** |
| `JWT_ACCESS_TTL` | `15m` | Access token lifetime (`30s`, `15m`, `2h`, `1d`) |
| `REFRESH_TOKEN_TTL_DAYS` | `30` | Refresh token lifetime |
| `SCORE_PASS_THRESHOLD` | `60` | Minimum precision to pass (**0–100 scale**) |
| `SCORE_TWO_STARS_THRESHOLD` | `74` | 2-star threshold |
| `SCORE_THREE_STARS_THRESHOLD` | `88` | 3-star threshold |
| `XP_PER_STAR` | `10` | XP per star (max. 10; the cap per sign is 3×) |
| `AVATAR_IDS` | `luna,nube,rio,sol` | Valid avatars (match Unity's `AvatarCatalog`) |
| `THROTTLE_TTL_MS` | `60000` | Rate-limit window |
| `THROTTLE_LIMIT` | `100` | Requests per window, per route and IP |
| `THROTTLE_AUTH_LIMIT` | `5` | Strict limit for register/login/refresh |

It must hold that `0 < SCORE_PASS_THRESHOLD < SCORE_TWO_STARS_THRESHOLD < SCORE_THREE_STARS_THRESHOLD ≤ 100`.
Precision uses the same 0–100 scale Unity's AI produces (`Similarity * 100`) and is stored as `numeric(5,2)`.

Generate `JWT_ACCESS_SECRET`:

```bash
node -e "console.log(require('crypto').randomBytes(48).toString('base64url'))"
```

## Setting up Supabase step by step

1. Create a project at [supabase.com](https://supabase.com) and save the database password.
2. In the project: **Connect → Session pooler** (IPv4, port **5432**, session mode).
   It is the one compatible with TypeORM and with Render.
3. Copy the data into `.env`, **separately**:
   - `DB_URL` = the connection string **without credentials**, e.g.
     `postgresql://aws-0-us-east-1.pooler.supabase.com:5432/postgres`
   - `DB_USER` = `postgres.<project-ref>` (the user the pooler shows)
   - `DB_PASSWORD` = the password **as-is**: no brackets and **not URL-encoded**, even if it
     has special characters (`! * @ #`…). It has its own variable precisely so you do not
     have to escape it.
   - Do not add `?sslmode=...` to the URL: SSL is controlled by `DB_SSL`.
4. Start the app (`npm run start:dev`). With `DB_SYNCHRONIZE=true` it creates the 8 tables
   (`users`, `profiles`, `levels`, `signs`, `level_signs`, `sign_progress`,
   `evaluations`, `sessions`) and with `DB_SEED_ON_START=true` it loads the catalog.
   The log shows:
   ```
   Security applied (RLS + REVOKE) to 8 tables.
   Catalog verified: 4 levels and 81 active signs.
   ```
5. In the **Table Editor** check that the tables exist and that `levels` has 4 rows and
   `signs` has 81. Each table shows RLS enabled with no policies: this is intentional (the
   public Supabase API cannot touch them; only this backend, using the `postgres` user).

Restarting the app duplicates nothing: the seed is an `upsert`. There is no migrations
folder or scripts.

## Scripts

| Command | What it does |
| --- | --- |
| `npm run start:dev` | Development with reload |
| `npm run build` | Compiles to `dist/` |
| `npm run start:prod` | Runs `dist/main` |
| `npm run lint` | oxlint |
| `npm run format` | Prettier |
| `npm test` | Unit tests |
| `npm run test:cov` | Unit tests with coverage (requires ≥ 80 % in the evaluations, progress and auth services) |
| `npm run db:test:up` / `db:test:down` | Starts / removes the test PostgreSQL (Docker) |
| `npm run test:e2e` | e2e tests (need `db:test:up`) |

## Tests

- **Unit** (`npm test`): scoring, XP, unlocks, authentication (refresh token rotation and
  reuse), and the services with an in-memory `EntityManager` and the real catalog. They need
  no database.
- **E2E** (`npm run test:e2e`, including the catalog seed reconciliation): start the whole app against a **PostgreSQL 16 in Docker**
  (port 54329) with the real `synchronize` and seed. They cover register → login → levels →
  passed attempt → unlock → idempotent retry → refresh → logout → revoked token, plus
  concurrency and throttling.

```bash
npm run db:test:up
npm run test:e2e
npm run db:test:down
```

With `NODE_ENV=test` the app **does not read `backend/.env`**, so the tests cannot touch
Supabase even if you have the file configured.

> Nest 12 packages are ESM; the test scripts already include `--experimental-vm-modules`.
> If you run `npx jest` directly, add it by hand.

## Structure

```
src/
  main.ts · app.module.ts · app.setup.ts   # startup and common configuration
  config/        # env validation, scoring rules, TypeORM options
  common/        # ApiError, exception filter, decorators (@Public, @CurrentUser)
  database/      # DatabaseBootstrapService (RLS + seed) and catalog.json
  auth/ users/ profiles/ signs/ levels/ evaluations/ progress/ health/
test/            # e2e and test utilities
docs/            # integration contract and decisions
```

## Database schema

| Table | Purpose |
| --- | --- |
| `users` | Accounts (`email`, `password_hash`, `role`, `active`, …) |
| `profiles` | Display name, avatar, preferences and `total_xp` (1:1 with users) |
| `levels` | The 4 levels (`code`, `position`, `required_xp`) |
| `signs` | The 81 signs (`code`, `type`) |
| `level_signs` | Which sign sits at which `position` of which level |
| `sign_progress` | Per player and sign: attempts, best precision (0–100)/stars, `earned_xp`, `passed` |
| `evaluations` | Immutable attempt history (idempotent per `user_id` + `attempt_id`) |
| `sessions` | Auth sessions (hashed refresh tokens) |

Enums: `user_role` (`PLAYER`, `ADMIN`) and `sign_type` (`LETTER`, `NUMBER`, `MONTH`, `WORD`, `PHRASE`).

## Deployment (Render)

[render.yaml](render.yaml) defines a Node web service in the **Virginia (us-east)** region,
close to Supabase `us-east-1`. Build `npm ci && npm run build`, start `npm run start:prod`,
health check `/api/v1/health`.

1. In Render: **New → Blueprint**, pick the repository and, in the options, set the
   blueprint path to `backend/render.yaml` (the file lives inside `backend/`, not at the root).
2. Render asks for the secrets marked `sync: false`: `DB_URL`, `DB_USER`, `DB_PASSWORD`
   and `JWT_ACCESS_SECRET`. Copy them from your local `.env` (the production JWT secret must
   be **different** from the development one).
3. On startup the service synchronizes the tables by itself.

**Before deploying a change to the entities:** with `synchronize`, TypeORM **drops** a
column (and its data) if a property is renamed or removed. Review the diff of
`src/**/entities/*.entity.ts` before every deployment:

- Adding a `nullable` property or one with a `default` is safe.
- Renaming, changing the type of, or removing a property that holds real data is **not**:
  take a backup first (Supabase → Database → Backups) or apply the change by hand in SQL,
  and then deploy.
- To freeze the schema in an environment, set `DB_SYNCHRONIZE=false`.

Notes: Render's free plan "sleeps" the service after a while without traffic and the first
request can take ~50 s. `SWAGGER_ENABLED` is `false` in production.

## Security

- Passwords use **argon2id**; only the **SHA-256 hash** of refresh tokens is stored.
- 15-minute HS256 JWT access token; the guard checks in the database that the session is
  neither revoked nor expired (logout is immediate).
- Refresh tokens with rotation and reuse detection.
- RLS enabled and `REVOKE` from `anon`/`authenticated` on every table.
- `helmet`, strict validation (`whitelist` + `forbidNonWhitelisted`), rate limiting, 500
  errors without internal details, and logs without passwords, tokens or hashes.
- No secret in git: `.env` is in `.gitignore`; only `.env.example` is versioned.
