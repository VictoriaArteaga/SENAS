import { createHash, randomUUID } from 'node:crypto';
import {
  constantTimeEqualHex,
  hashRefreshToken,
  isUuid,
  issueRefreshToken,
  parseRefreshToken,
  REFRESH_TOKEN_MAX_LENGTH,
} from './token.util';

const SECRET_64 = 'A'.repeat(64);

describe('issueRefreshToken', () => {
  it('formato <sessionId>.<secreto> con UUID y al menos 32 bytes de secreto', () => {
    const t = issueRefreshToken();
    const [id, secret, ...rest] = t.token.split('.');
    expect(rest).toHaveLength(0);
    expect(id).toBe(t.sessionId);
    expect(isUuid(id)).toBe(true);
    expect(secret).toMatch(/^[A-Za-z0-9_-]+$/); // base64url
    expect(Buffer.from(secret, 'base64url').length).toBeGreaterThanOrEqual(32);
    expect(t.token.length).toBeLessThanOrEqual(REFRESH_TOKEN_MAX_LENGTH);
  });

  it('es aleatorio: 300 tokens sin repeticiones ni sessionId repetido', () => {
    const tokens = Array.from({ length: 300 }, () => issueRefreshToken());
    expect(new Set(tokens.map((t) => t.token)).size).toBe(300);
    expect(new Set(tokens.map((t) => t.sessionId)).size).toBe(300);
    expect(new Set(tokens.map((t) => t.token.split('.')[1])).size).toBe(300);
  });

  it('conserva el sessionId al rotar y cambia el secreto y el hash', () => {
    const first = issueRefreshToken();
    const next = issueRefreshToken(first.sessionId);
    expect(next.sessionId).toBe(first.sessionId);
    expect(next.token).not.toBe(first.token);
    expect(next.tokenHash).not.toBe(first.tokenHash);
    expect(next.token.split('.')[1]).not.toBe(first.token.split('.')[1]);
  });

  it('tokenHash es el SHA-256 hexadecimal del token COMPLETO y no contiene el token', () => {
    const t = issueRefreshToken();
    expect(t.tokenHash).toBe(createHash('sha256').update(t.token).digest('hex'));
    expect(t.tokenHash).toHaveLength(64);
    expect(t.tokenHash).toBe(hashRefreshToken(t.token));
    expect(t.tokenHash).not.toContain(t.sessionId);
    // El hash del secreto solo no es el almacenado: se hashea el token completo.
    expect(t.tokenHash).not.toBe(hashRefreshToken(t.token.split('.')[1]));
  });
});

describe('parseRefreshToken (validación estricta)', () => {
  it('acepta un token emitido y devuelve su sessionId', () => {
    const t = issueRefreshToken();
    expect(parseRefreshToken(t.token)).toEqual({ sessionId: t.sessionId, token: t.token });
  });

  const id = randomUUID();
  it.each<[string, unknown]>([
    ['no es string', 12345],
    ['null', null],
    ['undefined', undefined],
    ['objeto', { token: 'x' }],
    ['arreglo', ['a.b']],
    ['vacío', ''],
    ['sin punto', `${id}${SECRET_64}`],
    ['solo UUID', id],
    ['UUID con mayúsculas', `${id.toUpperCase()}.${SECRET_64}`],
    ['UUID inválido', `no-es-un-uuid-valido-aaaaaaaaaaaaaaaaaaaa.${SECRET_64}`],
    ['UUID de versión inexistente (0)', `${id.slice(0, 14)}0${id.slice(15)}.${SECRET_64}`],
    ['UUID con variante inválida', `${id.slice(0, 19)}0${id.slice(20)}.${SECRET_64}`],
    ['secreto demasiado corto (<32 bytes)', `${id}.${'A'.repeat(42)}`],
    ['secreto con caracteres fuera de base64url', `${id}.${'A'.repeat(60)}+/==`],
    ['secreto con espacios', `${id}.${'A'.repeat(30)} ${'A'.repeat(30)}`],
    ['punto de más', `${id}.${SECRET_64}.extra`],
    ['espacios alrededor', ` ${id}.${SECRET_64} `],
    ['salto de línea final', `${id}.${SECRET_64}\n`],
    ['demasiado largo', `${id}.${'A'.repeat(REFRESH_TOKEN_MAX_LENGTH)}`],
    ['inyección SQL', `${id}.' OR '1'='1`],
  ])('rechaza: %s', (_n, value) => {
    expect(parseRefreshToken(value)).toBeNull();
  });
});

describe('constantTimeEqualHex', () => {
  const a = hashRefreshToken('uno');
  it('compara hashes SHA-256 hexadecimales', () => {
    expect(constantTimeEqualHex(a, a)).toBe(true);
    expect(constantTimeEqualHex(a, hashRefreshToken('dos'))).toBe(false);
  });
  it('devuelve false (sin lanzar) ante entradas que no son SHA-256 hex', () => {
    for (const bad of ['', 'abc', 'g'.repeat(64), a.toUpperCase(), a.slice(1), `${a}0`]) {
      expect(constantTimeEqualHex(a, bad)).toBe(false);
      expect(constantTimeEqualHex(bad, a)).toBe(false);
    }
  });
});

describe('isUuid', () => {
  it('solo acepta UUID canónicos en minúsculas', () => {
    expect(isUuid(randomUUID())).toBe(true);
    for (const bad of ['', 'x', randomUUID().toUpperCase(), 123, null, undefined, `${randomUUID()} `]) {
      expect(isUuid(bad)).toBe(false);
    }
  });
});
