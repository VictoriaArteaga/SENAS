import { parseDurationToSeconds } from '../common/duration';
import { validateRateLimitEnv } from './rate-limit.config';

export type NodeEnv = 'development' | 'production' | 'test';

const MIN_SECRET_LENGTH = 32;
const MAX_ACCESS_SECONDS = 60 * 60; // el access token debe ser de corta duración
const MAX_REFRESH_SECONDS = 90 * 24 * 60 * 60;
const PLACEHOLDER_PATTERN = /change[_-]?me|changeit|replace[_-]?me|your[_-]?secret|example|password|secret[_-]?key|^(.)\1+$/i;

/**
 * Valida las variables de entorno al arrancar (se pasa a ConfigModule.forRoot({ validate })).
 * Lanza un Error si falta algo o es inseguro: el servidor no arranca.
 * Los mensajes nunca incluyen los valores de las variables.
 */
export function validateEnv(config: Record<string, unknown>): Record<string, unknown> {
  const errors: string[] = [];
  const str = (key: string): string | undefined => {
    const value = config[key];
    return typeof value === 'string' && value.trim() !== '' ? value.trim() : undefined;
  };

  const nodeEnv = (str('NODE_ENV') ?? 'development') as NodeEnv;
  if (!['development', 'production', 'test'].includes(nodeEnv)) {
    errors.push('NODE_ENV debe ser development, production o test.');
  }

  const databaseUrl = str('DATABASE_URL');
  if (!databaseUrl) errors.push('DATABASE_URL es obligatoria.');
  else if (!/^postgres(ql)?:\/\//.test(databaseUrl)) errors.push('DATABASE_URL debe ser una URL postgresql://.');

  const secret = config['JWT_ACCESS_SECRET'];
  if (typeof secret !== 'string' || secret === '') {
    errors.push('JWT_ACCESS_SECRET es obligatoria.');
  } else if (secret.length < MIN_SECRET_LENGTH) {
    errors.push(`JWT_ACCESS_SECRET debe tener al menos ${MIN_SECRET_LENGTH} caracteres.`);
  } else if (PLACEHOLDER_PATTERN.test(secret) || new Set(secret).size < 10) {
    errors.push('JWT_ACCESS_SECRET parece un valor de ejemplo o poco aleatorio; genera uno con un generador criptográfico.');
  }

  const accessExpires = str('JWT_ACCESS_EXPIRES_IN') ?? '15m';
  const accessSeconds = parseDurationToSeconds(accessExpires);
  if (accessSeconds === null) errors.push('JWT_ACCESS_EXPIRES_IN debe tener formato <número><s|m|h|d>, p. ej. 15m.');
  else if (accessSeconds > MAX_ACCESS_SECONDS) errors.push('JWT_ACCESS_EXPIRES_IN no puede superar 1h (el access token es de corta duración).');

  const refreshExpires = str('REFRESH_TOKEN_EXPIRES_IN') ?? '30d';
  const refreshSeconds = parseDurationToSeconds(refreshExpires);
  if (refreshSeconds === null) errors.push('REFRESH_TOKEN_EXPIRES_IN debe tener formato <número><s|m|h|d>, p. ej. 30d.');
  else {
    if (refreshSeconds > MAX_REFRESH_SECONDS) errors.push('REFRESH_TOKEN_EXPIRES_IN no puede superar 90d.');
    if (accessSeconds !== null && refreshSeconds <= accessSeconds) errors.push('REFRESH_TOKEN_EXPIRES_IN debe ser mayor que JWT_ACCESS_EXPIRES_IN.');
  }

  const port = str('PORT') ?? '3000';
  if (!/^\d+$/.test(port) || Number(port) < 1 || Number(port) > 65535) errors.push('PORT debe ser un entero entre 1 y 65535.');

  errors.push(...validateRateLimitEnv(config));

  // La URL administrativa solo existe donde se despliegan migraciones, nunca en el proceso de la API.
  if (nodeEnv === 'production' && str('MIGRATION_DATABASE_URL')) {
    errors.push('MIGRATION_DATABASE_URL no debe estar definida en el entorno de ejecución de producción.');
  }

  if (errors.length > 0) {
    throw new Error(`Configuración de entorno inválida:\n - ${errors.join('\n - ')}`);
  }

  return {
    ...config,
    NODE_ENV: nodeEnv,
    PORT: port,
    JWT_ACCESS_EXPIRES_IN: accessExpires,
    REFRESH_TOKEN_EXPIRES_IN: refreshExpires,
  };
}
