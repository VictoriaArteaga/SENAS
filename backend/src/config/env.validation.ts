import Joi from 'joi';

export interface Env {
  NODE_ENV: 'development' | 'production' | 'test';
  PORT: number;
  API_PREFIX: string;
  SWAGGER_ENABLED: boolean;
  CORS_ORIGINS: string;
  DB_URL: string;
  DB_USER: string;
  DB_PASSWORD: string;
  DB_SSL: boolean;
  DB_POOL_MAX: number;
  DB_SYNCHRONIZE: boolean;
  DB_SEED_ON_START: boolean;
  JWT_ACCESS_SECRET: string;
  JWT_ACCESS_TTL: string;
  REFRESH_TOKEN_TTL_DAYS: number;
  SCORE_PASS_THRESHOLD: number;
  SCORE_TWO_STARS_THRESHOLD: number;
  SCORE_THREE_STARS_THRESHOLD: number;
  XP_PER_STAR: number;
  AVATAR_IDS: string;
  THROTTLE_TTL_MS: number;
  THROTTLE_LIMIT: number;
  THROTTLE_AUTH_LIMIT: number;
}

/**
 * Validates DB_URL without ever copying its value into error messages: a
 * malformed URL may carry credentials and those must not reach the logs.
 */
function validateDbUrl(
  value: string,
  helpers: Joi.CustomHelpers,
): string | Joi.ErrorReport {
  let url: URL;
  try {
    url = new URL(value);
  } catch {
    return helpers.error('any.invalid', {
      message: 'must be a valid URL (postgresql://host:port/database)',
    });
  }
  if (url.protocol !== 'postgresql:' && url.protocol !== 'postgres:') {
    return helpers.error('any.invalid', {
      message: 'must start with postgresql://',
    });
  }
  if (url.username || url.password) {
    return helpers.error('any.invalid', {
      message:
        'must not contain a user or password (use DB_USER and DB_PASSWORD)',
    });
  }
  if (url.searchParams.has('sslmode')) {
    return helpers.error('any.invalid', {
      message: 'must not include sslmode (SSL is controlled with DB_SSL)',
    });
  }
  if (!url.hostname || url.pathname.length <= 1) {
    return helpers.error('any.invalid', {
      message: 'must include a host and a database name',
    });
  }
  return value;
}

const envSchema = Joi.object<Env>({
  NODE_ENV: Joi.string()
    .valid('development', 'production', 'test')
    .default('development'),
  PORT: Joi.number().port().default(3000),
  API_PREFIX: Joi.string().default('api/v1'),
  SWAGGER_ENABLED: Joi.boolean().default(true),
  CORS_ORIGINS: Joi.string().allow('').default(''),

  DB_URL: Joi.string()
    .required()
    .custom(validateDbUrl)
    .messages({ 'any.invalid': '{{#message}}' }),
  DB_USER: Joi.string().required(),
  DB_PASSWORD: Joi.string().required(),
  DB_SSL: Joi.boolean().default(true),
  DB_POOL_MAX: Joi.number().integer().min(1).max(50).default(5),
  DB_SYNCHRONIZE: Joi.boolean().default(true),
  DB_SEED_ON_START: Joi.boolean().default(true),

  JWT_ACCESS_SECRET: Joi.string().min(32).required(),
  JWT_ACCESS_TTL: Joi.string()
    .pattern(/^\d+[smhd]$/)
    .default('15m')
    .messages({
      'string.pattern.base': 'must have the format <number><s|m|h|d>, e.g. 15m',
    }),
  REFRESH_TOKEN_TTL_DAYS: Joi.number().integer().min(1).default(30),

  SCORE_PASS_THRESHOLD: Joi.number().default(60),
  SCORE_TWO_STARS_THRESHOLD: Joi.number().default(74),
  SCORE_THREE_STARS_THRESHOLD: Joi.number().default(88),
  XP_PER_STAR: Joi.number().integer().min(1).max(10).default(10).messages({
    'number.max': 'cannot exceed 10 (sign_progress.earned_xp has CHECK 0..30)',
  }),
  AVATAR_IDS: Joi.string().default('luna,nube,rio,sol'),

  THROTTLE_TTL_MS: Joi.number().integer().min(1).default(60000),
  THROTTLE_LIMIT: Joi.number().integer().min(1).default(100),
  THROTTLE_AUTH_LIMIT: Joi.number().integer().min(1).default(5),
}).custom((env: Env, helpers) => {
  const pass = env.SCORE_PASS_THRESHOLD;
  const two = env.SCORE_TWO_STARS_THRESHOLD;
  const three = env.SCORE_THREE_STARS_THRESHOLD;
  if (!(0 < pass && pass < two && two < three && three <= 100)) {
    return helpers.error('any.invalid', {
      message:
        'requires 0 < SCORE_PASS_THRESHOLD < SCORE_TWO_STARS_THRESHOLD < SCORE_THREE_STARS_THRESHOLD <= 100 (precision is on a 0–100 scale)',
    });
  }
  if (env.AVATAR_IDS.split(',').every((id) => id.trim() === '')) {
    return helpers.error('any.invalid', {
      message: 'AVATAR_IDS must have at least one identifier',
    });
  }
  return env;
});

/** Validates and normalizes the environment; throws if a variable is missing or invalid. */
export function validateEnv(raw: Record<string, unknown>): Env {
  const { error, value } = envSchema.validate(raw, {
    abortEarly: false,
    allowUnknown: true,
  });
  if (error) {
    // Always the variable name, never its value.
    const detail = error.details
      .map((d) => {
        const text = String(d.context?.['message'] ?? d.message);
        const variable = d.path.join('.');
        return variable && !text.includes(variable)
          ? `${variable}: ${text}`
          : text;
      })
      .join('; ');
    throw new Error(`Invalid environment configuration: ${detail}`);
  }
  return value as Env;
}
