import { buildConfig, ttlToSeconds } from './configuration';
import { validateEnv } from './env.validation';

const base = {
  DB_URL: 'postgresql://aws-0-us-east-1.pooler.supabase.com:5432/postgres',
  DB_USER: 'postgres.abc',
  DB_PASSWORD: 'Pw!Test*123',
  JWT_ACCESS_SECRET: 'x'.repeat(32),
};

describe('validateEnv', () => {
  it('applies the default values', () => {
    const env = validateEnv(base);
    expect(env).toMatchObject({
      NODE_ENV: 'development',
      PORT: 3000,
      API_PREFIX: 'api/v1',
      DB_SSL: true,
      DB_POOL_MAX: 5,
      DB_SYNCHRONIZE: true,
      DB_SEED_ON_START: true,
      JWT_ACCESS_TTL: '15m',
      REFRESH_TOKEN_TTL_DAYS: 30,
      SCORE_PASS_THRESHOLD: 60,
      SCORE_TWO_STARS_THRESHOLD: 74,
      SCORE_THREE_STARS_THRESHOLD: 88,
      XP_PER_STAR: 10,
      AVATAR_IDS: 'luna,nube,rio,sol,carol,luna-crimson,tom,crimson,neon,tensy',
      THROTTLE_AUTH_LIMIT: 5,
    });
  });

  it('by default accepts exactly the 10 official avatar ids (4 legacy + 6 AVATARS2)', () => {
    expect(buildConfig(validateEnv(base)).avatarIds).toEqual([
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
    ]);
  });

  it('converts types from strings (as they arrive in process.env)', () => {
    const env = validateEnv({
      ...base,
      PORT: '4000',
      DB_SSL: 'false',
      SCORE_PASS_THRESHOLD: '50',
    });
    expect(env.PORT).toBe(4000);
    expect(env.DB_SSL).toBe(false);
    expect(env.SCORE_PASS_THRESHOLD).toBe(50);
  });

  it.each(['DB_URL', 'DB_USER', 'DB_PASSWORD', 'JWT_ACCESS_SECRET'])(
    'if %s is missing the app does not start',
    (key) => {
      const without: Record<string, unknown> = { ...base };
      delete without[key];
      expect(() => validateEnv(without)).toThrow(key);
    },
  );

  it('rejects an empty required variable (DB_PASSWORD=)', () => {
    expect(() => validateEnv({ ...base, DB_PASSWORD: '' })).toThrow(
      'DB_PASSWORD',
    );
  });

  it('JWT_ACCESS_SECRET must have at least 32 characters', () => {
    expect(() => validateEnv({ ...base, JWT_ACCESS_SECRET: 'short' })).toThrow(
      'JWT_ACCESS_SECRET',
    );
    expect(() =>
      validateEnv({ ...base, JWT_ACCESS_SECRET: 'x'.repeat(31) }),
    ).toThrow();
  });

  describe('DB_URL', () => {
    it.each([
      ['a user', 'postgresql://postgres:pw@host:5432/postgres'],
      ['a user without password', 'postgresql://postgres@host:5432/postgres'],
      ['sslmode', 'postgresql://host:5432/postgres?sslmode=require'],
      ['the wrong scheme', 'mysql://host:5432/postgres'],
      ['no database', 'postgresql://host:5432'],
      ['not a URL', 'not-a-url'],
    ])('rejects a URL with %s', (_name, url) => {
      expect(() => validateEnv({ ...base, DB_URL: url })).toThrow('DB_URL');
    });

    it('the error does not copy the URL (it could carry credentials)', () => {
      let message = '';
      try {
        validateEnv({
          ...base,
          DB_URL: 'postgresql://postgres:SuperSecret@host:5432/postgres',
        });
      } catch (e) {
        message = (e as Error).message;
      }
      expect(message).toContain('DB_URL');
      expect(message).not.toContain('SuperSecret');
    });
  });

  describe('score thresholds', () => {
    it('requires 0 < pass < two stars < three stars <= 1', () => {
      const invalid = (extra: Record<string, unknown>) =>
        expect(() => validateEnv({ ...base, ...extra })).toThrow(
          'SCORE_PASS_THRESHOLD',
        );
      invalid({ SCORE_PASS_THRESHOLD: 0 });
      invalid({ SCORE_PASS_THRESHOLD: 80 });
      invalid({ SCORE_TWO_STARS_THRESHOLD: 60 });
      invalid({ SCORE_TWO_STARS_THRESHOLD: 90 });
      invalid({ SCORE_THREE_STARS_THRESHOLD: 100.01 });
      // The old 0–1 scale is no longer valid.
      invalid({
        SCORE_PASS_THRESHOLD: 0.6,
        SCORE_TWO_STARS_THRESHOLD: 0.74,
        SCORE_THREE_STARS_THRESHOLD: 101,
      });
    });

    it('accepts three stars = 100', () => {
      expect(
        validateEnv({ ...base, SCORE_THREE_STARS_THRESHOLD: 100 })
          .SCORE_THREE_STARS_THRESHOLD,
      ).toBe(100);
    });
  });

  it('XP_PER_STAR cannot exceed 10 (CHECK earned_xp <= 30)', () => {
    expect(() => validateEnv({ ...base, XP_PER_STAR: 11 })).toThrow(
      'XP_PER_STAR',
    );
  });

  it('rejects a TTL with an invalid format', () => {
    expect(() => validateEnv({ ...base, JWT_ACCESS_TTL: 'fifteen' })).toThrow(
      'JWT_ACCESS_TTL',
    );
  });

  it('rejects AVATAR_IDS with no identifiers', () => {
    expect(() => validateEnv({ ...base, AVATAR_IDS: ' , ' })).toThrow(
      'AVATAR_IDS',
    );
  });
});

describe('buildConfig', () => {
  it('splits host, port and database from DB_URL and keeps user/password as separate fields', () => {
    const cfg = buildConfig(validateEnv(base));
    expect(cfg.db).toMatchObject({
      host: 'aws-0-us-east-1.pooler.supabase.com',
      port: 5432,
      database: 'postgres',
      user: 'postgres.abc',
      password: 'Pw!Test*123',
      ssl: true,
    });
  });

  it('uses port 5432 if the URL does not give one', () => {
    const cfg = buildConfig(
      validateEnv({ ...base, DB_URL: 'postgresql://host/base' }),
    );
    expect(cfg.db.port).toBe(5432);
  });

  it('parses lists, prefix and game rules', () => {
    const cfg = buildConfig(
      validateEnv({
        ...base,
        API_PREFIX: '/api/v1/',
        CORS_ORIGINS: 'http://a.com, http://b.com,',
        AVATAR_IDS: 'luna, sol',
      }),
    );
    expect(cfg.apiPrefix).toBe('api/v1');
    expect(cfg.corsOrigins).toEqual(['http://a.com', 'http://b.com']);
    expect(cfg.avatarIds).toEqual(['luna', 'sol']);
    expect(cfg.scoring).toEqual({
      passThreshold: 60,
      twoStars: 74,
      threeStars: 88,
      xpPerStar: 10,
    });
    expect(cfg.auth.accessTtlSeconds).toBe(900);
  });

  it('empty CORS means disabled', () => {
    expect(buildConfig(validateEnv(base)).corsOrigins).toEqual([]);
  });
});

describe('ttlToSeconds', () => {
  it.each([
    ['30s', 30],
    ['15m', 900],
    ['2h', 7200],
    ['1d', 86400],
  ])('%s → %i s', (ttl, seconds) => expect(ttlToSeconds(ttl)).toBe(seconds));

  it('rejects invalid formats', () => {
    expect(() => ttlToSeconds('15')).toThrow();
  });
});
