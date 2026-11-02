import { Env, validateEnv } from './env.validation';

export const APP_CONFIG = Symbol('APP_CONFIG');

export interface ScoringRules {
  /** Minimum precision (0–100 scale) to pass. */
  passThreshold: number;
  twoStars: number;
  threeStars: number;
  xpPerStar: number;
}

export interface AppConfig {
  nodeEnv: Env['NODE_ENV'];
  port: number;
  apiPrefix: string;
  swaggerEnabled: boolean;
  corsOrigins: string[];
  db: {
    host: string;
    port: number;
    database: string;
    user: string;
    password: string;
    ssl: boolean;
    poolMax: number;
    synchronize: boolean;
    seedOnStart: boolean;
  };
  auth: {
    accessSecret: string;
    accessTtl: string;
    accessTtlSeconds: number;
    refreshTtlDays: number;
  };
  scoring: ScoringRules;
  avatarIds: string[];
  throttle: { ttlMs: number; limit: number; authLimit: number };
}

const UNIT_SECONDS = { s: 1, m: 60, h: 3600, d: 86400 } as const;

/** Converts "15m", "1h", "30s" or "7d" to seconds. */
export function ttlToSeconds(ttl: string): number {
  const m = /^(\d+)([smhd])$/.exec(ttl);
  if (!m) throw new Error(`Invalid TTL: ${ttl}`);
  return Number(m[1]) * UNIT_SECONDS[m[2] as keyof typeof UNIT_SECONDS];
}

export function buildConfig(env: Env): AppConfig {
  // DB_URL was already validated: no credentials, with host and database.
  const url = new URL(env.DB_URL);
  return {
    nodeEnv: env.NODE_ENV,
    port: env.PORT,
    apiPrefix: env.API_PREFIX.replace(/^\/+|\/+$/g, ''),
    swaggerEnabled: env.SWAGGER_ENABLED,
    corsOrigins: env.CORS_ORIGINS.split(',')
      .map((o) => o.trim())
      .filter(Boolean),
    db: {
      host: url.hostname,
      port: url.port ? Number(url.port) : 5432,
      database: decodeURIComponent(url.pathname.slice(1)),
      user: env.DB_USER,
      password: env.DB_PASSWORD,
      ssl: env.DB_SSL,
      poolMax: env.DB_POOL_MAX,
      synchronize: env.DB_SYNCHRONIZE,
      seedOnStart: env.DB_SEED_ON_START,
    },
    auth: {
      accessSecret: env.JWT_ACCESS_SECRET,
      accessTtl: env.JWT_ACCESS_TTL,
      accessTtlSeconds: ttlToSeconds(env.JWT_ACCESS_TTL),
      refreshTtlDays: env.REFRESH_TOKEN_TTL_DAYS,
    },
    scoring: {
      passThreshold: env.SCORE_PASS_THRESHOLD,
      twoStars: env.SCORE_TWO_STARS_THRESHOLD,
      threeStars: env.SCORE_THREE_STARS_THRESHOLD,
      xpPerStar: env.XP_PER_STAR,
    },
    avatarIds: env.AVATAR_IDS.split(',')
      .map((a) => a.trim())
      .filter(Boolean),
    throttle: {
      ttlMs: env.THROTTLE_TTL_MS,
      limit: env.THROTTLE_LIMIT,
      authLimit: env.THROTTLE_AUTH_LIMIT,
    },
  };
}

/** Loads the configuration from process.env (already populated by ConfigModule from .env). */
export function loadConfig(): AppConfig {
  return buildConfig(validateEnv(process.env));
}
