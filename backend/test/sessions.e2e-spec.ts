/**
 * Integración de refresh, logout, logout-all y /auth/me contra un PostgreSQL REAL (local, temporal),
 * conectado como `senas_app` (SELECT/INSERT/UPDATE, sin DELETE, con RLS). Se omite sin TEST_DATABASE_URL.
 * Ver el encabezado de auth.e2e-spec.ts para levantar la base de pruebas.
 */
import { ConsoleLogger, INestApplication } from '@nestjs/common';
import { JwtService } from '@nestjs/jwt';
import { Test } from '@nestjs/testing';
import { createHash, randomBytes, randomUUID } from 'node:crypto';
import request from 'supertest';
import { App } from 'supertest/types';
import { AppModule } from '../src/app.module';
import { PrismaService } from '../src/prisma/prisma.service';
import { SessionsService } from '../src/sessions/sessions.service';

const describeDb = process.env.TEST_DATABASE_URL ? describe : describe.skip;
const SECRET = process.env.JWT_ACCESS_SECRET as string;
const INVALID = 'Refresh token inválido o expirado.';

describeDb('Sesiones: refresh, logout, logout-all y me (PostgreSQL local, rol senas_app)', () => {
  let app: INestApplication<App>;
  let prisma: PrismaService;
  let jwt: JwtService;
  let output: string[];
  const run = randomBytes(3).toString('hex');
  const sha256 = (s: string) => createHash('sha256').update(s).digest('hex');
  const http = () => request(app.getHttpServer());

  interface Account { id: string; email: string; username: string; password: string }
  interface Tokens { accessToken: string; refreshToken: string; refreshTokenExpiresAt: string }

  async function newAccount(): Promise<Account> {
    const username = `u_${run}_${randomBytes(2).toString('hex')}`;
    const a = { username, email: `${username}@example.com`, password: 'Contraseña-Segura-123', displayName: 'Jugador' };
    const res = await http().post('/auth/register').send(a).expect(201);
    return { id: res.body.user.id, email: a.email, username, password: a.password };
  }
  async function login(a: Account, plataforma = 'Windows'): Promise<Tokens> {
    const res = await http().post('/auth/login').send({ email: a.email, password: a.password, informacionDispositivo: { plataforma } }).expect(200);
    return res.body;
  }
  const refresh = (refreshToken: unknown) => http().post('/auth/refresh').send({ refreshToken } as object);
  const logout = (refreshToken: unknown) => http().post('/auth/logout').send({ refreshToken } as object);
  const me = (token?: string) => (token ? http().get('/auth/me').set('Authorization', `Bearer ${token}`) : http().get('/auth/me'));
  const sessionOf = (refreshToken: string) => prisma.sesion.findUniqueOrThrow({ where: { id: refreshToken.split('.')[0] } });

  beforeAll(async () => {
    // Captura toda la salida (incluido el arranque) desde antes de crear la aplicación.
    output = [];
    const capture = (chunk: unknown) => (output.push(String(chunk)), true);
    jest.spyOn(process.stdout, 'write').mockImplementation(capture as never);
    jest.spyOn(process.stderr, 'write').mockImplementation(capture as never);
    // ConsoleLogger real: el logger por defecto de las pruebas silencia log/warn.
    const moduleRef = await Test.createTestingModule({ imports: [AppModule] }).setLogger(new ConsoleLogger()).compile();
    app = moduleRef.createNestApplication();
    await app.init();
    prisma = app.get(PrismaService);
    jwt = app.get(JwtService, { strict: false });
  });
  afterAll(async () => {
    await app.close();
    jest.restoreAllMocks();
  });

  describe('login emite refresh tokens con el formato <sessionId>.<secreto>', () => {
    it('el id de sesión del token es la fila de sesiones y la base solo guarda el SHA-256 del token completo', async () => {
      const a = await newAccount();
      const t = await login(a);
      expect(t.refreshToken).toMatch(/^[0-9a-f-]{36}\.[A-Za-z0-9_-]{64}$/);
      const s = await sessionOf(t.refreshToken);
      expect(s.usuarioId).toBe(a.id);
      expect(s.tokenHash).toBe(sha256(t.refreshToken));
      const raw = await prisma.$queryRaw<{ n: bigint }[]>`SELECT count(*) AS n FROM sesiones s WHERE s::text LIKE ${'%' + t.refreshToken.split('.')[1] + '%'}`;
      expect(Number(raw[0].n)).toBe(0); // el secreto no está en ninguna columna
    });
  });

  describe('POST /auth/refresh', () => {
    it('token válido: 200, rota el token, actualiza ultimoUso y NO extiende la expiración', async () => {
      const a = await newAccount();
      const t = await login(a);
      const before = await sessionOf(t.refreshToken);
      expect(before.ultimoUso).toBeNull();

      const res = await refresh(t.refreshToken).expect(200);
      expect(Object.keys(res.body).sort()).toEqual(['accessToken', 'expiresIn', 'refreshToken', 'refreshTokenExpiresAt', 'tokenType']);
      expect(res.body).toMatchObject({ tokenType: 'Bearer', expiresIn: 900 });
      expect(res.body.refreshToken).not.toBe(t.refreshToken);
      expect(res.body.refreshToken.split('.')[0]).toBe(t.refreshToken.split('.')[0]); // misma sesión

      const after = await sessionOf(t.refreshToken);
      expect(after.tokenHash).toBe(sha256(res.body.refreshToken)); // solo el hash del nuevo
      expect(after.tokenHash).not.toBe(before.tokenHash);
      expect(after.ultimoUso).not.toBeNull();
      expect(after.fechaRevocacion).toBeNull();
      expect(after.fechaExpiracion.toISOString()).toBe(before.fechaExpiracion.toISOString()); // vigencia absoluta
      expect(res.body.refreshTokenExpiresAt).toBe(before.fechaExpiracion.toISOString());
      expect(await prisma.sesion.count({ where: { tokenHash: res.body.refreshToken } })).toBe(0);

      // El nuevo access token funciona y lleva los claims previstos.
      const claims = await jwt.verifyAsync(res.body.accessToken, { secret: SECRET, algorithms: ['HS256'] });
      expect(Object.keys(claims).sort()).toEqual(['email', 'exp', 'iat', 'sub']);
      expect(claims).toMatchObject({ sub: a.id, email: a.email });
      await me(res.body.accessToken).expect(200);
    });

    it('el token nuevo funciona (rotación encadenada) y el anterior deja de funcionar', async () => {
      const a = await newAccount();
      const t0 = await login(a);
      const t1 = (await refresh(t0.refreshToken).expect(200)).body;
      const t2 = (await refresh(t1.refreshToken).expect(200)).body;
      expect(new Set([t0.refreshToken, t1.refreshToken, t2.refreshToken]).size).toBe(3);
      const row = await sessionOf(t2.refreshToken);
      expect(row.tokenHash).toBe(sha256(t2.refreshToken));
    });

    it('reutilizar un token ya rotado: 401 y la sesión queda revocada (también el token nuevo)', async () => {
      const a = await newAccount();
      const t0 = await login(a);
      const t1 = (await refresh(t0.refreshToken).expect(200)).body;

      const res = await refresh(t0.refreshToken).expect(401); // reutilización
      expect(res.body.message).toBe(INVALID);
      const s = await sessionOf(t0.refreshToken);
      expect(s.fechaRevocacion).not.toBeNull();
      await refresh(t1.refreshToken).expect(401); // el token vigente de esa sesión también cae
    });

    it('la reutilización solo revoca esa sesión: las demás sesiones del usuario siguen activas', async () => {
      const a = await newAccount();
      const phone = await login(a, 'Android');
      const pc = await login(a, 'Windows');
      await refresh(phone.refreshToken).expect(200);
      await refresh(phone.refreshToken).expect(401); // reutilización en el teléfono

      expect((await sessionOf(phone.refreshToken)).fechaRevocacion).not.toBeNull();
      expect((await sessionOf(pc.refreshToken)).fechaRevocacion).toBeNull();
      await refresh(pc.refreshToken).expect(200);
    });

    it('secreto equivocado para un sessionId existente: 401 sin rotar (y revoca esa sesión)', async () => {
      const a = await newAccount();
      const t = await login(a);
      const before = await sessionOf(t.refreshToken);
      const forged = `${t.refreshToken.split('.')[0]}.${randomBytes(48).toString('base64url')}`;
      await refresh(forged).expect(401);
      const after = await sessionOf(t.refreshToken);
      expect(after.tokenHash).toBe(before.tokenHash); // no rotó
      expect(after.fechaRevocacion).not.toBeNull();
    });

    it.each([
      ['malformado', 'esto-no-es-un-token'],
      ['con UUID inválido', `no-uuid.${'A'.repeat(64)}`],
      ['con inyección SQL', `${randomUUID()}.' OR '1'='1`],
      ['con mayúsculas en el UUID', `${randomUUID().toUpperCase()}.${'A'.repeat(64)}`],
      ['sesión inexistente (formato válido)', `${randomUUID()}.${randomBytes(48).toString('base64url')}`],
    ])('token %s: 401 con el mensaje genérico', async (_n, token) => {
      const res = await refresh(token).expect(401);
      expect(res.body).toEqual({ statusCode: 401, message: INVALID, error: 'Unauthorized' });
    });

    it.each([
      ['falta el campo', {}],
      ['no es string', { refreshToken: 123 }],
      ['es un objeto', { refreshToken: { $ne: '' } }],
      ['vacío', { refreshToken: '' }],
      ['demasiado largo', { refreshToken: 'x'.repeat(300) }],
      ['campo adicional', { refreshToken: 'x', usuarioId: randomUUID() }],
    ])('cuerpo inválido (%s): 400', async (_n, body) => {
      await http().post('/auth/refresh').send(body).expect(400);
    });

    it('sesión revocada: 401 y no rota', async () => {
      const a = await newAccount();
      const t = await login(a);
      await logout(t.refreshToken).expect(204);
      const before = await sessionOf(t.refreshToken);
      await refresh(t.refreshToken).expect(401);
      expect((await sessionOf(t.refreshToken)).tokenHash).toBe(before.tokenHash);
    });

    it('sesión expirada: 401 y no rota', async () => {
      const a = await newAccount();
      const t = await login(a);
      const id = t.refreshToken.split('.')[0];
      const past = new Date(Date.now() - 24 * 3600 * 1000);
      await prisma.sesion.update({ where: { id }, data: { fechaCreacion: new Date(past.getTime() - 3600_000), fechaExpiracion: past } });
      const before = await sessionOf(t.refreshToken);
      const res = await refresh(t.refreshToken).expect(401);
      expect(res.body.message).toBe(INVALID);
      const after = await sessionOf(t.refreshToken);
      expect(after.tokenHash).toBe(before.tokenHash);
      expect(after.ultimoUso).toBeNull();
    });

    it('usuario inactivo: 401 y no rota; tampoco puede usar su access token', async () => {
      const a = await newAccount();
      const t = await login(a);
      await prisma.usuario.update({ where: { id: a.id }, data: { isActive: false } });
      const before = await sessionOf(t.refreshToken);
      await refresh(t.refreshToken).expect(401);
      expect((await sessionOf(t.refreshToken)).tokenHash).toBe(before.tokenHash);
      await me(t.accessToken).expect(401); // la cuenta inactiva pierde acceso aunque el JWT siga vigente
    });

    it('todos los rechazos son indistinguibles entre sí', async () => {
      const a = await newAccount();
      const t = await login(a);
      await logout(t.refreshToken).expect(204);
      const bodies = [
        (await refresh(t.refreshToken).expect(401)).body, // revocada
        (await refresh('basura').expect(401)).body, // malformado
        (await refresh(`${randomUUID()}.${randomBytes(48).toString('base64url')}`).expect(401)).body, // inexistente
      ];
      expect(new Set(bodies.map((b) => JSON.stringify(b))).size).toBe(1);
    });

    it('solicitudes simultáneas con el mismo token: solo UNA rotación exitosa (carrera forzada)', async () => {
      const a = await newAccount();
      const t = await login(a);
      const N = 8;

      // Barrera: todas las solicitudes leen la sesión (mismo hash vigente) ANTES de que cualquiera intente
      // rotar. Así la ventana lectura-escritura se solapa siempre y solo la condición atómica del UPDATE
      // puede impedir varias rotaciones (sin esto, el servidor las procesa casi en serie).
      const sessionsService = app.get(SessionsService, { strict: false });
      const original = sessionsService.findForRefresh.bind(sessionsService);
      let arrived = 0;
      let release!: () => void;
      const gate = new Promise<void>((resolve) => (release = resolve));
      const spy = jest.spyOn(sessionsService, 'findForRefresh').mockImplementation(async (id: string) => {
        const row = await original(id);
        if (++arrived >= N) release();
        await gate;
        return row;
      });

      const results = await Promise.all(Array.from({ length: N }, () => refresh(t.refreshToken)));
      spy.mockRestore();
      expect(arrived).toBe(N); // todas leyeron antes de rotar

      const ok = results.filter((r) => r.status === 200);
      const rejected = results.filter((r) => r.status === 401);
      expect(ok).toHaveLength(1);
      expect(rejected).toHaveLength(N - 1);
      expect(new Set(ok.map((r) => r.body.refreshToken)).size).toBe(1);

      // Política conservadora: la colisión se trata como reutilización y la sesión se revoca.
      const s = await sessionOf(t.refreshToken);
      expect(s.fechaRevocacion).not.toBeNull();
      expect(s.tokenHash).toBe(sha256(ok[0].body.refreshToken)); // nunca hubo una segunda rotación
      await refresh(ok[0].body.refreshToken).expect(401);
    });

    it.each([
      ['se revoca', (id: string) => prisma.sesion.update({ where: { id }, data: { fechaRevocacion: new Date() } })],
      ['expira', (id: string) => prisma.sesion.update({ where: { id }, data: { fechaCreacion: new Date(Date.now() - 7200_000), fechaExpiracion: new Date(Date.now() - 1000) } })],
    ])('si la sesión %s entre la lectura y la rotación, el UPDATE condicional no rota (carrera forzada)', async (_n, mutate) => {
      const a = await newAccount();
      const t = await login(a);
      const id = t.refreshToken.split('.')[0];
      const before = await sessionOf(t.refreshToken);

      const sessionsService = app.get(SessionsService, { strict: false });
      const original = sessionsService.findForRefresh.bind(sessionsService);
      const spy = jest.spyOn(sessionsService, 'findForRefresh').mockImplementation(async (sid: string) => {
        const row = await original(sid); // el servicio ve la sesión todavía válida...
        await mutate(id); // ...y justo después cambia de estado, antes de intentar rotar
        return row;
      });
      const res = await refresh(t.refreshToken);
      spy.mockRestore();

      expect(res.status).toBe(401);
      const after = await sessionOf(t.refreshToken);
      expect(after.tokenHash).toBe(before.tokenHash); // no rotó
      expect(after.ultimoUso).toBeNull();
    });

    it('no registra tokens, secretos ni hashes en los logs', async () => {
      const a = await newAccount();
      const t = await login(a);
      const t1 = (await refresh(t.refreshToken).expect(200)).body;
      await refresh(t.refreshToken).expect(401); // reutilización -> genera el warning
      const logs = output.join('');
      expect(logs).toContain('Posible reutilización de refresh token');
      for (const secret of [t.refreshToken, t.refreshToken.split('.')[1], t1.refreshToken, t1.refreshToken.split('.')[1], t1.accessToken, sha256(t.refreshToken), sha256(t1.refreshToken)]) {
        expect(logs).not.toContain(secret);
      }
    });
  });

  describe('POST /auth/logout', () => {
    it('revoca solo esa sesión (204 sin cuerpo), conserva el registro y no elimina nada', async () => {
      const a = await newAccount();
      const t1 = await login(a, 'Android');
      const t2 = await login(a, 'Windows');
      const mine = { usuarioId: a.id }; // acotado: otras suites usan la misma base
      const total = await prisma.sesion.count({ where: mine });

      const res = await logout(t1.refreshToken).expect(204);
      expect(res.text).toBe('');
      expect(await prisma.sesion.count({ where: mine })).toBe(total); // sin DELETE
      expect((await sessionOf(t1.refreshToken)).fechaRevocacion).not.toBeNull();
      expect((await sessionOf(t2.refreshToken)).fechaRevocacion).toBeNull();
      await refresh(t1.refreshToken).expect(401);
      await refresh(t2.refreshToken).expect(200);
    });

    it('repetir la operación es idempotente (204) y conserva la primera fecha de revocación', async () => {
      const a = await newAccount();
      const t = await login(a);
      await logout(t.refreshToken).expect(204);
      const first = (await sessionOf(t.refreshToken)).fechaRevocacion!;
      await new Promise((r) => setTimeout(r, 30));
      await logout(t.refreshToken).expect(204);
      expect((await sessionOf(t.refreshToken)).fechaRevocacion!.toISOString()).toBe(first.toISOString());
    });

    it('con un secreto incorrecto responde 204 pero NO revoca la sesión', async () => {
      const a = await newAccount();
      const t = await login(a);
      const forged = `${t.refreshToken.split('.')[0]}.${randomBytes(48).toString('base64url')}`;
      await logout(forged).expect(204);
      expect((await sessionOf(t.refreshToken)).fechaRevocacion).toBeNull();
      await refresh(t.refreshToken).expect(200);
    });

    it('sesión ajena: el token de otro usuario solo cierra la sesión de ese otro usuario', async () => {
      const victim = await newAccount();
      const other = await newAccount();
      const tv = await login(victim);
      const to = await login(other);
      await logout(to.refreshToken).expect(204);
      expect((await sessionOf(tv.refreshToken)).fechaRevocacion).toBeNull();
      expect((await sessionOf(to.refreshToken)).fechaRevocacion).not.toBeNull();
      // Un intento de cerrar la sesión ajena sin su secreto no tiene efecto.
      await logout(`${tv.refreshToken.split('.')[0]}.${to.refreshToken.split('.')[1]}`).expect(204);
      expect((await sessionOf(tv.refreshToken)).fechaRevocacion).toBeNull();
    });

    it.each([
      ['sesión inexistente', () => `${randomUUID()}.${randomBytes(48).toString('base64url')}`],
      ['token malformado', () => 'basura'],
      ['UUID inválido', () => `x.${'A'.repeat(64)}`],
    ])('%s: 204 sin efectos', async (_n, make) => {
      const a = await newAccount();
      const t = await login(a);
      await logout(make()).expect(204);
      expect(await prisma.sesion.count({ where: { usuarioId: a.id } })).toBe(1); // nada eliminado
      expect((await sessionOf(t.refreshToken)).fechaRevocacion).toBeNull(); // ni revocado
    });

    it('cuerpo inválido: 400', async () => {
      await http().post('/auth/logout').send({}).expect(400);
      await http().post('/auth/logout').send({ refreshToken: 5 }).expect(400);
      await http().post('/auth/logout').send({ refreshToken: 'x', sessionId: randomUUID() }).expect(400);
    });

    it('alcance: el logout no invalida un access token ya emitido (vence a los 15 min)', async () => {
      const a = await newAccount();
      const t = await login(a);
      await logout(t.refreshToken).expect(204);
      await me(t.accessToken).expect(200); // documentado: solo se impide renovar
    });
  });

  describe('POST /auth/logout-all', () => {
    it('revoca todas las sesiones activas del usuario autenticado y no las de otros', async () => {
      const a = await newAccount();
      const b = await newAccount();
      const sessionsA = [await login(a, 'Android'), await login(a, 'iOS'), await login(a, 'Windows')];
      const sessionsB = [await login(b, 'Android'), await login(b, 'Windows')];
      await logout(sessionsA[0].refreshToken).expect(204); // una ya estaba revocada
      const both = { usuarioId: { in: [a.id, b.id] } }; // acotado: otras suites usan la misma base
      const total = await prisma.sesion.count({ where: both });
      const firstRevocation = (await sessionOf(sessionsA[0].refreshToken)).fechaRevocacion!;

      const res = await http().post('/auth/logout-all').set('Authorization', `Bearer ${sessionsA[2].accessToken}`).expect(200);
      expect(res.body).toEqual({ message: expect.any(String), sesionesRevocadas: 2 }); // solo las activas

      for (const s of sessionsA) expect((await sessionOf(s.refreshToken)).fechaRevocacion).not.toBeNull();
      for (const s of sessionsB) expect((await sessionOf(s.refreshToken)).fechaRevocacion).toBeNull();
      expect((await sessionOf(sessionsA[0].refreshToken)).fechaRevocacion!.toISOString()).toBe(firstRevocation.toISOString());
      expect(await prisma.sesion.count({ where: both })).toBe(total); // nada eliminado

      for (const s of sessionsA) await refresh(s.refreshToken).expect(401);
      for (const s of sessionsB) await refresh(s.refreshToken).expect(200);
    });

    it('la identidad sale del JWT: un usuarioId enviado por el cliente se ignora', async () => {
      const a = await newAccount();
      const victim = await newAccount();
      const ta = await login(a);
      const tv = await login(victim);
      await http().post('/auth/logout-all').set('Authorization', `Bearer ${ta.accessToken}`).send({ usuarioId: victim.id, userId: victim.id }).expect(200);
      expect((await sessionOf(tv.refreshToken)).fechaRevocacion).toBeNull();
      expect((await sessionOf(ta.refreshToken)).fechaRevocacion).not.toBeNull();
    });

    it('es repetible (0 sesiones la segunda vez)', async () => {
      const a = await newAccount();
      const t = await login(a);
      const call = () => http().post('/auth/logout-all').set('Authorization', `Bearer ${t.accessToken}`).expect(200);
      expect((await call()).body.sesionesRevocadas).toBe(1);
      expect((await call()).body.sesionesRevocadas).toBe(0);
    });

    it('exige autenticación', async () => {
      await http().post('/auth/logout-all').expect(401);
      await http().post('/auth/logout-all').set('Authorization', 'Bearer basura').expect(401);
    });
  });

  describe('GET /auth/me', () => {
    it('con un access token válido devuelve datos públicos del usuario y su perfil', async () => {
      const a = await newAccount();
      const t = await login(a);
      const res = await me(t.accessToken).expect(200);
      expect(res.body).toEqual({
        id: a.id,
        username: a.username,
        email: a.email,
        displayName: 'Jugador',
        createdAt: expect.any(String),
        profile: { totalXp: 0, nivelActual: 1, precisionPromedio: 0, avatarConfig: null },
      });
    });

    it('no devuelve campos sensibles ni datos de sesiones', async () => {
      const a = await newAccount();
      const t = await login(a);
      await login(a, 'Android'); // otro dispositivo
      const json = JSON.stringify((await me(t.accessToken).expect(200)).body);
      expect(json).not.toMatch(/passwordHash|password_hash|argon2|tokenHash|token_hash|refresh|sesion|session|isActive/i);
      expect(json).not.toContain(t.refreshToken);
      expect(json).not.toContain(a.password);
    });

    it('sin token, alterado, con otra clave, expirado o con el refresh token: 401', async () => {
      const a = await newAccount();
      const t = await login(a);
      await me().expect(401);
      await me(t.accessToken.slice(0, -3) + (t.accessToken.endsWith('AAA') ? 'BBB' : 'AAA')).expect(401);
      await me(await new JwtService({ secret: 'otra-clave-distinta-0123456789abcdef0123456789' }).signAsync({ sub: a.id, email: a.email }, { expiresIn: 900 })).expect(401);
      await me(await jwt.signAsync({ sub: a.id, email: a.email }, { secret: SECRET, expiresIn: -10 })).expect(401);
      await me(t.refreshToken).expect(401);
    });

    it('usuario inexistente (JWT válido con sub desconocido) o sub inválido: 401, nunca 500', async () => {
      const ghost = await jwt.signAsync({ sub: randomUUID(), email: 'fantasma@example.com' }, { secret: SECRET, expiresIn: 900 });
      await me(ghost).expect(401);
      for (const sub of ['no-es-uuid', "1' OR '1'='1", '', 123, null]) {
        await me(await jwt.signAsync({ sub, email: 'x@example.com' } as never, { secret: SECRET, expiresIn: 900 })).expect(401);
      }
      await me(await jwt.signAsync({ email: 'x@example.com' }, { secret: SECRET, expiresIn: 900 })).expect(401); // sin sub
    });

    it('usuario inactivo: 401 inmediatamente, aunque el access token siga vigente', async () => {
      const a = await newAccount();
      const t = await login(a);
      await me(t.accessToken).expect(200);
      await prisma.usuario.update({ where: { id: a.id }, data: { isActive: false } });
      await me(t.accessToken).expect(401);
      await http().post('/auth/logout-all').set('Authorization', `Bearer ${t.accessToken}`).expect(401);
    });
  });

  it('las operaciones respetan los permisos reales de senas_app: sin DELETE sobre sesiones', async () => {
    const [row] = await prisma.$queryRaw<{ u: string }[]>`SELECT current_user AS u`;
    expect(row.u).toBe('senas_app');
    await expect(prisma.$executeRaw`DELETE FROM sesiones`).rejects.toThrow();
    await expect(prisma.sesion.deleteMany()).rejects.toThrow();
  });
});
