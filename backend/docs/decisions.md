# Design decisions

A record of the decisions made while building the backend and why. If you change one,
update this file.

## Stack and versions

| Decision | Rationale |
| --- | --- |
| **NestJS 12** (not the 11 from the brief) | It is what `nest new` installs today; all the `@nestjs/*` modules (config, typeorm, jwt, passport, swagger, throttler, terminus) publish 12 and are compatible with each other. |
| **TypeORM 0.3.31** (pinned; not 1.x) | `latest` on npm is already 1.1.x, but the requirement is 0.3.x and `@nestjs/typeorm` supports it. 1.x changes APIs; there is no reason to take that risk. |
| **oxlint + Prettier** instead of ESLint | The Nest 12 CLI generates oxlint (`npm run lint`). It is faster and does the same job; it was kept. |
| **TypeScript 6** with `strict: true` | Comes from the CLI. `strictPropertyInitialization` is `false` because entities and DTOs are filled by decorators/`class-transformer`, not in the constructor. |
| **Node ≥ 22** (`engines`, `.nvmrc`) | Requested LTS. Nest 12 is pure ESM: the build stays CommonJS and Node 22+ loads it with `require(esm)`. |
| **Jest with `--experimental-vm-modules`** | Nest 12 packages are ESM; Jest only loads them with that flag. It is included in `npm test`, `npm run test:cov` and `npm run test:e2e`; nothing to configure. |
| **No Swagger CLI plugin** | It generated absolute paths containing the `ñ` of the project folder and broke startup. DTOs carry explicit `@ApiProperty`. |
| **No `@nestjs/observe` or `@nestjs/mau`** | They came in the CLI template with sample keys (`YOUR_APP_KEY`); they are not needed and sent telemetry to an external service. |
| **Everything in English** | Code, comments, tables, columns, enums, endpoints, JSON fields, error codes and documentation. The only Spanish left is content that must match Unity: avatar ids (`luna`, `nube`, `rio`, `sol`) and the game's name. |

## Database

- **`synchronize: true`, no migrations.** It is the requirement. Known risk: if a property
  of an entity holding data is **renamed or removed**, TypeORM drops the column (and its
  data) or creates a new empty one. Review such a change before deploying (see "Deployment"
  in the README). Adding properties is safe if they are `nullable` or have a `default`.
  Also: **tables removed from the entities are never dropped** by `synchronize`; when the
  schema was renamed to English the old Spanish tables had to be removed by hand.
- **Connection with separate fields** (`host`, `port`, `database`, `username`, `password`)
  and never `url`: user and password cannot be overridden by what the URL carries.
  `DB_URL` is rejected at startup if it contains a user, password or `sslmode`.
- **`ssl: { rejectUnauthorized: false }`** when `DB_SSL=true`. The Supabase pooler does not
  provide a certificate chain that `pg` validates by default; the connection is encrypted
  but the server identity is not verified.
- **`uuidExtension: 'pgcrypto'`** → keys use `gen_random_uuid()` (the requested default)
  instead of `uuid_generate_v4()` from `uuid-ossp`.
- **`logging: false` in TypeORM.** Its error log prints the query parameters (password
  hashes, tokens). Errors are logged by our filter: only name and message.
- **No functional indexes.** The email is normalized to lowercase in the application and
  has a plain unique index (`ux_users_email`).
- **Index `(user_id, created_at)` without `DESC`.** TypeORM's `@Index` decorator does not
  allow descending order; Postgres scans the index backwards with the same performance for
  the history (most recent first).
- **History is immutable by convention**, not by trigger: the code only inserts into
  `evaluations`. A trigger blocking `DELETE` would prevent `ON DELETE CASCADE` when a user
  is deleted.
- **Extra column `evaluations.response` (jsonb).** It stores the response delivered the
  first time. It lets an idempotent retry return *exactly* the same response even if the
  progress has moved on (`totalXp`, unlocks). It was not in the brief's column list.
- **`sign_progress.earned_xp` has a `CHECK` between 0 and 30** (as the model requires), so
  `XP_PER_STAR` cannot exceed 10. It is validated at startup. Raising it would require
  changing the `CHECK` in the entity.
- **The catalog does not use `ON DELETE CASCADE`.** Deleting a level or sign must not be
  able to take the players' history with it.
- **Naming.** `position` (not `order`, a reserved word), `passed` instead of "approved",
  `LOCKED | AVAILABLE | PASSED` for sign status, `PLAYER` for the default role, and sign
  types `LETTER | NUMBER | MONTH | WORD | PHRASE`. Catalog codes follow
  `A`, `Ñ`, `numeros.0`, `MONTH_JANUARY`, `WORD_HELLO`, `PHRASE_GOOD_MORNING` (see "Catalog
  identifiers" below). Display names in the catalog are in English.

## Catalog identifiers (aligned with Unity)

The catalog is still 4 levels and 81 items with the same content and required XP. Only the
sign `code`s changed, following one rule: **the backend adopts Unity's identifiers for what
Unity has implemented and sends; for everything else the backend's catalog is the reference.**

| Level | Code source | Why |
| --- | --- | --- |
| `ALPHABET` (27) | **Unity**: `A`…`Z`, `Ñ` | The 27 letters are fully implemented. `LscAlphabet`, the AI's `AlphabetLandmarkTemplates` and `AlphabetLevelController` all identify a letter by the bare letter, so that is what will arrive as `signCode`. Order is the same (Ñ at position 15). |
| `NUMBERS` (31) | **Unity**: `numeros.0`…`numeros.30` | `MediaPipeNumberSignRecognizer` already sends `SignAttemptResult.signId = SignEntry.id = numeros.N`. |
| `MONTHS` (12) | **Backend**: `MONTH_*` | Not implemented in Unity; the `meses.*` ids are provisional data. |
| `GREETINGS_PHRASES` (11) | **Backend**: `WORD_*` / `PHRASE_*` | Not implemented in Unity; `saludos.*` ids and order are provisional. |

Consequences:

- The sign code validation (`^[\p{L}\p{N}_.]+# Design decisions

A record of the decisions made while building the backend and why. If you change one,
update this file.

## Stack and versions

| Decision | Rationale |
| --- | --- |
| **NestJS 12** (not the 11 from the brief) | It is what `nest new` installs today; all the `@nestjs/*` modules (config, typeorm, jwt, passport, swagger, throttler, terminus) publish 12 and are compatible with each other. |
| **TypeORM 0.3.31** (pinned; not 1.x) | `latest` on npm is already 1.1.x, but the requirement is 0.3.x and `@nestjs/typeorm` supports it. 1.x changes APIs; there is no reason to take that risk. |
| **oxlint + Prettier** instead of ESLint | The Nest 12 CLI generates oxlint (`npm run lint`). It is faster and does the same job; it was kept. |
| **TypeScript 6** with `strict: true` | Comes from the CLI. `strictPropertyInitialization` is `false` because entities and DTOs are filled by decorators/`class-transformer`, not in the constructor. |
| **Node ≥ 22** (`engines`, `.nvmrc`) | Requested LTS. Nest 12 is pure ESM: the build stays CommonJS and Node 22+ loads it with `require(esm)`. |
| **Jest with `--experimental-vm-modules`** | Nest 12 packages are ESM; Jest only loads them with that flag. It is included in `npm test`, `npm run test:cov` and `npm run test:e2e`; nothing to configure. |
| **No Swagger CLI plugin** | It generated absolute paths containing the `ñ` of the project folder and broke startup. DTOs carry explicit `@ApiProperty`. |
| **No `@nestjs/observe` or `@nestjs/mau`** | They came in the CLI template with sample keys (`YOUR_APP_KEY`); they are not needed and sent telemetry to an external service. |
| **Everything in English** | Code, comments, tables, columns, enums, endpoints, JSON fields, error codes and documentation. The only Spanish left is content that must match Unity: avatar ids (`luna`, `nube`, `rio`, `sol`) and the game's name. |

## Database

- **`synchronize: true`, no migrations.** It is the requirement. Known risk: if a property
  of an entity holding data is **renamed or removed**, TypeORM drops the column (and its
  data) or creates a new empty one. Review such a change before deploying (see "Deployment"
  in the README). Adding properties is safe if they are `nullable` or have a `default`.
  Also: **tables removed from the entities are never dropped** by `synchronize`; when the
  schema was renamed to English the old Spanish tables had to be removed by hand.
- **Connection with separate fields** (`host`, `port`, `database`, `username`, `password`)
  and never `url`: user and password cannot be overridden by what the URL carries.
  `DB_URL` is rejected at startup if it contains a user, password or `sslmode`.
- **`ssl: { rejectUnauthorized: false }`** when `DB_SSL=true`. The Supabase pooler does not
  provide a certificate chain that `pg` validates by default; the connection is encrypted
  but the server identity is not verified.
- **`uuidExtension: 'pgcrypto'`** → keys use `gen_random_uuid()` (the requested default)
  instead of `uuid_generate_v4()` from `uuid-ossp`.
- **`logging: false` in TypeORM.** Its error log prints the query parameters (password
  hashes, tokens). Errors are logged by our filter: only name and message.
- **No functional indexes.** The email is normalized to lowercase in the application and
  has a plain unique index (`ux_users_email`).
- **Index `(user_id, created_at)` without `DESC`.** TypeORM's `@Index` decorator does not
  allow descending order; Postgres scans the index backwards with the same performance for
  the history (most recent first).
- **History is immutable by convention**, not by trigger: the code only inserts into
  `evaluations`. A trigger blocking `DELETE` would prevent `ON DELETE CASCADE` when a user
  is deleted.
- **Extra column `evaluations.response` (jsonb).** It stores the response delivered the
  first time. It lets an idempotent retry return *exactly* the same response even if the
  progress has moved on (`totalXp`, unlocks). It was not in the brief's column list.
- **`sign_progress.earned_xp` has a `CHECK` between 0 and 30** (as the model requires), so
  `XP_PER_STAR` cannot exceed 10. It is validated at startup. Raising it would require
  changing the `CHECK` in the entity.
- **The catalog does not use `ON DELETE CASCADE`.** Deleting a level or sign must not be
  able to take the players' history with it.
- **Naming.** `position` (not `order`, a reserved word), `passed` instead of "approved",
  `LOCKED | AVAILABLE | PASSED` for sign status, `PLAYER` for the default role, and sign
  types `LETTER | NUMBER | MONTH | WORD | PHRASE`. Catalog codes follow
  `A`, `Ñ`, `numeros.0`, `MONTH_JANUARY`, `WORD_HELLO`, `PHRASE_GOOD_MORNING` (see "Catalog
  identifiers" below). Display names in the catalog are in English.

, 1–60 characters) accepts `Ñ` and dots, and
  `catalog.json` is checked against it (and for case-insensitive duplicates) before seeding.
- Because letters are uppercase, numbers lowercase and the rest uppercase with underscores,
  **matching is case-insensitive** and responses always carry the catalog's spelling.
- Signs in Unity's `SignCatalog.asset` that are not among the 81 (`colores.*`, `personas.*`,
  `saludos.por_favor`, `numeros.uno`…) are **not** added; they are listed in the contract as
  provisional data.

## Seed reconciliation

The database already had the 81 signs under the old codes, and the seed upserts by `code`, so
new codes would have created duplicates. `CatalogSeeder.seed` now does the following in the
bootstrap transaction:

1. Upserts levels and signs from `catalog.json`.
2. **Replaces** the `level_signs` links of every catalog level (delete + insert). This avoids
   the unique `(level_id, position)` constraint failing on reorders.
3. For signs in the database that are not in `catalog.json`: removes their links, then
   **deletes** them if no `evaluations` or `sign_progress` row references them, or sets
   `active = false` if one does (history must survive).
4. `verify` then requires exactly 4 active levels and 81 active signs (27 + 31 + 12 + 11);
   on mismatch the bootstrap throws, the transaction **rolls back** and the app does not
   start (with `DB_SEED_ON_START=false` the check only logs).

Covered by e2e tests: changed code, reorder, unreferenced orphan, referenced orphan,
idempotency and rollback on failed verification.

- **`synchronize` does not update an existing CHECK's expression.** TypeORM compares CHECK
  constraints by name. When the precision range changed from 0–1 to 0–100, the existing
  `chk_evaluations_precision` kept `<= 1` in Supabase (it would have rejected every real
  value) while the new `numeric(5,2)` type was applied. The fix is to give a CHECK a new name
  whenever its expression changes: the checks are now `chk_evaluations_precision_0_100` and
  `chk_sign_progress_precision_0_100`, and an e2e test reads their definitions from
  `pg_constraint`. **Rule:** when you edit a CHECK's expression, rename it.
- **Progress does not migrate across a code change.** If a sign's code changes while players
  already have history for it, the reconciliation keeps that history by deactivating the old
  sign (it is not deleted), but the player's progress stays on the old, inactive sign and does
  not carry over to the new code. This was irrelevant for the code change made here (the
  database had no player data), but a future rename with real data needs a data migration.

## Database security (RLS)

`DatabaseBootstrapService` enables `ROW LEVEL SECURITY` (no policies) and runs
`REVOKE ALL ... FROM anon, authenticated` on the 8 tables, on every startup (idempotent).
This way Supabase's public API (PostgREST) can neither read nor write anything. The backend
connects with the `postgres` user, which owns the tables and is not affected by RLS. If the
`anon`/`authenticated` roles do not exist (test PostgreSQL) the `REVOKE` is skipped. It all
happens in one transaction together with the seed: if it fails, the app does not start.

## Scoring and XP

- **The backend verifies, it does not recompute.** It compares what the AI reports against
  the official table. Below the pass threshold it does not validate the reported stars/XP
  (Unity's numbers recognizer assigns 1 star to any value below 74, even below 60), but
  stores them as received for auditing.
- **Precision is on a 0–100 scale.** Unity's AI already sends `Similarity * 100f` and the AI
  cannot be changed, so the backend adapts: thresholds are `60 / 74 / 88` (validated as
  `0 < pass < two < three ≤ 100`), the DTO accepts 0–100 with at most 2 decimals, and
  `evaluations.precision` / `sign_progress.best_precision` are `numeric(5,2)` with a `CHECK`
  between 0 and 100. Values outside the range or with more than 2 decimals are rejected
  (`400`). Precision is still rounded to 2 decimals before evaluating, so what is evaluated is
  exactly what is stored. A client still sending the old 0–1 scale would not be rejected
  (0.82 is a valid 0–100 value); it would simply fail the pass threshold, so this is called
  out in the contract.
- **In the response, `stars` is 0 if the attempt did not pass**, even if the AI reported 1.
  The reported value stays in `evaluations.reported_stars`.
- **`bestPrecision` is updated by failed attempts too**; `bestStars` and XP are not (only
  passed attempts count).
- **Profile XP is added with an atomic `UPDATE ... SET total_xp = total_xp + n`**, not by
  read-then-write. Two simultaneous attempts on different signs cannot overwrite each other.
  `profiles.total_xp` and the sum of `sign_progress.earned_xp` stay equal within the same
  transaction.

## Idempotency and concurrency of `POST /evaluations`

1. `(user_id, attempt_id)` is looked up first.
2. The `sign_progress` row is locked with `SELECT ... FOR UPDATE` (created beforehand with
   `INSERT ... ON CONFLICT DO NOTHING`). Simultaneous attempts on the same sign are serialized.
3. After getting the lock the attempt is **looked up again** (another submission may have
   committed while waiting).
4. If the `INSERT` still collides with `ux_evaluations_user_attempt`, error `23505` is
   caught and the winning evaluation is returned.

"Same content" compares sign, precision (2 decimals), stars, XP, duration and feedback.
Tested with 10 simultaneous submissions: one `201`, nine identical `200`, XP awarded once.

### A sign in several levels

The model lets a sign be in several levels. `POST /evaluations` only receives the sign
code, so the **first unlocked level in which the sign is AVAILABLE or PASSED** is used (and
that is the stored `level_id`). Progress is per sign, not per level. Today each sign is in
exactly one level.

## Authentication and sessions

- **argon2id** for passwords. At login, if the email does not exist the password is checked
  against a fake hash so response time is similar and the message identical.
- **Inactive user:** `403 INACTIVE_USER` only if the password is correct (with a wrong
  password it is still `401`, so the account status is not revealed).
- **Refresh token** `<sessionId>.<secret>`: the id allows direct lookup and locking
  (`FOR UPDATE`); only the secret's SHA-256 is stored and it is compared in constant time.
- **Reuse detection:** presenting the refresh token of an *already revoked* session revokes
  **all** of the user's sessions (`401 INVALID_SESSION`). It applies literally to any
  revoked session, including ones closed by logout. Consequence to be aware of: if Unity
  sends a refresh token it already discarded (e.g. after a logout) it closes the player's
  other devices. Unity must delete the tokens on logout and **not blindly retry a
  `/auth/refresh` that failed on the network with the same token** (see the contract).
- **The JWT guard queries the session in the database on every request.** It costs one
  more query, but makes `logout` take effect immediately (a 15-minute JWT would stay valid
  if only the signature were verified). It also rejects tokens of disabled users.
- **`ThrottlerGuard` runs before `JwtAuthGuard`**, so a client without a session is limited too.
- **Throttling:** two named limiters: `global` (100 per minute) and `auth` (5 per minute,
  only routes with `@StrictThrottle()`: register, login and refresh). Nest's limit is **per
  route and per IP**, not shared between register and login. `THROTTLE_AUTH_LIMIT` is a
  variable added to the brief so it can be changed and tested.
- **`trust proxy = 1` only in production**, so behind Render the throttler's IP is the
  client's and not the proxy's.

## API

- **Uniform errors** `{statusCode, code, message, details?, path, timestamp}`. 500 errors
  never expose internal details (the detail only goes to the log, without request bodies).
  An unknown route is `404 NOT_FOUND`.
- **`ValidationPipe`** with `whitelist`, `forbidNonWhitelisted` and `transform`. Because
  transformed DTOs carry unsent keys as `undefined`, the preferences merge drops the
  `undefined` ones (there was a real bug with this; it has a test).
- **Sign codes are case-insensitive** (`a`, `ñ`, `NUMEROS.0`) in routes, filters and the
  `POST /evaluations` body; the catalog's spelling is canonical and is what responses
  return. Level codes are normalized to uppercase.
- **Helmet** always; the CSP is disabled only on `/docs` (Swagger UI uses inline scripts and
  styles). **CORS** is off unless `CORS_ORIGINS` is set.
- **`GET /health` with a 5 s timeout** on the database check (Terminus' default is 1 s).
  Against the Supabase pooler from outside AWS the query takes ~1 s and the health check
  returned 503 intermittently.
- **Swagger at `/docs`** (outside the `/api/v1` prefix), off in production in `render.yaml`
  (`SWAGGER_ENABLED=false`); enable it if you need it.

## Tests

- **Unit** with an in-memory `EntityManager` (`test/helpers/fake-db.ts`) that uses the real
  catalog. They test the transaction logic without a database.
- **E2E** with PostgreSQL 16 in Docker (`docker-compose.test.yml`, port 54329, on `tmpfs`).
  With `NODE_ENV=test` the app **does not read `backend/.env`**, so the tests cannot touch
  Supabase even if the file exists.
- **In-memory doubles do not see real driver failures:** the XP `UPDATE ... RETURNING`
  returned `raw=[]` with `.returning(['total_xp'])` and only the e2e caught it. That is why
  both levels of testing exist.

## Out of scope: how to add it later

- **Email verification / password recovery:** add `email_verified_at` (nullable) to `users`
  and a `user_tokens` table (`user_id`, `type`, `hash`, `expires_at`, `used_at`) with
  single-use tokens stored hashed, a mail module (SMTP/Resend) and the endpoints
  `POST /auth/verify-email`, `POST /auth/forgot-password` and `POST /auth/reset-password`.
  Resetting must revoke all sessions. With `synchronize`, only adding new columns/tables is safe.
- **Admin panel / catalog editing:** the `ADMIN` role already exists in the model; a
  `RolesGuard` and endpoints under `/admin` would be missing. Today the catalog is managed
  by editing `src/database/seeds/data/catalog.json`.
