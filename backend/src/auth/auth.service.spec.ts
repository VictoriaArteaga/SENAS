import { ConfigService } from '@nestjs/config';
import { JwtService } from '@nestjs/jwt';
import { UnauthorizedException } from '@nestjs/common';
import { createHash } from 'node:crypto';
import { SessionsService } from '../sessions/sessions.service';
import { UsersService } from '../users/users.service';
import { AuthService } from './auth.service';
import { PasswordService } from './password.service';

const SECRET = 'q8Zr2LxV7mT4pN1cB6yH9wKd3sFgJa5eUo0iXvRb';
const env: Record<string, string> = {
  JWT_ACCESS_SECRET: SECRET,
  JWT_ACCESS_EXPIRES_IN: '15m',
  REFRESH_TOKEN_EXPIRES_IN: '30d',
};

describe('AuthService', () => {
  const passwords = new PasswordService();
  const jwt = new JwtService({ secret: SECRET, signOptions: { algorithm: 'HS256', expiresIn: 900 } });
  const config = { getOrThrow: (key: string) => env[key] } as unknown as ConfigService;

  let users: { createWithProfile: jest.Mock; findByEmail: jest.Mock };
  let sessions: { create: jest.Mock };
  let service: AuthService;
  let output: string[];

  const PASSWORD = 'Contraseña-Correcta-9';
  let storedHash: string;
  const baseUser = () => ({
    id: '018f0000-0000-7000-8000-000000000001',
    username: 'maria',
    email: 'maria@example.com',
    passwordHash: storedHash,
    isActive: true,
    perfil: { displayName: 'María' },
  });

  beforeAll(async () => {
    storedHash = await passwords.hash(PASSWORD);
  });

  beforeEach(() => {
    users = { createWithProfile: jest.fn(), findByEmail: jest.fn() };
    sessions = { create: jest.fn().mockResolvedValue({ id: 's1' }) };
    service = new AuthService(users as unknown as UsersService, sessions as unknown as SessionsService, passwords, jwt, config);

    // Captura todo lo que Nest escriba en consola/log durante la prueba.
    output = [];
    const capture = (chunk: unknown) => (output.push(String(chunk)), true);
    jest.spyOn(process.stdout, 'write').mockImplementation(capture as never);
    jest.spyOn(process.stderr, 'write').mockImplementation(capture as never);
  });
  afterEach(() => jest.restoreAllMocks());

  describe('register', () => {
    const dto = { username: 'Maria', email: ' Maria@Example.com ', password: PASSWORD, displayName: ' María ' };

    it('hashea con Argon2id, normaliza y entrega al servicio de usuarios solo el hash', async () => {
      users.createWithProfile.mockResolvedValue({ id: 'u1', username: 'maria', email: 'maria@example.com', displayName: 'María' });
      await service.register(dto);

      const input = users.createWithProfile.mock.calls[0][0];
      expect(input).toMatchObject({ username: 'maria', email: 'maria@example.com', displayName: 'María' });
      expect(input.passwordHash).toMatch(/^\$argon2id\$/);
      expect(JSON.stringify(input)).not.toContain(PASSWORD); // la contraseña en claro no llega al servicio
      await expect(passwords.verify(input.passwordHash, PASSWORD)).resolves.toBe(true);
    });

    it('devuelve mensaje y datos públicos, sin hashes ni contraseña', async () => {
      users.createWithProfile.mockResolvedValue({ id: 'u1', username: 'maria', email: 'maria@example.com', displayName: 'María' });
      const res = await service.register(dto);
      expect(res).toEqual({
        message: expect.stringContaining('Cuenta creada'),
        user: { id: 'u1', username: 'maria', email: 'maria@example.com', displayName: 'María' },
      });
      const json = JSON.stringify(res);
      expect(json).not.toMatch(/passwordHash|argon2|tokenHash/i);
      expect(json).not.toContain(PASSWORD);
    });

    it('no crea ninguna sesión (el usuario debe hacer login)', async () => {
      users.createWithProfile.mockResolvedValue({ id: 'u1', username: 'maria', email: 'maria@example.com', displayName: 'María' });
      await service.register(dto);
      expect(sessions.create).not.toHaveBeenCalled();
    });

    it('propaga el conflicto del servicio de usuarios (duplicados)', async () => {
      users.createWithProfile.mockRejectedValue(new Error('conflicto'));
      await expect(service.register(dto)).rejects.toThrow('conflicto');
    });

    it('no escribe contraseñas ni hashes en el log', async () => {
      users.createWithProfile.mockResolvedValue({ id: 'u1', username: 'maria', email: 'maria@example.com', displayName: 'María' });
      await service.register(dto);
      const logs = output.join('');
      expect(logs).not.toContain(PASSWORD);
      expect(logs).not.toMatch(/argon2/);
    });
  });

  describe('login', () => {
    const dto = { email: 'Maria@Example.com', password: PASSWORD };

    it('con credenciales correctas devuelve tokens, y la sesión guarda solo el SHA-256 del refresh token', async () => {
      users.findByEmail.mockResolvedValue(baseUser());
      const before = Date.now();
      const res = await service.login({ ...dto, informacionDispositivo: { plataforma: 'Windows', versionApp: '1.0' } });

      expect(res).toMatchObject({ tokenType: 'Bearer', expiresIn: 900 });
      expect(res.refreshToken.length).toBeGreaterThanOrEqual(64);

      expect(sessions.create).toHaveBeenCalledTimes(1);
      const saved = sessions.create.mock.calls[0][0];
      expect(saved.usuarioId).toBe(baseUser().id);
      expect(saved.tokenHash).toBe(createHash('sha256').update(res.refreshToken).digest('hex'));
      expect(saved.tokenHash).not.toBe(res.refreshToken);
      expect(JSON.stringify(saved)).not.toContain(res.refreshToken); // el token plano no se persiste
      expect(saved.informacionDispositivo).toEqual({ plataforma: 'Windows', versionApp: '1.0' });

      // Expiración del refresh token: 30 días desde ahora.
      const thirtyDays = 30 * 24 * 3600 * 1000;
      expect(saved.fechaExpiracion.getTime()).toBeGreaterThanOrEqual(before + thirtyDays);
      expect(saved.fechaExpiracion.getTime()).toBeLessThanOrEqual(Date.now() + thirtyDays);
      expect(res.refreshTokenExpiresAt).toBe(saved.fechaExpiracion.toISOString());
    });

    it('el access token es un JWT HS256 con solo sub, email, iat y exp (15 min)', async () => {
      users.findByEmail.mockResolvedValue(baseUser());
      const { accessToken } = await service.login(dto);

      const claims = await jwt.verifyAsync(accessToken, { secret: SECRET, algorithms: ['HS256'] });
      expect(Object.keys(claims).sort()).toEqual(['email', 'exp', 'iat', 'sub']);
      expect(claims.sub).toBe(baseUser().id);
      expect(claims.email).toBe('maria@example.com');
      expect(claims.exp - claims.iat).toBe(900);

      const header = JSON.parse(Buffer.from(accessToken.split('.')[0], 'base64url').toString());
      expect(header.alg).toBe('HS256');
      expect(accessToken).not.toContain(storedHash);
    });

    it('un token firmado con otra clave no se acepta', async () => {
      users.findByEmail.mockResolvedValue(baseUser());
      const { accessToken } = await service.login(dto);
      await expect(jwt.verifyAsync(accessToken, { secret: 'otra-clave-distinta-0123456789abcdef0123' })).rejects.toThrow();
    });

    it('busca al usuario por el email normalizado', async () => {
      users.findByEmail.mockResolvedValue(baseUser());
      await service.login({ email: '  MARIA@example.COM ', password: PASSWORD });
      expect(users.findByEmail).toHaveBeenCalledWith('maria@example.com');
    });

    it('cada login crea su propia sesión (varios dispositivos) con refresh tokens distintos', async () => {
      users.findByEmail.mockResolvedValue(baseUser());
      const a = await service.login(dto);
      const b = await service.login(dto);
      expect(sessions.create).toHaveBeenCalledTimes(2);
      expect(a.refreshToken).not.toBe(b.refreshToken);
      expect(sessions.create.mock.calls[0][0].tokenHash).not.toBe(sessions.create.mock.calls[1][0].tokenHash);
    });

    it('sin informacionDispositivo la sesión se guarda sin ese campo', async () => {
      users.findByEmail.mockResolvedValue(baseUser());
      await service.login(dto);
      expect(sessions.create.mock.calls[0][0].informacionDispositivo).toBeUndefined();
    });

    it('la respuesta pública no contiene hashes ni datos internos', async () => {
      users.findByEmail.mockResolvedValue(baseUser());
      const res = await service.login(dto);
      expect(res.user).toEqual({ id: baseUser().id, username: 'maria', email: 'maria@example.com', displayName: 'María' });
      const json = JSON.stringify(res);
      expect(json).not.toContain(storedHash);
      expect(json).not.toMatch(/passwordHash|tokenHash|isActive|argon2/i);
      expect(json).not.toContain(createHash('sha256').update(res.refreshToken).digest('hex'));
    });

    describe('rechazos (todos con el mismo error genérico)', () => {
      const failures: Array<[string, () => void, typeof dto]> = [
        ['email inexistente', () => users.findByEmail.mockResolvedValue(null), dto],
        ['contraseña incorrecta', () => users.findByEmail.mockResolvedValue(baseUser()), { ...dto, password: 'otra-contraseña-1' }],
        ['usuario inactivo (con contraseña correcta)', () => users.findByEmail.mockResolvedValue({ ...baseUser(), isActive: false }), dto],
      ];

      it.each(failures)('%s -> 401 sin crear sesión ni emitir tokens', async (_name, arrange, input) => {
        arrange();
        await expect(service.login(input)).rejects.toBeInstanceOf(UnauthorizedException);
        expect(sessions.create).not.toHaveBeenCalled();
      });

      it('el mensaje es idéntico en los tres casos y no revela qué falló', async () => {
        const msgs: string[] = [];
        for (const [, arrange, input] of failures) {
          arrange();
          try {
            await service.login(input);
          } catch (e) {
            msgs.push(JSON.stringify((e as UnauthorizedException).getResponse()));
          }
        }
        expect(new Set(msgs).size).toBe(1);
        expect(msgs[0]).toContain('Credenciales inválidas.');
        expect(msgs[0]).not.toMatch(/inactiv|existe|correo|contraseña incorrecta/i);
      });

      it('con email inexistente igualmente ejecuta una verificación Argon2 (mismo coste temporal)', async () => {
        users.findByEmail.mockResolvedValue(null);
        const dummy = jest.spyOn(passwords, 'verifyAgainstDummy');
        await expect(service.login(dto)).rejects.toThrow();
        expect(dummy).toHaveBeenCalledWith(PASSWORD);
      });

      it('un hash almacenado corrupto no autentica', async () => {
        users.findByEmail.mockResolvedValue({ ...baseUser(), passwordHash: 'corrupto' });
        await expect(service.login(dto)).rejects.toBeInstanceOf(UnauthorizedException);
      });
    });

    it('si falla guardar la sesión no se entregan tokens', async () => {
      users.findByEmail.mockResolvedValue(baseUser());
      sessions.create.mockRejectedValue(new Error('db caída'));
      await expect(service.login(dto)).rejects.toThrow('db caída');
    });

    it('no escribe contraseñas, tokens ni hashes en el log (éxito ni fallo)', async () => {
      users.findByEmail.mockResolvedValue(baseUser());
      const ok = await service.login(dto);
      users.findByEmail.mockResolvedValue(null);
      await service.login({ ...dto, password: 'intento-fallido-77' }).catch(() => undefined);

      const logs = output.join('');
      for (const secret of [PASSWORD, 'intento-fallido-77', ok.refreshToken, ok.accessToken, storedHash, createHash('sha256').update(ok.refreshToken).digest('hex')]) {
        expect(logs).not.toContain(secret);
      }
    });
  });
});
