import { createHash, randomBytes, randomUUID, timingSafeEqual } from 'node:crypto';

/**
 * Formato del refresh token: `<sessionId>.<secreto>`.
 *  - sessionId: UUID de la fila de `sesiones`. NO es un secreto; solo permite localizar la sesión
 *    (incluso si se presenta un token antiguo) sin recorrer la tabla.
 *  - secreto: 48 bytes aleatorios (384 bits) en base64url. Toda la seguridad está aquí.
 * En la base solo se guarda el SHA-256 del token completo.
 */
const SECRET_BYTES = 48;
const UUID = '[0-9a-f]{8}-[0-9a-f]{4}-[1-8][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}';
const UUID_PATTERN = new RegExp(`^${UUID}$`);
// 43 caracteres base64url = 32 bytes: el mínimo aceptado; los emitidos tienen 64.
const TOKEN_PATTERN = new RegExp(`^(${UUID})\\.([A-Za-z0-9_-]{43,128})$`);

export const REFRESH_TOKEN_MAX_LENGTH = 200;

export interface IssuedRefreshToken {
  sessionId: string;
  /** Token completo que se entrega al cliente una sola vez. */
  token: string;
  /** SHA-256 hexadecimal del token completo: lo único que se persiste. */
  tokenHash: string;
}

/** UUID canónico en minúsculas (versiones 1 a 8). */
export function isUuid(value: unknown): value is string {
  return typeof value === 'string' && UUID_PATTERN.test(value);
}

/** SHA-256 en hexadecimal. */
export function hashRefreshToken(token: string): string {
  return createHash('sha256').update(token, 'utf8').digest('hex');
}

/** Emite un refresh token nuevo para `sessionId` (o para una sesión nueva si se omite). */
export function issueRefreshToken(sessionId: string = randomUUID()): IssuedRefreshToken {
  const token = `${sessionId}.${randomBytes(SECRET_BYTES).toString('base64url')}`;
  return { sessionId, token, tokenHash: hashRefreshToken(token) };
}

/**
 * Valida estrictamente el formato y extrae el identificador de sesión. Devuelve null si no es un
 * token bien formado (tipo, longitud, UUID en minúsculas, alfabeto y longitud del secreto).
 */
export function parseRefreshToken(raw: unknown): { sessionId: string; token: string } | null {
  if (typeof raw !== 'string' || raw.length > REFRESH_TOKEN_MAX_LENGTH) return null;
  const match = TOKEN_PATTERN.exec(raw);
  return match ? { sessionId: match[1], token: raw } : null;
}

/** Comparación de hashes SHA-256 hexadecimales en tiempo constante. */
export function constantTimeEqualHex(a: string, b: string): boolean {
  if (!/^[0-9a-f]{64}$/.test(a) || !/^[0-9a-f]{64}$/.test(b)) return false;
  return timingSafeEqual(Buffer.from(a, 'hex'), Buffer.from(b, 'hex'));
}
