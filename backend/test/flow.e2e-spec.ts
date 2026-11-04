import { INestApplication } from '@nestjs/common';
import { randomUUID } from 'node:crypto';
import request from 'supertest';
import { DataSource } from 'typeorm';
import {
  API,
  attemptBody,
  auth,
  createApp,
  registerUser,
  THREE_STARS,
} from './helpers/app';

describe('Full flow (e2e, real PostgreSQL with synchronize and seed)', () => {
  let app: INestApplication;
  let http: ReturnType<INestApplication['getHttpServer']>;

  beforeAll(async () => {
    app = await createApp();
    http = app.getHttpServer();
  });
  afterAll(async () => {
    await app.close();
  });

  describe('database', () => {
    it('creates the 8 tables with RLS enabled and no migrations table', async () => {
      const ds = app.get(DataSource);
      const rows: { table: string; rls: boolean }[] = await ds.query(
        `select c.relname as "table", c.relrowsecurity as rls
           from pg_class c join pg_namespace n on n.oid = c.relnamespace
          where n.nspname = 'public' and c.relkind = 'r' order by 1`,
      );
      expect(rows.map((r) => r.table)).toEqual([
        'evaluations',
        'level_signs',
        'levels',
        'profiles',
        'sessions',
        'sign_progress',
        'signs',
        'users',
      ]);
      expect(rows.every((r) => r.rls)).toBe(true);
    });

    it('stores precision as numeric(5,2) with a CHECK between 0 and 100', async () => {
      const ds = app.get(DataSource);
      const cols: { t: string; p: number; sc: number }[] = await ds.query(
        `select table_name t, numeric_precision p, numeric_scale sc from information_schema.columns
          where table_schema = 'public' and column_name in ('precision', 'best_precision') order by 1`,
      );
      expect(cols).toEqual([
        { t: 'evaluations', p: 5, sc: 2 },
        { t: 'sign_progress', p: 5, sc: 2 },
      ]);
      const checks: { def: string }[] = await ds.query(
        `select pg_get_constraintdef(oid) def from pg_constraint
          where contype = 'c' and conname like '%precision%'`,
      );
      expect(checks).toHaveLength(2);
      // A stale "<= 1" CHECK (TypeORM does not update an existing CHECK's expression) would
      // reject every real precision value.
      for (const c of checks) expect(c.def).toContain('100');
      expect(JSON.stringify(checks)).not.toMatch(/<= (1)::numeric/);
    });

    it('loads 4 levels and 81 signs with 27+31+12+11', async () => {
      const ds = app.get(DataSource);
      const per: { code: string; n: number }[] = await ds.query(
        `select l.code, count(*)::int as n from level_signs ls join levels l on l.id = ls.level_id group by 1 order by min(l.position)`,
      );
      expect(per).toEqual([
        { code: 'ALPHABET', n: 27 },
        { code: 'NUMBERS', n: 31 },
        { code: 'MONTHS', n: 12 },
        { code: 'GREETINGS_PHRASES', n: 11 },
      ]);
      const [{ total }] = await ds.query(
        `select count(*)::int as total from signs where active`,
      );
      expect(total).toBe(81);
    });

    it('on restart the seed duplicates nothing and keeps the data', async () => {
      const u = await registerUser(app, 'Persistent');
      await app.close();
      app = await createApp();
      http = app.getHttpServer();

      const ds = app.get(DataSource);
      const [{ signs }] = await ds.query(
        `select count(*)::int as signs from signs`,
      );
      const [{ levels }] = await ds.query(
        `select count(*)::int as levels from levels`,
      );
      const [{ links }] = await ds.query(
        `select count(*)::int as links from level_signs`,
      );
      expect([signs, levels, links]).toEqual([81, 4, 81]);

      const login = await request(http)
        .post(`${API}/auth/login`)
        .send({ email: u.email, password: u.password })
        .expect(200);
      expect(login.body.user.id).toBe(u.userId);
    });
  });

  describe('cross-cutting', () => {
    it('GET /health is public and returns ok', async () => {
      const res = await request(http).get(`${API}/health`).expect(200);
      expect(res.body.status).toBe('ok');
      expect(res.body.info.database.status).toBe('up');
    });

    it('Swagger responds at /docs and the JSON documents the main routes', async () => {
      await request(http).get('/docs').expect(200);
      const doc = await request(http).get('/docs-json').expect(200);
      const paths = Object.keys(doc.body.paths);
      for (const path of [
        '/api/v1/auth/register',
        '/api/v1/auth/login',
        '/api/v1/auth/refresh',
        '/api/v1/auth/logout',
        '/api/v1/auth/logout-all',
        '/api/v1/profile',
        '/api/v1/profile/avatar',
        '/api/v1/levels',
        '/api/v1/levels/{code}',
        '/api/v1/signs',
        '/api/v1/signs/{code}',
        '/api/v1/evaluations',
        '/api/v1/progress',
        '/api/v1/progress/signs/{code}',
        '/api/v1/health',
      ]) {
        expect(paths).toContain(path);
      }
    });

    it('sends helmet security headers and does not enable CORS', async () => {
      const res = await request(http).get(`${API}/health`);
      expect(res.headers['x-content-type-options']).toBe('nosniff');
      expect(res.headers['access-control-allow-origin']).toBeUndefined();
    });

    it('an unknown route uses the uniform error format', async () => {
      const res = await request(http).get(`${API}/does-not-exist`).expect(404);
      expect(res.body).toMatchObject({
        statusCode: 404,
        code: 'NOT_FOUND',
        path: `${API}/does-not-exist`,
      });
      expect(res.body.timestamp).toEqual(expect.any(String));
    });

    it('protected routes require a JWT', async () => {
      for (const route of [
        '/levels',
        '/signs',
        '/profile',
        '/progress',
        '/evaluations',
      ]) {
        const res = await request(http).get(`${API}${route}`).expect(401);
        expect(res.body.code).toBe('UNAUTHORIZED');
      }
      await request(http)
        .get(`${API}/profile`)
        .set('Authorization', 'Bearer garbage')
        .expect(401);
    });
  });

  describe('register and login', () => {
    it('validates the /auth/register body', async () => {
      const bad = (body: object) =>
        request(http).post(`${API}/auth/register`).send(body).expect(400);
      const ok = {
        email: 'a@b.com',
        password: 'Lumina2026',
        displayName: 'Ana',
      };

      for (const extra of [
        { email: 'not-an-email' },
        { password: 'short1' },
        { password: 'onlyletterss' },
        { password: '123456789' },
        { password: 'a1'.repeat(40) },
        { displayName: 'A' },
        { displayName: 'x'.repeat(41) },
      ]) {
        const res = await bad({ ...ok, ...extra });
        expect(res.body.code).toBe('VALIDATION_FAILED');
        expect(res.body.details.length).toBeGreaterThan(0);
      }
      // Undeclared properties (forbidNonWhitelisted).
      expect((await bad({ ...ok, role: 'ADMIN' })).body.code).toBe(
        'VALIDATION_FAILED',
      );
    });

    it.each([
      'luna',
      'nube',
      'rio',
      'sol',
      'carol',
      'luna-crimson',
      'tom',
      'crimson',
      'neon',
      'tensy',
    ])(
      'registers with the official avatar %s and GET /profile returns it',
      async (avatarId) => {
        const res = await request(http)
          .post(`${API}/auth/register`)
          .send({
            email: `av-${randomUUID()}@example.com`,
            password: 'Lumina2026',
            displayName: 'Ana',
            avatarId,
          })
          .expect(201);
        const profile = await request(http)
          .get(`${API}/profile`)
          .set(auth(res.body.accessToken))
          .expect(200);
        expect(profile.body.avatarId).toBe(avatarId);
      },
    );

    it.each(['unknown', 'carolina', 'luna_crimson', 'Luna', 'TOM', 'avatar'])(
      'rejects the avatar %s with INVALID_AVATAR',
      async (avatarId) => {
        const res = await request(http)
          .post(`${API}/auth/register`)
          .send({
            email: `av-${randomUUID()}@example.com`,
            password: 'Lumina2026',
            displayName: 'Ana',
            avatarId,
          })
          .expect(400);
        expect(res.body.code).toBe('INVALID_AVATAR');
      },
    );

    it('rejects an avatar that is not in AVATAR_IDS', async () => {
      const res = await request(http)
        .post(`${API}/auth/register`)
        .send({
          email: `av-${randomUUID()}@example.com`,
          password: 'Lumina2026',
          displayName: 'Ana',
          avatarId: 'dragon',
        })
        .expect(400);
      expect(res.body.code).toBe('INVALID_AVATAR');
    });

    it('registers (201), creates the profile and rejects a repeated email (409)', async () => {
      const email = `Ana-${randomUUID().slice(0, 6)}@Example.com`;
      const res = await request(http)
        .post(`${API}/auth/register`)
        .send({ email, password: 'Lumina2026', displayName: 'Ana' })
        .expect(201);
      expect(res.body).toMatchObject({
        tokenType: 'Bearer',
        expiresIn: 900,
        user: { email: email.toLowerCase(), role: 'PLAYER' },
      });
      expect(res.body.accessToken.split('.')).toHaveLength(3);
      expect(res.body).not.toHaveProperty('passwordHash');

      const profile = await request(http)
        .get(`${API}/profile`)
        .set(auth(res.body.accessToken))
        .expect(200);
      expect(profile.body).toMatchObject({
        email: email.toLowerCase(),
        displayName: 'Ana',
        avatarId: 'luna',
        totalXp: 0,
        preferences: {},
        avatarConfig: {},
      });

      const dup = await request(http)
        .post(`${API}/auth/register`)
        .send({
          email: email.toUpperCase(),
          password: 'Lumina2026',
          displayName: 'Other',
        })
        .expect(409);
      expect(dup.body.code).toBe('EMAIL_IN_USE');
    });

    it('login: correct (200) and incorrect with the same error whether or not the email exists', async () => {
      const u = await registerUser(app, 'Login');
      await request(http)
        .post(`${API}/auth/login`)
        .send({ email: u.email, password: u.password })
        .expect(200);

      const wrongPassword = await request(http)
        .post(`${API}/auth/login`)
        .send({ email: u.email, password: 'Incorrect1' })
        .expect(401);
      const unknown = await request(http)
        .post(`${API}/auth/login`)
        .send({ email: 'nobody@example.com', password: 'Incorrect1' })
        .expect(401);
      expect(wrongPassword.body.code).toBe('INVALID_CREDENTIALS');
      expect(unknown.body.code).toBe('INVALID_CREDENTIALS');
      expect(wrongPassword.body.message).toBe(unknown.body.message);
    });

    it('a disabled user cannot sign in (403) or use their token', async () => {
      const u = await registerUser(app, 'Inactive');
      await app
        .get(DataSource)
        .query(`update users set active = false where id = $1`, [u.userId]);
      const login = await request(http)
        .post(`${API}/auth/login`)
        .send({ email: u.email, password: u.password })
        .expect(403);
      expect(login.body.code).toBe('INACTIVE_USER');
      await request(http)
        .get(`${API}/profile`)
        .set(auth(u.accessToken))
        .expect(401);
    });
  });

  describe('profile', () => {
    it('PATCH /profile and /profile/avatar', async () => {
      const u = await registerUser(app, 'Profile');
      const h = auth(u.accessToken);

      const a = await request(http)
        .patch(`${API}/profile`)
        .set(h)
        .send({ displayName: '  Maria  ', preferences: { language: 'en' } })
        .expect(200);
      expect(a.body).toMatchObject({
        displayName: 'Maria',
        preferences: { language: 'en' },
      });

      const b = await request(http)
        .patch(`${API}/profile`)
        .set(h)
        .send({ preferences: { theme: 'dark' } })
        .expect(200);
      expect(b.body.preferences).toEqual({ language: 'en', theme: 'dark' });

      const c = await request(http)
        .patch(`${API}/profile/avatar`)
        .set(h)
        .send({ avatarId: 'sol', avatarConfig: { color: 'blue' } })
        .expect(200);
      expect(c.body).toMatchObject({
        avatarId: 'sol',
        avatarConfig: { color: 'blue' },
      });

      // Every official avatar can be chosen in turn (and the profile keeps the last one).
      for (const avatarId of [
        'carol',
        'luna-crimson',
        'tom',
        'crimson',
        'neon',
        'tensy',
      ]) {
        const r = await request(http)
          .patch(`${API}/profile/avatar`)
          .set(h)
          .send({ avatarId })
          .expect(200);
        expect(r.body.avatarId).toBe(avatarId);
      }
      expect(
        (
          await request(http)
            .patch(`${API}/profile/avatar`)
            .set(h)
            .send({ avatarId: 'unknown' })
            .expect(400)
        ).body.code,
      ).toBe('INVALID_AVATAR');
      expect(
        (await request(http).get(`${API}/profile`).set(h).expect(200)).body
          .avatarId,
      ).toBe('tensy');

      await request(http)
        .patch(`${API}/profile`)
        .set(h)
        .send({ displayName: 'X' })
        .expect(400);
      await request(http)
        .patch(`${API}/profile`)
        .set(h)
        .send({ preferences: { theme: 'pink' } })
        .expect(400);
      await request(http)
        .patch(`${API}/profile`)
        .set(h)
        .send({ totalXp: 9999 })
        .expect(400);
      const av = await request(http)
        .patch(`${API}/profile/avatar`)
        .set(h)
        .send({ avatarId: 'dragon' })
        .expect(400);
      expect(av.body.code).toBe('INVALID_AVATAR');
    });
  });

  describe('catalog', () => {
    it('GET /levels returns the 4 levels; only the first unlocked', async () => {
      const u = await registerUser(app, 'Levels');
      const res = await request(http)
        .get(`${API}/levels`)
        .set(auth(u.accessToken))
        .expect(200);
      expect(res.body.map((l: { code: string }) => l.code)).toEqual([
        'ALPHABET',
        'NUMBERS',
        'MONTHS',
        'GREETINGS_PHRASES',
      ]);
      expect(res.body.map((l: { unlocked: boolean }) => l.unlocked)).toEqual([
        true,
        false,
        false,
        false,
      ]);
      expect(res.body[1]).toMatchObject({
        requiredXp: 270,
        missingXp: 270,
        passedSigns: 0,
        totalSigns: 31,
        earnedStars: 0,
        possibleStars: 93,
      });
    });

    it('GET /levels/:code: signs in order with status', async () => {
      const u = await registerUser(app, 'Detail');
      const res = await request(http)
        .get(`${API}/levels/alphabet`)
        .set(auth(u.accessToken))
        .expect(200);
      expect(res.body.signs).toHaveLength(27);
      expect(res.body.signs[0]).toMatchObject({
        code: 'A',
        position: 1,
        status: 'AVAILABLE',
        bestPrecision: null,
        bestStars: 0,
      });
      expect(res.body.signs[1].status).toBe('LOCKED');
      expect(res.body.signs[14].code).toBe('Ñ');

      const nope = await request(http)
        .get(`${API}/levels/NOPE`)
        .set(auth(u.accessToken))
        .expect(404);
      expect(nope.body.code).toBe('LEVEL_NOT_FOUND');
    });

    it('GET /signs with filters and GET /signs/:code', async () => {
      const u = await registerUser(app, 'Signs');
      const h = auth(u.accessToken);
      const all = await request(http).get(`${API}/signs`).set(h).expect(200);
      expect(all.body).toHaveLength(81);
      const months = await request(http)
        .get(`${API}/signs?type=MONTH`)
        .set(h)
        .expect(200);
      expect(months.body).toHaveLength(12);
      const level = await request(http)
        .get(`${API}/signs?level=greetings_phrases`)
        .set(h)
        .expect(200);
      expect(level.body).toHaveLength(11);
      await request(http).get(`${API}/signs?type=OTHER`).set(h).expect(400);

      const one = await request(http).get(`${API}/signs/a`).set(h).expect(200);
      expect(one.body).toMatchObject({
        code: 'A',
        type: 'LETTER',
        levels: [{ level: 'ALPHABET', position: 1 }],
      });
      expect(
        (await request(http).get(`${API}/signs/NOPE`).set(h).expect(404)).body
          .code,
      ).toBe('SIGN_NOT_FOUND');
    });
  });

  describe('evaluations, progress and unlocks', () => {
    it('passed attempt → next sign unlocked → idempotent retry', async () => {
      const u = await registerUser(app, 'Game');
      const h = auth(u.accessToken);

      // B is still locked.
      const locked = await request(http)
        .post(`${API}/evaluations`)
        .set(h)
        .send(attemptBody('B'))
        .expect(403);
      expect(locked.body.code).toBe('SIGN_LOCKED');

      const body = attemptBody('A');
      const first = await request(http)
        .post(`${API}/evaluations`)
        .set(h)
        .send(body)
        .expect(201);
      expect(first.body).toMatchObject({
        attemptId: body.attemptId,
        signCode: 'A',
        passed: true,
        precision: 82,
        stars: 2,
        awardedXp: 20,
        totalXp: 20,
        bestPrecision: 82,
        bestStars: 2,
        unlockedSign: 'B',
        unlockedLevels: [],
      });
      expect(first.body.evaluationId).toEqual(expect.any(String));

      // Identical retry: 200 and exactly the same response.
      const repeated = await request(http)
        .post(`${API}/evaluations`)
        .set(h)
        .send(body)
        .expect(200);
      expect(repeated.body).toEqual(first.body);

      // Same attemptId with different content.
      const conflict = await request(http)
        .post(`${API}/evaluations`)
        .set(h)
        .send({ ...body, ...THREE_STARS })
        .expect(409);
      expect(conflict.body.code).toBe('DUPLICATE_ATTEMPT');

      // Now B is available.
      const b = await request(http)
        .post(`${API}/evaluations`)
        .set(h)
        .send(attemptBody('B', { precision: 65, stars: 1, xp: 10 }))
        .expect(201);
      expect(b.body).toMatchObject({
        awardedXp: 10,
        totalXp: 30,
        unlockedSign: 'C',
      });

      // Nothing was duplicated.
      const profile = await request(http)
        .get(`${API}/profile`)
        .set(h)
        .expect(200);
      expect(profile.body.totalXp).toBe(30);
      const history = await request(http)
        .get(`${API}/evaluations`)
        .set(h)
        .expect(200);
      expect(history.body.total).toBe(2);
    });

    it('rejects inconsistent results with 422 and saves nothing', async () => {
      const u = await registerUser(app, 'Inconsistent');
      const h = auth(u.accessToken);
      const bad = await request(http)
        .post(`${API}/evaluations`)
        .set(h)
        .send(attemptBody('A', { precision: 82, stars: 3, xp: 30 }))
        .expect(422);
      expect(bad.body.code).toBe('INCONSISTENT_RESULT');
      const history = await request(http)
        .get(`${API}/evaluations`)
        .set(h)
        .expect(200);
      expect(history.body.total).toBe(0);
    });

    it('an attempt below the threshold is saved but gives no XP and unlocks nothing', async () => {
      const u = await registerUser(app, 'Failing');
      const h = auth(u.accessToken);
      // The numbers recognizer assigns 1 star even with precision < 60.
      const res = await request(http)
        .post(`${API}/evaluations`)
        .set(h)
        .send(attemptBody('A', { precision: 40, stars: 1, xp: 10 }))
        .expect(201);
      expect(res.body).toMatchObject({
        passed: false,
        stars: 0,
        awardedXp: 0,
        totalXp: 0,
        unlockedSign: null,
        bestPrecision: 40,
      });
      const history = await request(http)
        .get(`${API}/evaluations?passed=false`)
        .set(h)
        .expect(200);
      expect(history.body.items[0]).toMatchObject({
        passed: false,
        reportedStars: 1,
        reportedXp: 10,
        awardedXp: 0,
      });
    });

    it('validates the POST /evaluations body', async () => {
      const u = await registerUser(app, 'Validation');
      const h = auth(u.accessToken);
      for (const extra of [
        { attemptId: 'not-a-uuid' },
        { precision: 100.01 },
        { precision: -0.1 },
        { precision: 82.123 },
        { precision: 820 },
        { signCode: 'A B' },
        { signCode: 'a/b' },
        { signCode: 'x'.repeat(61) },
        { precision: '0.8' },
        { stars: 4 },
        { stars: 1.5 },
        { xp: -1 },
        { durationMs: -5 },
        { feedback: 'x'.repeat(501) },
        { extra: true },
      ]) {
        const res = await request(http)
          .post(`${API}/evaluations`)
          .set(h)
          .send({ ...attemptBody('A'), ...extra })
          .expect(400);
        expect(res.body.code).toBe('VALIDATION_FAILED');
      }
      const nope = await request(http)
        .post(`${API}/evaluations`)
        .set(h)
        .send(attemptBody('MADE_UP_SIGN'))
        .expect(404);
      expect(nope.body.code).toBe('SIGN_NOT_FOUND');
    });

    it('XP without duplication: improving from 1 to 3 stars adds 20, and repeating adds nothing', async () => {
      const u = await registerUser(app, 'Improve');
      const h = auth(u.accessToken);
      const a = await request(http)
        .post(`${API}/evaluations`)
        .set(h)
        .send(attemptBody('A', { precision: 65, stars: 1, xp: 10 }))
        .expect(201);
      const b = await request(http)
        .post(`${API}/evaluations`)
        .set(h)
        .send(attemptBody('A', THREE_STARS))
        .expect(201);
      const c = await request(http)
        .post(`${API}/evaluations`)
        .set(h)
        .send(attemptBody('A', THREE_STARS))
        .expect(201);
      expect(a.body.awardedXp).toBe(10);
      expect(b.body).toMatchObject({
        awardedXp: 20,
        totalXp: 30,
        bestStars: 3,
        unlockedSign: null,
      });
      expect(c.body).toMatchObject({ awardedXp: 0, totalXp: 30 });

      const progress = await request(http)
        .get(`${API}/progress/signs/A`)
        .set(h)
        .expect(200);
      expect(progress.body).toMatchObject({
        status: 'PASSED',
        totalAttempts: 3,
        passedAttempts: 3,
        bestStars: 3,
        bestPrecision: 95,
        earnedXp: 30,
        passed: true,
      });
      expect(progress.body.firstPassedAt).toEqual(expect.any(String));
    });

    it('unlocks Numbers after all alphabet signs earn their minimum passing XP', async () => {
      const u = await registerUser(app, 'Climb');
      const h = auth(u.accessToken);
      const letters = 'ABCDEFGHIJKLMNÑOPQRSTUVWXYZ'.split('');
      let last:
        { body: { unlockedLevels: string[]; totalXp: number } } | undefined;

      // Too early: Numbers is locked.
      const early = await request(http)
        .post(`${API}/evaluations`)
        .set(h)
        .send(attemptBody('numeros.0'))
        .expect(403);
      expect(early.body.code).toBe('LEVEL_LOCKED');

      for (const [i, l] of letters.entries()) {
        last = await request(http)
          .post(`${API}/evaluations`)
          .set(h)
          .send(attemptBody(`${l}`, { precision: 65, stars: 1, xp: 10 }))
          .expect(201);
        if (i < letters.length - 1)
          expect(last.body.unlockedLevels).toEqual([]);
      }
      expect(last?.body.totalXp).toBe(270);
      expect(last?.body.unlockedLevels).toEqual(['NUMBERS']);

      const levels = await request(http)
        .get(`${API}/levels`)
        .set(h)
        .expect(200);
      expect(levels.body[1]).toMatchObject({
        code: 'NUMBERS',
        unlocked: true,
        missingXp: 0,
      });
      const detail = await request(http)
        .get(`${API}/levels/NUMBERS`)
        .set(h)
        .expect(200);
      expect(detail.body.signs[0]).toMatchObject({
        code: 'numeros.0',
        status: 'AVAILABLE',
      });
      expect(detail.body.signs[1].status).toBe('LOCKED');

      const ok = await request(http)
        .post(`${API}/evaluations`)
        .set(h)
        .send(attemptBody('numeros.0', THREE_STARS))
        .expect(201);
      expect(ok.body).toMatchObject({
        unlockedSign: 'numeros.1',
        totalXp: 330,
      });

      const summary = await request(http)
        .get(`${API}/progress`)
        .set(h)
        .expect(200);
      expect(summary.body).toMatchObject({
        totalXp: 330,
        highestUnlockedLevel: { code: 'NUMBERS' },
        nextLevel: { code: 'MONTHS', missingXp: 250 },
        passedSigns: 11,
        totalSigns: 81,
        totalStars: 33,
        totalAttempts: 11,
        passRate: 1,
      });
    });

    it('history: pagination and filters', async () => {
      const u = await registerUser(app, 'History');
      const h = auth(u.accessToken);
      await request(http)
        .post(`${API}/evaluations`)
        .set(h)
        .send(attemptBody('A', { precision: 30, stars: 0, xp: 0 }))
        .expect(201);
      await request(http)
        .post(`${API}/evaluations`)
        .set(h)
        .send(attemptBody('A'))
        .expect(201);
      await request(http)
        .post(`${API}/evaluations`)
        .set(h)
        .send(attemptBody('B', { precision: 65, stars: 1, xp: 10 }))
        .expect(201);

      const all = await request(http)
        .get(`${API}/evaluations`)
        .set(h)
        .expect(200);
      expect(all.body).toMatchObject({ total: 3, page: 1, limit: 20 });
      expect(all.body.items[0].signCode).toBe('B'); // most recent first

      const page = await request(http)
        .get(`${API}/evaluations?page=2&limit=2`)
        .set(h)
        .expect(200);
      expect(page.body).toMatchObject({ total: 3, page: 2, limit: 2 });
      expect(page.body.items).toHaveLength(1);

      expect(
        (await request(http).get(`${API}/evaluations?sign=A`).set(h)).body
          .total,
      ).toBe(2);
      expect(
        (
          await request(http)
            .get(`${API}/evaluations?level=ALPHABET&passed=true`)
            .set(h)
        ).body.total,
      ).toBe(2);
      expect(
        (await request(http).get(`${API}/evaluations?passed=false`).set(h)).body
          .total,
      ).toBe(1);
      expect(
        (await request(http).get(`${API}/evaluations?level=MONTHS`).set(h)).body
          .total,
      ).toBe(0);
      expect(
        (
          await request(http)
            .get(`${API}/evaluations?from=2999-01-01T00:00:00Z`)
            .set(h)
        ).body.total,
      ).toBe(0);
      expect(
        (
          await request(http)
            .get(`${API}/evaluations?to=2999-01-01T00:00:00Z`)
            .set(h)
        ).body.total,
      ).toBe(3);

      await request(http)
        .get(`${API}/evaluations?limit=101`)
        .set(h)
        .expect(400);
      await request(http).get(`${API}/evaluations?page=0`).set(h).expect(400);
      await request(http)
        .get(`${API}/evaluations?from=yesterday`)
        .set(h)
        .expect(400);
    });

    it('each player only sees their own progress and history', async () => {
      const a = await registerUser(app, 'One');
      const b = await registerUser(app, 'Two');
      await request(http)
        .post(`${API}/evaluations`)
        .set(auth(a.accessToken))
        .send(attemptBody('A'))
        .expect(201);
      const history = await request(http)
        .get(`${API}/evaluations`)
        .set(auth(b.accessToken))
        .expect(200);
      expect(history.body.total).toBe(0);
      const progress = await request(http)
        .get(`${API}/progress`)
        .set(auth(b.accessToken))
        .expect(200);
      expect(progress.body.totalXp).toBe(0);
    });

    it('another player can use the same attemptId (uniqueness is per user)', async () => {
      const a = await registerUser(app, 'Ana');
      const b = await registerUser(app, 'Beto');
      const body = attemptBody('A');
      await request(http)
        .post(`${API}/evaluations`)
        .set(auth(a.accessToken))
        .send(body)
        .expect(201);
      await request(http)
        .post(`${API}/evaluations`)
        .set(auth(b.accessToken))
        .send(body)
        .expect(201);
    });
  });

  describe('concurrency', () => {
    it('10 simultaneous submissions of the same attempt: only one is created and XP is not duplicated', async () => {
      const u = await registerUser(app, 'Race');
      const h = auth(u.accessToken);
      const body = attemptBody('A');
      const responses = await Promise.all(
        Array.from({ length: 10 }, () =>
          request(http).post(`${API}/evaluations`).set(h).send(body),
        ),
      );
      const statuses = responses.map((r) => r.status).sort();
      expect(statuses.filter((s) => s === 201)).toHaveLength(1);
      expect(statuses.filter((s) => s === 200)).toHaveLength(9);
      // All responses have the same content (jsonb reorders the keys).
      for (const r of responses) expect(r.body).toEqual(responses[0].body);

      const profile = await request(http)
        .get(`${API}/profile`)
        .set(h)
        .expect(200);
      expect(profile.body.totalXp).toBe(20);
      const progress = await request(http)
        .get(`${API}/progress/signs/A`)
        .set(h)
        .expect(200);
      expect(progress.body.totalAttempts).toBe(1);
    });

    it('simultaneous different submissions of the same sign never exceed 30 XP', async () => {
      const u = await registerUser(app, 'Parallel');
      const h = auth(u.accessToken);
      const attempts = [
        { precision: 65, stars: 1, xp: 10 },
        { precision: 80, stars: 2, xp: 20 },
        { precision: 95, stars: 3, xp: 30 },
        { precision: 95, stars: 3, xp: 30 },
        { precision: 70, stars: 1, xp: 10 },
      ];
      const res = await Promise.all(
        attempts.map((a) =>
          request(http)
            .post(`${API}/evaluations`)
            .set(h)
            .send(attemptBody('A', a)),
        ),
      );
      expect(res.every((r) => r.status === 201)).toBe(true);
      expect(res.reduce((s, r) => s + r.body.awardedXp, 0)).toBe(30);
      const profile = await request(http)
        .get(`${API}/profile`)
        .set(h)
        .expect(200);
      expect(profile.body.totalXp).toBe(30);
      const progress = await request(http)
        .get(`${API}/progress/signs/A`)
        .set(h)
        .expect(200);
      expect(progress.body).toMatchObject({ earnedXp: 30, totalAttempts: 5 });
    });
  });

  describe('sessions', () => {
    it('refresh rotates the tokens and detects reuse', async () => {
      const u = await registerUser(app, 'Session');

      const r1 = await request(http)
        .post(`${API}/auth/refresh`)
        .send({ refreshToken: u.refreshToken })
        .expect(200);
      expect(r1.body.refreshToken).not.toBe(u.refreshToken);
      await request(http)
        .get(`${API}/profile`)
        .set(auth(r1.body.accessToken))
        .expect(200);

      const r2 = await request(http)
        .post(`${API}/auth/refresh`)
        .send({ refreshToken: r1.body.refreshToken })
        .expect(200);

      // Reusing the first refresh token (already rotated) revokes every session.
      const reuse = await request(http)
        .post(`${API}/auth/refresh`)
        .send({ refreshToken: u.refreshToken })
        .expect(401);
      expect(reuse.body.code).toBe('INVALID_SESSION');
      await request(http)
        .get(`${API}/profile`)
        .set(auth(r2.body.accessToken))
        .expect(401);
      await request(http)
        .post(`${API}/auth/refresh`)
        .send({ refreshToken: r2.body.refreshToken })
        .expect(401);

      // And the user can sign in normally again.
      const login = await request(http)
        .post(`${API}/auth/login`)
        .send({ email: u.email, password: u.password })
        .expect(200);
      await request(http)
        .get(`${API}/profile`)
        .set(auth(login.body.accessToken))
        .expect(200);
    });

    it('refresh with malformed or fake tokens → 401', async () => {
      for (const refreshToken of ['garbage', `${randomUUID()}.fake`, '.']) {
        const res = await request(http)
          .post(`${API}/auth/refresh`)
          .send({ refreshToken })
          .expect(401);
        expect(res.body.code).toBe('INVALID_SESSION');
      }
      await request(http).post(`${API}/auth/refresh`).send({}).expect(400);
    });

    it('logout revokes the current session immediately (the access token stops working)', async () => {
      const u = await registerUser(app, 'Logout');
      const other = await request(http)
        .post(`${API}/auth/login`)
        .send({ email: u.email, password: u.password })
        .expect(200);

      await request(http)
        .post(`${API}/auth/logout`)
        .set(auth(u.accessToken))
        .expect(204);
      const rejected = await request(http)
        .get(`${API}/profile`)
        .set(auth(u.accessToken))
        .expect(401);
      expect(rejected.body.code).toBe('INVALID_SESSION');

      // The other session is still alive.
      await request(http)
        .get(`${API}/profile`)
        .set(auth(other.body.accessToken))
        .expect(200);

      // The closed session's refresh token no longer works; presenting it counts
      // as reuse of a revoked session and revokes all the others.
      await request(http)
        .post(`${API}/auth/refresh`)
        .send({ refreshToken: u.refreshToken })
        .expect(401);
      await request(http)
        .get(`${API}/profile`)
        .set(auth(other.body.accessToken))
        .expect(401);
    });

    it('logout-all closes every session', async () => {
      const u = await registerUser(app, 'LogoutAll');
      const other = await request(http)
        .post(`${API}/auth/login`)
        .send({ email: u.email, password: u.password })
        .expect(200);
      await request(http)
        .post(`${API}/auth/logout-all`)
        .set(auth(u.accessToken))
        .expect(204);
      await request(http)
        .get(`${API}/profile`)
        .set(auth(u.accessToken))
        .expect(401);
      await request(http)
        .get(`${API}/profile`)
        .set(auth(other.body.accessToken))
        .expect(401);
    });

    it('stores only the SHA-256 hash of the refresh token', async () => {
      const u = await registerUser(app, 'Hash');
      const [sessionId, secret] = u.refreshToken.split('.');
      const [row] = await app
        .get(DataSource)
        .query(`select refresh_token_hash from sessions where id = $1`, [
          sessionId,
        ]);
      expect(row.refresh_token_hash).toMatch(/^[0-9a-f]{64}$/);
      expect(row.refresh_token_hash).not.toContain(secret);
    });
  });
});
