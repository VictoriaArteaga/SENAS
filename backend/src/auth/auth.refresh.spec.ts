import { UnauthorizedException } from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import { JwtService } from '@nestjs/jwt';
import { SessionForRefresh, SessionsService } from '../sessions/sessions.service';
import { UsersService } from '../users/users.service';
import { AuthService } from './auth.service';
import { PasswordService } from './password.service';
import { hashRefreshToken, issueRefreshToken, parseRefreshToken } from './token.util';

const SECRET = 'q8Zr2LxV7mT4pN1cB6yH9wKd3sFgJa5eUo0iXvRb';
const env: Record<string, string> = { JWT_ACCESS_SECRET: SECRET, JWT_ACCESS_EXPIRES_IN: '15m', REFRESH_TOKEN_EXPIRES_IN: '30d' };
const INVALID = 'Refresh token inválido o expirado.';

describe('AuthService: refresh, logout, logoutAll y me', () => {
  const jwt = new JwtService({ secret: SECRET, signOptions: { algorithm: 'HS256', expiresIn: 900 } });
  const config = { getOrThrow: (key: string) => env[key] } as unknown as ConfigService;

  let sessions: { findForRefresh: jest.Mock; rotate: jest.Mock; revoke: jest.Mock; revokeByToken: jest.Mock; revokeAllForUser: jest.Mock; create: jest.Mock };
  let users: { findMeById: jest.Mock };
  let service: AuthService;
  let output: string[];
  let issued: ReturnType<typeof issueRefreshToken>;
  let session: SessionForRefresh;

  const withSession = (over: Partial<SessionForRefresh> = {}): SessionForRefresh => ({
    id: issued.sessionId,
    usuarioId: '018f0000-0000-7000-8000-000000000001',
    tokenHash: issued.tokenHash,
    fechaExpiracion: new Date(Date.now() + 10 * 24 * 3600 * 1000),
    fechaRevocacion: null,
    usuario: { email: 'maria@example.com', isActive: true },
    ...over,
  });
  const rejection = async (token: string) => {
    const err = await service.refresh({ refreshToken: token }).catch((e) => e);
    expect(err).toBeInstanceOf(UnauthorizedException);
    return err as UnauthorizedException;
  };

  beforeEach(() => {
    issued = issueRefreshToken();
    session = withSession();
    sessions = {
      findForRefresh: jest.fn().mockResolvedValue(session),
      rotate: jest.fn().mockResolvedValue(true),
      revoke: jest.fn().mockResolvedValue(true),
      revokeByToken: jest.fn().mockResolvedValue(true),
      revokeAllForUser: jest.fn().mockResolvedValue(3),
      create: jest.fn(),
    };
    users = { findMeById: jest.fn() };
    service = new AuthService(users as unknown as UsersService, sessions as unknown as SessionsService, new PasswordService(), jwt, config);

    output = [];
    const capture = (chunk: unknown) => (output.push(String(chunk)), true);
    jest.spyOn(process.stdout, 'write').mockImplementation(capture as never);
    jest.spyOn(process.stderr, 'write').mockImplementation(capture as never);
  });
  afterEach(() => jest.restoreAllMocks());

  describe('refresh: token válido', () => {
    it('rota el token (misma sesión, secreto nuevo) y actualiza el hash de forma condicional', async () => {
      const res = await service.refresh({ refreshToken: issued.token });

      expect(res.refreshToken).not.toBe(issued.token);
      expect(parseRefreshToken(res.refreshToken)?.sessionId).toBe(issued.sessionId);
      expect(sessions.findForRefresh).toHaveBeenCalledWith(issued.sessionId);
      expect(sessions.rotate).toHaveBeenCalledTimes(1);
      const args = sessions.rotate.mock.calls[0][0];
      expect(args).toEqual({ id: issued.sessionId, currentHash: issued.tokenHash, newHash: hashRefreshToken(res.refreshToken), now: expect.any(Date) });
      expect(args.newHash).not.toBe(args.currentHash);
      expect(sessions.revoke).not.toHaveBeenCalled();
    });

    it('la expiración es ABSOLUTA: no se extiende ni se envía a la base al rotar', async () => {
      const res = await service.refresh({ refreshToken: issued.token });
      expect(res.refreshTokenExpiresAt).toBe(session.fechaExpiracion.toISOString());
      expect(Object.keys(sessions.rotate.mock.calls[0][0])).not.toContain('fechaExpiracion');
    });

    it('emite un access token JWT HS256 solo con sub, email, iat y exp', async () => {
      const res = await service.refresh({ refreshToken: issued.token });
      const claims = await jwt.verifyAsync(res.accessToken, { secret: SECRET, algorithms: ['HS256'] });
      expect(Object.keys(claims).sort()).toEqual(['email', 'exp', 'iat', 'sub']);
      expect(claims).toMatchObject({ sub: session.usuarioId, email: 'maria@example.com' });
      expect(claims.exp - claims.iat).toBe(900);
      expect(res).toMatchObject({ tokenType: 'Bearer', expiresIn: 900 });
      expect(Object.keys(res).sort()).toEqual(['accessToken', 'expiresIn', 'refreshToken', 'refreshTokenExpiresAt', 'tokenType']);
    });
  });

  describe('refresh: rechazos (mismo error genérico, sin rotar)', () => {
    it.each([
      ['malformado (no es string con formato)', 'esto-no-es-un-token'],
      ['sin secreto', issueRefreshToken().sessionId],
      ['UUID inválido', 'zzzz.' + 'A'.repeat(64)],
    ])('token %s -> 401 sin consultar la base de datos', async (_n, token) => {
      const err = await rejection(token);
      expect(err.message).toBe(INVALID);
      expect(sessions.findForRefresh).not.toHaveBeenCalled();
      expect(sessions.rotate).not.toHaveBeenCalled();
    });

    it('sesión inexistente', async () => {
      sessions.findForRefresh.mockResolvedValue(null);
      expect((await rejection(issued.token)).message).toBe(INVALID);
      expect(sessions.rotate).not.toHaveBeenCalled();
      expect(sessions.revoke).not.toHaveBeenCalled();
    });

    it('sesión revocada', async () => {
      sessions.findForRefresh.mockResolvedValue(withSession({ fechaRevocacion: new Date() }));
      expect((await rejection(issued.token)).message).toBe(INVALID);
      expect(sessions.rotate).not.toHaveBeenCalled();
    });

    it('sesión expirada', async () => {
      sessions.findForRefresh.mockResolvedValue(withSession({ fechaExpiracion: new Date(Date.now() - 1000) }));
      expect((await rejection(issued.token)).message).toBe(INVALID);
      expect(sessions.rotate).not.toHaveBeenCalled();
    });

    it('usuario inactivo', async () => {
      sessions.findForRefresh.mockResolvedValue(withSession({ usuario: { email: 'maria@example.com', isActive: false } }));
      expect((await rejection(issued.token)).message).toBe(INVALID);
      expect(sessions.rotate).not.toHaveBeenCalled();
    });

    it('todos los fallos devuelven exactamente el mismo cuerpo', async () => {
      const bodies = new Set<string>();
      const cases: Array<() => void> = [
        () => sessions.findForRefresh.mockResolvedValue(null),
        () => sessions.findForRefresh.mockResolvedValue(withSession({ fechaRevocacion: new Date() })),
        () => sessions.findForRefresh.mockResolvedValue(withSession({ fechaExpiracion: new Date(0) })),
        () => sessions.findForRefresh.mockResolvedValue(withSession({ tokenHash: 'a'.repeat(64) })),
        () => sessions.findForRefresh.mockResolvedValue(withSession({ usuario: { email: 'x', isActive: false } })),
      ];
      for (const arrange of cases) {
        arrange();
        bodies.add(JSON.stringify((await rejection(issued.token)).getResponse()));
      }
      bodies.add(JSON.stringify((await rejection('malformado')).getResponse()));
      expect(bodies.size).toBe(1);
    });
  });

  describe('refresh: reutilización y concurrencia', () => {
    it('token cuyo hash no coincide con el vigente (p. ej. ya rotado): 401 y revoca ESA sesión', async () => {
      const rotated = issueRefreshToken(issued.sessionId); // la sesión ya avanzó a este token
      sessions.findForRefresh.mockResolvedValue(withSession({ tokenHash: rotated.tokenHash }));

      expect((await rejection(issued.token)).message).toBe(INVALID); // se presenta el anterior
      expect(sessions.revoke).toHaveBeenCalledTimes(1);
      expect(sessions.revoke).toHaveBeenCalledWith(issued.sessionId);
      expect(sessions.rotate).not.toHaveBeenCalled();
    });

    it('la reutilización NO revoca las demás sesiones del usuario', async () => {
      sessions.findForRefresh.mockResolvedValue(withSession({ tokenHash: 'b'.repeat(64) }));
      await rejection(issued.token);
      expect(sessions.revokeAllForUser).not.toHaveBeenCalled();
      expect(sessions.revokeByToken).not.toHaveBeenCalled();
    });

    it('una firma falsificada para un sessionId existente tampoco rota y solo afecta a esa sesión', async () => {
      const forged = `${issued.sessionId}.${'B'.repeat(64)}`;
      expect(parseRefreshToken(forged)).not.toBeNull();
      await rejection(forged);
      expect(sessions.rotate).not.toHaveBeenCalled();
      expect(sessions.revoke).toHaveBeenCalledWith(issued.sessionId);
    });

    it('si pierde la rotación atómica (rotate=false): 401, revoca la sesión y NO emite tokens', async () => {
      sessions.rotate.mockResolvedValue(false);
      const sign = jest.spyOn(jwt, 'signAsync');
      expect((await rejection(issued.token)).message).toBe(INVALID);
      expect(sessions.revoke).toHaveBeenCalledWith(issued.sessionId);
      expect(sign).not.toHaveBeenCalled();
    });

    it('dos solicitudes simultáneas con el mismo token: solo una rotación exitosa', async () => {
      let first = true;
      sessions.rotate.mockImplementation(async () => (first ? ((first = false), true) : false));
      const results = await Promise.allSettled([
        service.refresh({ refreshToken: issued.token }),
        service.refresh({ refreshToken: issued.token }),
        service.refresh({ refreshToken: issued.token }),
      ]);
      expect(results.filter((r) => r.status === 'fulfilled')).toHaveLength(1);
      expect(results.filter((r) => r.status === 'rejected')).toHaveLength(2);
      expect(sessions.rotate).toHaveBeenCalledTimes(3);
    });

    it('registra el evento sin tokens, secretos ni hashes', async () => {
      sessions.findForRefresh.mockResolvedValue(withSession({ tokenHash: 'c'.repeat(64) }));
      await rejection(issued.token);
      const ok = await service.refresh({ refreshToken: (sessions.findForRefresh.mockResolvedValue(session), issued.token) });

      const logs = output.join('');
      expect(logs).toContain('Posible reutilización de refresh token');
      for (const secret of [issued.token, issued.token.split('.')[1], issued.tokenHash, 'c'.repeat(64), ok.refreshToken, ok.accessToken, hashRefreshToken(ok.refreshToken)]) {
        expect(logs).not.toContain(secret);
      }
    });
  });

  describe('logout', () => {
    it('revoca la sesión del token presentado comprobando su hash', async () => {
      await expect(service.logout({ refreshToken: issued.token })).resolves.toBeUndefined();
      expect(sessions.revokeByToken).toHaveBeenCalledWith(issued.sessionId, issued.tokenHash);
    });

    it('es idempotente: token malformado, desconocido, ajeno o ya revocado terminan igual, sin error', async () => {
      sessions.revokeByToken.mockResolvedValue(false); // nada que revocar
      for (const token of ['basura', issueRefreshToken().token, `${issued.sessionId}.${'C'.repeat(64)}`]) {
        await expect(service.logout({ refreshToken: token })).resolves.toBeUndefined();
      }
      await expect(service.logout({ refreshToken: issued.token })).resolves.toBeUndefined();
      await expect(service.logout({ refreshToken: issued.token })).resolves.toBeUndefined(); // repetición
    });

    it('un token malformado no toca la base de datos', async () => {
      await service.logout({ refreshToken: 'basura' });
      expect(sessions.revokeByToken).not.toHaveBeenCalled();
    });

    it('con el secreto equivocado intenta revocar con el hash del token presentado (no el vigente)', async () => {
      const forged = `${issued.sessionId}.${'D'.repeat(64)}`;
      await service.logout({ refreshToken: forged });
      expect(sessions.revokeByToken).toHaveBeenCalledWith(issued.sessionId, hashRefreshToken(forged));
      expect(hashRefreshToken(forged)).not.toBe(issued.tokenHash);
    });

    it('nunca elimina ni revoca otras sesiones', async () => {
      await service.logout({ refreshToken: issued.token });
      expect(sessions.revokeAllForUser).not.toHaveBeenCalled();
      expect(sessions.revoke).not.toHaveBeenCalled();
    });
  });

  describe('logoutAll', () => {
    it('revoca las sesiones del usuario indicado por la identidad autenticada y devuelve el total', async () => {
      const res = await service.logoutAll('018f0000-0000-7000-8000-000000000042');
      expect(sessions.revokeAllForUser).toHaveBeenCalledWith('018f0000-0000-7000-8000-000000000042');
      expect(res).toEqual({ message: expect.any(String), sesionesRevocadas: 3 });
    });
  });

  describe('me', () => {
    it('devuelve el perfil público', async () => {
      const profile = { id: 'u', username: 'maria', email: 'maria@example.com', displayName: 'María', createdAt: 'x', profile: { totalXp: 0, nivelActual: 1, precisionPromedio: 0, avatarConfig: null } };
      users.findMeById.mockResolvedValue(profile);
      await expect(service.me('u')).resolves.toEqual(profile);
    });
    it('si la cuenta ya no existe o está inactiva responde 401', async () => {
      users.findMeById.mockResolvedValue(null);
      await expect(service.me('u')).rejects.toBeInstanceOf(UnauthorizedException);
    });
  });
});
