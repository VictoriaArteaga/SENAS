import { parseDurationToSeconds } from '../common/duration';
import { validateEnv } from './env.validation';

const SECRET = 'q8Zr2LxV7mT4pN1cB6yH9wKd3sFgJa5eUo0iXvRb';
const valid = {
  DATABASE_URL: 'postgresql://u:p@127.0.0.1:5432/db',
  JWT_ACCESS_SECRET: SECRET,
};

describe('validateEnv', () => {
  it('acepta una configuración válida y aplica valores por defecto', () => {
    const env = validateEnv({ ...valid });
    expect(env).toMatchObject({
      NODE_ENV: 'development',
      PORT: '3000',
      JWT_ACCESS_EXPIRES_IN: '15m',
      REFRESH_TOKEN_EXPIRES_IN: '30d',
    });
  });

  it('falla si falta JWT_ACCESS_SECRET', () => {
    expect(() => validateEnv({ DATABASE_URL: valid.DATABASE_URL })).toThrow(/JWT_ACCESS_SECRET es obligatoria/);
  });

  it('falla si JWT_ACCESS_SECRET es corta', () => {
    expect(() => validateEnv({ ...valid, JWT_ACCESS_SECRET: 'abc123' })).toThrow(/al menos 32/);
  });

  it.each([
    'CHANGE_ME_CHANGE_ME_CHANGE_ME_CHANGE_ME',
    'your-secret-your-secret-your-secret-0000',
    'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa',
    'password-password-password-password-1234',
  ])('rechaza secretos de ejemplo o poco aleatorios (%s)', (secret) => {
    expect(() => validateEnv({ ...valid, JWT_ACCESS_SECRET: secret })).toThrow(/ejemplo o poco aleatorio/);
  });

  it('falla si falta DATABASE_URL o no es postgresql://', () => {
    expect(() => validateEnv({ JWT_ACCESS_SECRET: SECRET })).toThrow(/DATABASE_URL es obligatoria/);
    expect(() => validateEnv({ ...valid, DATABASE_URL: 'mysql://x' })).toThrow(/postgresql/);
  });

  it('exige un access token de corta duración y un refresh mayor', () => {
    expect(() => validateEnv({ ...valid, JWT_ACCESS_EXPIRES_IN: '2h' })).toThrow(/no puede superar 1h/);
    expect(() => validateEnv({ ...valid, JWT_ACCESS_EXPIRES_IN: 'quince' })).toThrow(/formato/);
    expect(() => validateEnv({ ...valid, REFRESH_TOKEN_EXPIRES_IN: '10m' })).toThrow(/mayor que JWT_ACCESS_EXPIRES_IN/);
    expect(() => validateEnv({ ...valid, REFRESH_TOKEN_EXPIRES_IN: '365d' })).toThrow(/no puede superar 90d/);
  });

  it('valida PORT y NODE_ENV', () => {
    expect(() => validateEnv({ ...valid, PORT: '99999' })).toThrow(/PORT/);
    expect(() => validateEnv({ ...valid, NODE_ENV: 'staging' })).toThrow(/NODE_ENV/);
  });

  it('en producción no admite MIGRATION_DATABASE_URL, en desarrollo sí', () => {
    const migration = { MIGRATION_DATABASE_URL: 'postgresql://m:m@127.0.0.1:5432/db' };
    expect(() => validateEnv({ ...valid, ...migration, NODE_ENV: 'production' })).toThrow(/MIGRATION_DATABASE_URL/);
    expect(() => validateEnv({ ...valid, ...migration, NODE_ENV: 'development' })).not.toThrow();
  });

  it('los mensajes de error nunca incluyen el valor del secreto ni de la URL', () => {
    let message = '';
    try {
      validateEnv({ DATABASE_URL: 'postgresql://usuario:clave-super-privada@h/db', JWT_ACCESS_SECRET: 'corta-secreta' });
    } catch (e) {
      message = (e as Error).message;
    }
    expect(message).not.toContain('corta-secreta');
    expect(message).not.toContain('clave-super-privada');
  });
});

describe('parseDurationToSeconds', () => {
  it('convierte unidades y rechaza formatos inválidos', () => {
    expect(parseDurationToSeconds('15m')).toBe(900);
    expect(parseDurationToSeconds('30d')).toBe(2_592_000);
    expect(parseDurationToSeconds('90s')).toBe(90);
    expect(parseDurationToSeconds('1h')).toBe(3600);
    for (const bad of ['', '15', 'm', '-5m', '1.5h', '0m', 15, null, undefined]) {
      expect(parseDurationToSeconds(bad)).toBeNull();
    }
  });
});
