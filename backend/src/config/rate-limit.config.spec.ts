import { readRateLimitConfig, validateRateLimitEnv } from './rate-limit.config';
import { validateEnv } from './env.validation';

const base = { DATABASE_URL: 'postgresql://u:p@127.0.0.1:5432/db', JWT_ACCESS_SECRET: 'q8Zr2LxV7mT4pN1cB6yH9wKd3sFgJa5eUo0iXvRb' };

describe('rate limit config', () => {
  it('valores por defecto razonables', () => {
    expect(readRateLimitConfig({})).toEqual({ ttlMs: 60_000, login: 10, register: 5, refresh: 30 });
  });

  it('se configura por variables de entorno', () => {
    expect(
      readRateLimitConfig({ RATE_LIMIT_TTL_SECONDS: '120', RATE_LIMIT_LOGIN: '3', RATE_LIMIT_REGISTER: '2', RATE_LIMIT_REFRESH: '7' }),
    ).toEqual({ ttlMs: 120_000, login: 3, register: 2, refresh: 7 });
  });

  it.each(['0', '-1', '1.5', 'diez', '1000001', '1e3'])('valor inválido %s: error de validación y cae al valor por defecto', (v) => {
    expect(validateRateLimitEnv({ RATE_LIMIT_LOGIN: v })).toHaveLength(1);
    expect(readRateLimitConfig({ RATE_LIMIT_LOGIN: v }).login).toBe(10);
  });

  it('validateEnv rechaza límites inválidos y acepta los válidos', () => {
    expect(() => validateEnv({ ...base, RATE_LIMIT_LOGIN: 'muchos' })).toThrow(/RATE_LIMIT_LOGIN/);
    expect(() => validateEnv({ ...base, RATE_LIMIT_LOGIN: '15', RATE_LIMIT_TTL_SECONDS: '30' })).not.toThrow();
  });

  it('los mensajes no incluyen el valor recibido', () => {
    expect(validateRateLimitEnv({ RATE_LIMIT_LOGIN: 'valor-secreto-123' }).join()).not.toContain('valor-secreto-123');
  });
});
