/**
 * Límites de intentos por IP y por ventana, para los endpoints de autenticación.
 * Variables (todas opcionales): RATE_LIMIT_TTL_SECONDS, RATE_LIMIT_LOGIN,
 * RATE_LIMIT_REGISTER, RATE_LIMIT_REFRESH.
 */
export interface RateLimitConfig {
  /** Duración de la ventana, en milisegundos (el throttler trabaja en ms). */
  ttlMs: number;
  login: number;
  register: number;
  refresh: number;
}

const DEFAULTS = { RATE_LIMIT_TTL_SECONDS: 60, RATE_LIMIT_LOGIN: 10, RATE_LIMIT_REGISTER: 5, RATE_LIMIT_REFRESH: 30 } as const;
const MAX_VALUE = 1_000_000;

type Key = keyof typeof DEFAULTS;
const KEYS = Object.keys(DEFAULTS) as Key[];

function parse(env: Record<string, unknown>, key: Key): number | null {
  const raw = env[key];
  if (raw === undefined || raw === null || String(raw).trim() === '') return DEFAULTS[key];
  const text = String(raw).trim();
  if (!/^\d+$/.test(text)) return null;
  const value = Number(text);
  return value >= 1 && value <= MAX_VALUE ? value : null;
}

/** Errores de configuración (vacío si todo es válido). Nunca incluye los valores. */
export function validateRateLimitEnv(env: Record<string, unknown>): string[] {
  return KEYS.filter((key) => parse(env, key) === null).map(
    (key) => `${key} debe ser un entero entre 1 y ${MAX_VALUE}.`,
  );
}

/** Lee la configuración vigente; los valores inválidos caen al valor por defecto. */
export function readRateLimitConfig(env: Record<string, unknown> = process.env): RateLimitConfig {
  const get = (key: Key) => parse(env, key) ?? DEFAULTS[key];
  return {
    ttlMs: get('RATE_LIMIT_TTL_SECONDS') * 1000,
    login: get('RATE_LIMIT_LOGIN'),
    register: get('RATE_LIMIT_REGISTER'),
    refresh: get('RATE_LIMIT_REFRESH'),
  };
}
