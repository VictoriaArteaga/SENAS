# Unity ↔ Backend integration contract

For the Unity team. The backend is the **single source of truth for progress**: Unity does
not store progress locally. This document describes how to talk to it.

- Base: `https://<host>/api/v1` (locally `http://localhost:3000/api/v1`).
- JSON in `camelCase`, dates in ISO 8601 UTC, `Content-Type: application/json`.
- Interactive documentation (if enabled): `/docs`.

## 1. Flow of a practice

```
Player ──► Unity ──► AI (MediaPipe, local) ──► result
                         signId, precision (0–100), stars, xp, feedback
                                     │
            Unity: POST /evaluations │ (with attemptId, signCode, ...)
                                     ▼
                              Backend validates, records, adds XP,
                              computes unlocks and responds
                                     │
            Unity shows: stars, XP, "B unlocked", etc.
```

The backend **never** receives images, video or landmarks, and **does not recompute**
precision or stars: it only checks that what the AI reports is consistent with the official
table (section 5).

Typical game session:

1. Register or login → store `accessToken` and `refreshToken`.
2. `GET /profile` and `GET /levels` → draw the map with levels and state.
3. `GET /levels/{code}` when entering a level → signs with `LOCKED | AVAILABLE | PASSED`.
4. For every finished attempt → `POST /evaluations`.
5. With the response, update the UI (XP, stars, unlocks) or fetch `GET /levels/{code}` again.

## 2. Authentication and token handling

All routes except `register`, `login`, `refresh` and `health` require
`Authorization: Bearer <accessToken>`.

| Token | Lifetime | Where to keep it |
| --- | --- | --- |
| `accessToken` (JWT) | `expiresIn` seconds (900 = 15 min) | In memory only |
| `refreshToken` | 30 days, **single use** | Secure storage on the player's machine (never in logs) |

**When to refresh**

- When a request returns `401`, do **one** `POST /auth/refresh` and repeat the original
  request **once** with the new token.
- Optionally refresh before expiry (`expiresIn` minus a 30 s margin).
- **Serialize refreshes:** never fire two `/auth/refresh` at once with the same token; the
  second would be a reuse.

**Rotation and reuse (important)**

Every `POST /auth/refresh` returns a **new** `refreshToken` and invalidates the previous
one. You must **always replace** the stored one with the new one. If the backend receives a
refresh token that was already used or revoked, it assumes it was stolen and **closes all of
the player's sessions** (`401 INVALID_SESSION`).

- If a `/auth/refresh` fails on the **network** (no response), you cannot know whether the
  server processed it. Retrying with the same token may trigger the revocation. The safe
  approach: if the retry returns `401 INVALID_SESSION`, send the player to the login screen.
- If `/auth/refresh` returns `401 INVALID_SESSION` → delete the tokens and ask for login.
- On **sign out**, call `POST /auth/logout`, delete both tokens and never use the
  `refreshToken` again.

**On a `401` with `code`:**

| `code` | What to do |
| --- | --- |
| `UNAUTHORIZED` | `Authorization` missing, or the token is invalid/expired → try `refresh` |
| `INVALID_SESSION` | The session was closed/revoked → login |
| `INVALID_CREDENTIALS` | Only on `/auth/login`: wrong email or password |

## 3. Endpoints

### `POST /auth/register` (public) → 201

```json
{ "email": "ana@example.com", "password": "Lumina2026", "displayName": "Ana", "avatarId": "luna" }
```

`password`: 8–72 characters, at least one letter and one number. `displayName`: 2–40.
`avatarId` optional (`luna | nube | rio | sol`; defaults to `luna`). The email is stored
lowercase.

```json
{
  "accessToken": "eyJhbGciOi...",
  "refreshToken": "7c1f2a9e-....-....-....-............aB3x...",
  "tokenType": "Bearer",
  "expiresIn": 900,
  "user": { "id": "0b6f...-uuid", "email": "ana@example.com", "role": "PLAYER" }
}
```

Errors: `400 VALIDATION_FAILED | INVALID_AVATAR`, `409 EMAIL_IN_USE`, `429`.

### `POST /auth/login` (public) → 200

`{ "email": "...", "password": "..." }` → same response as register.
Errors: `401 INVALID_CREDENTIALS` (same message whether or not the email exists),
`403 INACTIVE_USER`, `429`.

### `POST /auth/refresh` (public) → 200

`{ "refreshToken": "..." }` → same response as login (new tokens).
Errors: `401 INVALID_SESSION`, `403 INACTIVE_USER`, `429`.

### `POST /auth/logout` and `POST /auth/logout-all` → 204

With `Authorization`. `logout` closes the current session; `logout-all` all of the
player's sessions. They take effect immediately: the `accessToken` stops working at once.

### `GET /profile` → 200

```json
{
  "userId": "0b6f...-uuid", "email": "ana@example.com", "displayName": "Ana",
  "avatarId": "luna", "avatarConfig": {}, "preferences": { "language": "es", "theme": "light" },
  "totalXp": 310, "createdAt": "2026-11-01T15:00:00.000Z"
}
```

### `PATCH /profile` → 200

`{ "displayName": "Maria", "preferences": { "language": "en", "theme": "dark" } }`
All fields are optional; preferences are **merged** with the stored ones.
`language`: `es | en`; `theme`: `light | dark`.

### `PATCH /profile/avatar` → 200

`{ "avatarId": "sol", "avatarConfig": { "color": "blue" } }` (`avatarConfig` ≤ 4 KB).
Errors: `400 INVALID_AVATAR | INVALID_AVATAR_CONFIG`.

### `GET /levels` → 200

```json
[
  { "code": "ALPHABET", "name": "Alphabet", "description": "...", "position": 1,
    "unlocked": true, "requiredXp": 0, "missingXp": 0,
    "passedSigns": 5, "totalSigns": 27, "earnedStars": 11, "possibleStars": 81 },
  { "code": "NUMBERS", "name": "Numbers", "position": 2,
    "unlocked": false, "requiredXp": 300, "missingXp": 180,
    "passedSigns": 0, "totalSigns": 31, "earnedStars": 0, "possibleStars": 93 }
]
```

### `GET /levels/{code}` → 200

The level (same fields) plus `signs` in order:

```json
{
  "code": "ALPHABET", "...": "...",
  "signs": [
    { "code": "A", "name": "Letter A", "type": "LETTER", "position": 1,
      "status": "PASSED", "bestPrecision": 91, "bestStars": 3 },
    { "code": "B", "name": "Letter B", "type": "LETTER", "position": 2,
      "status": "AVAILABLE", "bestPrecision": null, "bestStars": 0 },
    { "code": "C", "name": "Letter C", "type": "LETTER", "position": 3,
      "status": "LOCKED", "bestPrecision": null, "bestStars": 0 }
  ]
}
```

Error: `404 LEVEL_NOT_FOUND`. The UI **must respect `position`**.

### `GET /signs?type=&level=` and `GET /signs/{code}` → 200

Catalog (without player state). `type`: `LETTER | NUMBER | MONTH | WORD | PHRASE`.
`GET /signs/A` → `{ "code": "A", "name": "Letter A", "type": "LETTER", "description": null, "levels": [{ "level": "ALPHABET", "position": 1 }] }`.
Error: `404 SIGN_NOT_FOUND`.

### `POST /evaluations` → 201 (first time) / 200 (identical retry)

```json
{
  "attemptId": "6f1d9c1e-3b52-4c1a-8f0e-1d6c2a9b7e44",
  "signCode": "A",
  "precision": 82,
  "stars": 2,
  "xp": 20,
  "durationMs": 4200,
  "feedback": "optional text from the AI"
}
```

| Field | Rule |
| --- | --- |
| `attemptId` | UUID v4 generated by Unity |
| `signCode` | Catalog code (section 7): `A`, `Ñ`, `numeros.5`, `MONTH_JANUARY`, … Matched case-insensitively; only letters, digits, `_` and `.`; max. 60 characters |
| `precision` | Number **0–100** (similarity), **at most 2 decimals**. This is the scale Unity's AI already produces (`Similarity * 100`) |
| `stars` | Integer 0–3, as produced by the AI |
| `xp` | Integer ≥ 0, as produced by the AI |
| `durationMs` | Optional, integer ≥ 0 |
| `feedback` | Optional, max. 500 characters |

Response:

```json
{
  "evaluationId": "uuid",
  "attemptId": "6f1d9c1e-3b52-4c1a-8f0e-1d6c2a9b7e44",
  "signCode": "A",
  "passed": true,
  "precision": 82,
  "stars": 2,
  "awardedXp": 20,
  "totalXp": 310,
  "bestPrecision": 82,
  "bestStars": 2,
  "unlockedSign": "B",
  "unlockedLevels": ["NUMBERS"]
}
```

- `passed` is decided by the backend (`precision ≥ 60`).
- `stars` in the response is **0** if the attempt did not pass, even if the AI reported 1.
- `awardedXp` is only the **improvement** over what was already earned on that sign (max. 30 per sign).
- `unlockedSign` is `null` if no sign was unlocked. `unlockedLevels` has only the levels
  opened **by this attempt**.
- A failed attempt is still recorded (it counts as an attempt and can improve
  `bestPrecision`) but gives 0 XP.

Errors: `400 VALIDATION_FAILED`, `403 LEVEL_LOCKED | SIGN_LOCKED`, `404 SIGN_NOT_FOUND`,
`409 DUPLICATE_ATTEMPT`, `422 INCONSISTENT_RESULT`.

### `GET /evaluations` → 200

Paginated history, most recent first. Query: `page` (1…), `limit` (1–100, default 20),
`sign` (code), `level` (code), `passed` (`true|false`), `from`, `to` (ISO 8601).

```json
{ "items": [ { "id": "uuid", "attemptId": "uuid", "signCode": "A", "levelCode": "ALPHABET",
  "precision": 82, "reportedStars": 2, "reportedXp": 20, "passed": true,
  "awardedXp": 20, "durationMs": 4200, "feedback": null, "createdAt": "2026-11-01T15:00:00.000Z" } ],
  "total": 41, "page": 1, "limit": 20 }
```

### `GET /progress` → 200

```json
{
  "totalXp": 310,
  "highestUnlockedLevel": { "code": "NUMBERS", "name": "Numbers", "requiredXp": 300 },
  "nextLevel": { "code": "MONTHS", "name": "Months of the year", "requiredXp": 700, "missingXp": 390 },
  "passedSigns": 12, "totalSigns": 81, "totalStars": 25,
  "totalAttempts": 40, "passRate": 0.55
}
```

`nextLevel` is `null` when all levels are already unlocked.

### `GET /progress/signs/{code}` → 200

`{ "signCode", "name", "status", "totalAttempts", "passedAttempts", "bestPrecision", "bestStars", "earnedXp", "passed", "firstPassedAt", "lastAttemptAt" }`.

### `GET /health` (public) → 200

`{ "status": "ok", "info": { "database": { "status": "up" } }, ... }`

## 4. `attemptId` idempotency

`POST /evaluations` is idempotent per `(player, attemptId)`.

1. Unity generates the `attemptId` (UUID v4) **once per attempt**, when the AI delivers the result.
2. If the submission fails on the network or times out, **retry with the same `attemptId` and
   the same body**. If the first one did arrive, the retry returns `200` with the **same
   response** and does not add XP twice.
3. The same `attemptId` with **different content** → `409 DUPLICATE_ATTEMPT`. A new attempt by
   the player must carry a new `attemptId`.
4. Keep the `attemptId` and body (in memory) until a `2xx` or a definitive `4xx` response arrives.

Retry recommendation: 15 s timeout per request; retry with growing waits (1 s, 2 s, 4 s…)
only on network errors, `429` and `5xx`. On Render's free plan the service "sleeps" and the
first request can take ~50 s: allow a longer timeout for the first call, or query
`GET /health` when the game opens.

## 5. Scoring (official table)

The AI produces the result; the backend verifies it against this table.

| Precision | Stars | Result | XP |
| --- | --- | --- | --- |
| ≥ 88 | 3 | Passed | 30 |
| ≥ 74 and < 88 | 2 | Passed | 20 |
| ≥ 60 and < 74 | 1 | Passed | 10 |
| < 60 | — | Not passed | 0 |

- If `precision ≥ 60` and `stars`/`xp` do not match the table → `422 INCONSISTENT_RESULT`
  and nothing is saved.
- If `precision < 60` it is recorded as not passed with 0 XP, regardless of the stars/XP
  sent (they are stored as received for auditing).
- Precision is on a **0–100 scale with at most 2 decimals**; values outside 0–100 or with more decimals are rejected with `400 VALIDATION_FAILED`.
- The thresholds live in the server configuration; the table is the one in force today.

## 6. Unlocks

- **Levels:** a level opens when `totalXp ≥ requiredXp` (0 / 300 / 700 / 1200).
- **Signs:** in an open level, the sign with `position = 1` is `AVAILABLE`; the sign
  `position = k` unlocks when `k-1` has been **passed** at least once.
- A passed sign can be practiced again to improve (more stars = more XP, up to 30 per sign).
- Practicing a `LOCKED` sign → `403 SIGN_LOCKED`; one in a closed level → `403 LEVEL_LOCKED`.

## 7. Official catalog (81 items)

The catalog has 4 levels and 81 items (27 + 31 + 12 + 11). A sign's `code` is what
`POST /evaluations` expects in `signCode` (case-insensitive). **Where Unity already
implements and sends an item, the backend uses Unity's own identifier and order; for
everything Unity has not implemented yet, the backend's catalog is the reference.**

| Level | Code | Required XP | Items | Where the ids come from |
| --- | --- | --- | --- | --- |
| 1 | `ALPHABET` | 0 | 27 | **Unity**: the bare letter used by `LscAlphabet`, the AI templates and `AlphabetLevelController` |
| 2 | `NUMBERS` | 300 | 31 | **Unity**: `SignAttemptResult.signId` = `numeros.N`, as sent by `MediaPipeNumberSignRecognizer` |
| 3 | `MONTHS` | 700 | 12 | **Backend** (Unity has not implemented this level) |
| 4 | `GREETINGS_PHRASES` | 1200 | 11 | **Backend** (Unity has not implemented this level) |

### Final codes and order

**ALPHABET** (type `LETTER`, Spanish alphabet order, position 1–27):
`A` `B` `C` `D` `E` `F` `G` `H` `I` `J` `K` `L` `M` `N` `Ñ` `O` `P` `Q` `R` `S` `T` `U` `V` `W` `X` `Y` `Z`

**NUMBERS** (type `NUMBER`, position 1–31): `numeros.0`, `numeros.1`, … `numeros.30`

**MONTHS** (type `MONTH`, position 1–12):
`MONTH_JANUARY`, `MONTH_FEBRUARY`, `MONTH_MARCH`, `MONTH_APRIL`, `MONTH_MAY`, `MONTH_JUNE`,
`MONTH_JULY`, `MONTH_AUGUST`, `MONTH_SEPTEMBER`, `MONTH_OCTOBER`, `MONTH_NOVEMBER`, `MONTH_DECEMBER`

**GREETINGS_PHRASES** (position 1–11):

| Position | Code | Type | Name |
| --- | --- | --- | --- |
| 1 | `PHRASE_GOOD_MORNING` | PHRASE | Good morning |
| 2 | `PHRASE_GOOD_AFTERNOON` | PHRASE | Good afternoon |
| 3 | `PHRASE_GOOD_EVENING` | PHRASE | Good evening |
| 4 | `WORD_HELLO` | WORD | Hello |
| 5 | `WORD_GOODBYE` | WORD | Goodbye |
| 6 | `WORD_YES` | WORD | Yes |
| 7 | `WORD_NO` | WORD | No |
| 8 | `PHRASE_HOW_ARE_YOU` | PHRASE | How are you? |
| 9 | `WORD_THANK_YOU` | WORD | Thank you |
| 10 | `PHRASE_FINE_THANK_YOU` | PHRASE | Fine, thank you |
| 11 | `PHRASE_YOU_ARE_WELCOME` | PHRASE | You're welcome |

### Codes that changed in the backend (previous → current)

| Previous backend code | Current code (Unity's id) |
| --- | --- |
| `LETTER_A` … `LETTER_N`, `LETTER_O` … `LETTER_Z` | `A` … `N`, `O` … `Z` |
| `LETTER_ENYE` | `Ñ` |
| `NUMBER_0` … `NUMBER_30` | `numeros.0` … `numeros.30` |
| `MONTH_*`, `WORD_*`, `PHRASE_*` | unchanged |

Avatars: `AvatarCatalog.asset` defines `luna`, `nube`, `rio`, `sol`; they match the backend's
`AVATAR_IDS`.

## 8. Error codes

Format: `{ "statusCode": 403, "code": "SIGN_LOCKED", "message": "...", "details": [...]?, "path": "/api/v1/evaluations", "timestamp": "..." }`.
React to the `code`, not to the text of `message`.

| HTTP | `code` | When | What to do in Unity |
| --- | --- | --- | --- |
| 400 | `VALIDATION_FAILED` | Invalid body/query (`details` lists the fields) | A client bug or a form error: fix it |
| 400 | `INVALID_AVATAR` | `avatarId` not in `AVATAR_IDS` | Show a valid selector |
| 400 | `INVALID_AVATAR_CONFIG` | `avatarConfig` > 4 KB | Reduce it |
| 401 | `UNAUTHORIZED` | Missing token, or expired/invalid | `refresh` and repeat once |
| 401 | `INVALID_SESSION` | Closed or revoked session, or invalid refresh token | Login |
| 401 | `INVALID_CREDENTIALS` | Wrong login | Show the error |
| 403 | `INACTIVE_USER` | Disabled account | Show a notice |
| 403 | `LEVEL_LOCKED` | Level without enough XP | Refresh `GET /levels` |
| 403 | `SIGN_LOCKED` | The previous sign must be passed first | Refresh `GET /levels/{code}` |
| 404 | `SIGN_NOT_FOUND` | Nonexistent sign code | Check the equivalence table |
| 404 | `LEVEL_NOT_FOUND` | Nonexistent level code | — |
| 404 | `NOT_FOUND` | Nonexistent route or resource | — |
| 409 | `EMAIL_IN_USE` | Register with an existing email | Offer login |
| 409 | `DUPLICATE_ATTEMPT` | Same `attemptId`, different content | Generate a new `attemptId` |
| 422 | `INCONSISTENT_RESULT` | Stars/XP do not match the table | Review what Unity sends |
| 429 | `TOO_MANY_REQUESTS` | Rate limit | Wait and retry with growing waits |
| 500 | `INTERNAL_ERROR` | Server failure (no details) | Retry with waits; report if it persists |

## 9. Unity pending items

Findings from reviewing (read-only) `Assets/Project/Scripts`, `Assets/Project/Data` and
`Assets/IA`. **Nothing was modified in Unity.**

What Unity actually has today: the 27 letters are fully implemented (3D models and
animations, AI templates and evaluator). Numbers have AI templates
(`NumberLandmarkTemplates.asset`), `MediaPipeNumberSignRecognizer` and the 31 objects of
`NumbersHouse`, but no 3D animations. Greetings have a Unity learning house and MediaPipe
evaluation; their local `saludos.*` IDs are translated to the backend's canonical codes.
Months have a Unity learning house and local Holistic capture/evaluation for validation; their
local `meses.*` IDs map to the backend's canonical `MONTH_*` codes. Months are not enabled for
backend progression until their references are validated.

### 9.1 Changes Unity must make

> **Status:** items 1–3 are implemented in the Unity client (section 10). Greeting codes are mapped in Unity; item 4 remains
> open for the Months level.

1. **Send `stars` and `xp` as the AI produces them** (the numbers recognizer already gives
   `stars * 10` XP, which matches the table), and `precision` as the AI produces it
   (`Similarity * 100f`, 0–100, at most 2 decimals — round with `Mathf.Round(x * 100f) / 100f`
   if needed). If `SignAttemptResult.evaluated` is `false` or `stars`/`xp` are `-1` (the
   defaults), **do not send** the attempt.
2. **`AlphabetLevelController` stores progress locally** (`PlayerPrefs`, keys such as
   `Lumina.Alphabet.Practiced.Qualified.v1.<letter>`) and does not go through
   `ISignRecognizer`. Progress must be **queried** (`GET /levels/...`, `GET /progress`) and
   **submitted** (`POST /evaluations`) to the backend; the status of the signs
   (locked/available/passed) comes from the server. For letters, send the bare letter as
   `signCode` (`LetterEntry.letter`), e.g. `"A"`, `"Ñ"`.
3. **`AuthenticationMockService` must be replaced** by an HTTP implementation of
   `IAuthenticationService` (register, login, refresh, logout) with the token handling from
   section 2.
4. **Months:** when Unity implements them, it must use the backend's codes and order from
   section 7 (`MONTH_JANUARY` … `MONTH_DECEMBER`). Greetings are implemented using the
   backend's official order and a `SignCodes.BackendCodeFor` mapping from local `saludos.*`
   recognition IDs to `WORD_*` / `PHRASE_*` codes.

### 9.2 Identifier findings (letters and numbers)

- **Letters have no `signId` yet**: they are only addressed by `LetterEntry.letter`
  (`LscAlphabet.asset`), by `_letter` in `AlphabetLandmarkTemplates.asset` and by
  `CandidateLetter` in the evaluator. All three use the same bare letter (`A`…`Z`, `Ñ` =
  U+00D1), so that is the id the backend adopted. `Ñ` sits at position 15, between `N` and `O`,
  in all of them.
- **Numbers have two spellings in Unity.** `SignEntry.id` / `SignAttemptResult.signId` is
  `numeros.N` (what the recognizer sends, and what the backend adopted), while
  `NumberLandmarkTemplates.asset` stores the number as `_letter: N` (just `N`, an internal
  AI detail that is never sent).
- **`SignCatalog.asset` is unrelated to the real levels**: it has no letters and uses ids
  like `numeros.uno` (words) that do not match `numeros.0`…`numeros.30`.

### 9.3 Provisional data in Unity that is not in the official 81 (can be cleaned up)

Not added to the backend:

- `SignCatalog.asset`: categories `colores` (`colores.rojo`, `colores.azul`, `colores.verde`,
  `colores.amarillo`) and `personas` (`personas.yo`, `personas.tú`, `personas.amigo`,
  `personas.familia`), plus `saludos.por_favor`, `saludos.adiós` (with accent; `LearningLevels`
  uses `saludos.adios`) and `numeros.uno` … `numeros.cuatro` (as words).
- `LearningLevels` keeps local ids for Months (`meses.*`) and Greetings (`saludos.*`) so the
  recognizers can find their templates. Greeting ids are translated to canonical API codes by
  `SignCodes.BackendCodeFor`; the Unity greeting order matches the backend's 11-item order.

### 9.4 Other behavior to be aware of

- **Sequential unlock:** the backend rejects (`403 SIGN_LOCKED`) practicing a sign whose
  previous one is not passed, and levels need XP (300 / 700 / 1200). The practice screens
  must start from the `AVAILABLE`/`PASSED` signs the backend reports. The Numbers and Greetings
  houses use backend sign status; the alphabet retains its own sequential practice flow.
- **The numbers recognizer does not report attempts below its `MinimumSimilarity`** (it only
  emits `Completed` on recognition). The backend supports failed attempts
  (`precision < 60`), but to have a history of failures Unity would have to send them.

## 10. Unity implementation (Implementación en Unity)

Status: the Unity client now talks to this backend (`Assets/Project/Scripts/Api/`). This code fulfils items 1–4 of
section 9.1; what is still open is listed at the end of this section.

### 10.1 Classes (namespace `Lumina.Api` unless noted)

| Class | What it does |
| --- | --- |
| `ApiConfig` (ScriptableObject, `Assets/Project/Data/Resources/ApiConfig.asset`) | Development and production base URLs, `timeoutSeconds` (15) and the `useMockAuthentication` switch (off). It lives in `Resources` so builds have it without referencing it from a scene. |
| `ApiClient` | HTTP client over `UnityWebRequest` (`async/await` with `Task`). `GetAsync / PostAsync / PatchAsync`; `Authorization: Bearer`; path segments encoded with `Segment()` (`Ñ` → `%C3%91`); typed errors; root-level arrays wrapped as `{"items":[…]}` for `JsonUtility`. |
| `ApiException` / `ApiCodes` | Typed error: `Kind` (Network, Timeout, Server, Client, Auth, NotConfigured), `StatusCode`, `Code` (section 8), `Details`. `IsRetryable` says whether retrying makes sense. |
| `ApiSession` | Access and refresh tokens **in memory only**. Never `PlayerPrefs` or disk. |
| `AuthenticationHttpService : IAuthenticationService` | `POST /auth/register`, `/auth/login`, `/auth/logout`; loads the profile after signing in. Password recovery answers "not available" (the backend has none). |
| `AuthBootstrap` | At startup assigns `AuthServices.Current`: the HTTP service, or the mock if `ApiConfig.useMockAuthentication` is on. |
| `ProfileStore` / `ProfileSync` | In-memory profile; applies language and theme on sign-in and saves them (`PATCH /profile`, coalescing quick changes) when the player changes them. |
| `ProgressStore` / `BackendProgressProvider` | In-memory cache of `GET /levels`, `GET /progress` and `GET /levels/{code}`. Feeds "MY PROGRESS", the learning houses and the alphabet. Reloaded on sign-in and after every evaluation; cleared on sign-out. |
| `EvaluationService` / `EvaluationSubmission` | `POST /evaluations`. One `attemptId` (Guid) per attempt; retries with the **same** `attemptId` on network errors, timeouts, 429 and 5xx (up to 4 sends: 1 s, 2 s, 4 s). |
| `SignCodes`, `LevelAccess`, `ApiMessages` | Sign/level codes, entry blocking for XP-locked houses, and error messages localized by `code`. |
| `Lumina.Signs.ScoreAdapter` | Stars/XP/precision for **letters** (see 10.4). |
| `Lumina.UI.Toast` | Short notice for errors that do not belong to a form. |

### 10.2 Flow

1. **Startup:** `AuthBootstrap` picks the authentication service. Spanish and light theme by default.
2. **Register / login:** the form validates (password 8–72 with a letter and a number, name 2–40) → HTTP service → tokens in
   `ApiSession` → `GET /profile` → `AuthSession.SignIn(name, avatar, id)`. That event makes `ProfileSync` apply the
   profile's language and theme and `ProgressStore` load levels and summary.
3. **Protected requests:** `ApiClient` adds the Bearer. If the access token expires in under 30 s it refreshes first; if a
   request gets `401` it does **one** `POST /auth/refresh` (never two at once) and repeats the request **once**. If the refresh
   says the session is no longer valid (`401/403`) it clears the session and goes to Login. A network failure during the
   refresh does **not** sign the player out.
4. **Practicing a number:** `PracticeController` → the recognizer emits `Completed(SignAttemptResult)` →
   `PracticeSession.Finish` starts `EvaluationService.Submit(signId, precision, stars, xp, durationMs)` exactly as the AI
   delivers them → `ResultsController` shows "saving…" (button locked) and then **what the backend answered**: precision,
   stars, `awardedXp`, total XP and unlocks. On failure it says so and adds nothing.
5. **Practicing a letter:** `AlphabetLevelController` loads `GET /levels/ALPHABET`; when a letter is recognized it computes
   precision/stars/XP with `ScoreAdapter`, sends `POST /evaluations` with `signCode` = the letter, and shows the response.
6. **Learning houses:** on entering (`SceneEntrance`, Practice shortcuts) the level's unlocked state is checked; Numbers and
   Greetings load the state of each sign from the backend; Months show "COMING SOON" instead of PRACTICE.
   For manual Editor validation only, `GreetingsHouse` allows local practice despite XP/sign locks and does not submit local
   results. Player builds retain the backend unlock rules.
7. **Sign out:** `LogoutModalController` → `POST /auth/logout` → tokens and caches cleared → Login. Progress stays in the
   backend.

### 10.3 Changing the API URL

Edit `Assets/Project/Data/Resources/ApiConfig.asset` (Inspector):

- `developmentBaseUrl` (Editor and development builds): `http://localhost:3000/api/v1` by default.
- `productionBaseUrl` (release builds): the Render `https://…/api/v1` URL. It is empty; while it is, a release build says
  "the API is not configured" instead of failing silently. Release requires `https`.
- `timeoutSeconds`: 15 by default (the free Render plan can take ~50 s to wake up; raise it for the first request if needed).
- `useMockAuthentication`: off. Returns to simulated authentication **for UI work only**: with no real session there is no
  saved progress or practice.

`Player Settings → Other Settings → Allow downloads over HTTP` is set to **Allowed in development builds** (never *Always*), so
`http://localhost` works in the Editor and development builds but not in release.

If another program holds port 3000, change `PORT` in `backend/.env` and `developmentBaseUrl` to match.

### 10.4 Decisions and known limits

- **`ScoreAdapter` (temporary).** The AI gives stars and XP only for numbers. For letters it applies the same table
  `MediaPipeNumberSignRecognizer` uses (≥ 0.88 → 3★, ≥ 0.74 → 2★, ≥ 0.60 → 1★, XP = stars × 10). Those thresholds are literals
  inside the recognizer (not accessible constants), so they are repeated. Remove it when the AI delivers stars and XP for letters.
  Stars are decided with the recognizer's own float comparisons and the precision is kept inside its stars' band, so the
  backend never rejects a result because of floating-point rounding (`0.88f` is actually 0.87999999…).
- **Precision is truncated to 2 decimals** (not rounded) before sending, so it never jumps a band relative to what the AI decided.
- **"Username" field of the registration form:** the backend has no such data (it rejects extra properties). With the HTTP
  service the field is hidden and the visible name is the "player name". With the mock it stays visible.
- **"Remember me":** hidden with the HTTP service (tokens are not stored, by decision; remembering sessions would be a separate decision).
- **Password recovery:** the backend does not offer it yet; the button says it is not available.
- **Language and theme** are no longer in `PlayerPrefs`; they live in the profile. Volume, camera and quality remain per-device.
- **"Seen" letters** count only for the current visit (not saved).
- **Average precision** in "MY PROGRESS": mean of the last 100 passed attempts (`GET /evaluations?passed=true&limit=100`).
- **No manual retry button:** if sending a result fails after the automatic retries, Results says so but does not offer a
  "retry" button; the attempt is not saved and the player can practice again.
- **Old local data** (`Lumina.Alphabet.*` in `PlayerPrefs`) is ignored, not migrated.
- **Still open in Unity:** number animations; Months (use the backend codes); cleaning the provisional
  data of section 9.3; a UI to change the avatar from the profile (the client already has `ProfileStore.UpdateAvatarAsync`;
  the button is missing).
