/**
 * Integración de registro y login contra un PostgreSQL REAL (local y temporal), conectado como el rol
 * de ejecución `senas_app` (privilegios mínimos + RLS). Se omite si no hay TEST_DATABASE_URL.
 *
 *   url=$(test/support/provision-test-db.sh up /ruta/temporal)
 *   TEST_DATABASE_URL="$url" npm run test:e2e
 *   test/support/provision-test-db.sh down /ruta/temporal
 *
 * Nunca usa .env ni DATABASE_URL real (ver test/support/jest.setup.ts).
 */
import { ConsoleLogger, Controller, Get, INestApplication, Module, Req, UseGuards } from '@nestjs/common';
import { JwtService } from '@nestjs/jwt';
import { Test } from '@nestjs/testing';
import { createHash, randomBytes } from 'node:crypto';
import request from 'supertest';
import { App } from 'supertest/types';
import { AppModule } from '../src/app.module';
import { AuthenticatedUser } from '../src/auth/auth.types';
import { AuthModule } from '../src/auth/auth.module';
import { JwtAuthGuard } from '../src/auth/guards/jwt-auth.guard';
import { PrismaService } from '../src/prisma/prisma.service';

@Controller('probe')
class ProbeController {
  @UseGuards(JwtAuthGuard)
  @Get('me')
  me(@Req() req: { user: AuthenticatedUser }) {
    return req.user;
  }
}
@Module({ imports: [AuthModule], controllers: [ProbeController] })
class ProbeModule {}

const describeDb = process.env.TEST_DATABASE_URL ? describe : describe.skip;
const SECRET = process.env.JWT_ACCESS_SECRET as string;

describeDb('Autenticación (integración con PostgreSQL local, rol senas_app)', () => {
  let app: INestApplication<App>;
  let prisma: PrismaService;
  let jwt: JwtService;
  let output: string[];
  const run = randomBytes(3).toString('hex');
  const uniq = (prefix: string) => `${prefix}_${run}_${randomBytes(2).toString('hex')}`;
  const newUser = (over: Record<string, unknown> = {}) => {
    const u = uniq('jugador');
    return { username: u, email: `${u}@example.com`, password: 'Contraseña-Segura-123', displayName: 'Jugador Uno', ...over };
  };
  const register = (body: unknown) => request(app.getHttpServer()).post('/auth/register').send(body as object);
  const login = (body: unknown) => request(app.getHttpServer()).post('/auth/login').send(body as object);
  const sha256 = (s: string) => createHash('sha256').update(s).digest('hex');

  beforeAll(async () => {
    // Captura toda la salida (incluido el arranque) desde antes de crear la aplicación.
    output = [];
    const capture = (chunk: unknown) => (output.push(String(chunk)), true);
    jest.spyOn(process.stdout, 'write').mockImplementation(capture as never);
    jest.spyOn(process.stderr, 'write').mockImplementation(capture as never);
    // ConsoleLogger real: el logger por defecto de las pruebas silencia log/warn y no permitiría comprobar los logs.
    const moduleRef = await Test.createTestingModule({ imports: [AppModule, ProbeModule] }).setLogger(new ConsoleLogger()).compile();
    app = moduleRef.createNestApplication();
    await app.init();
    prisma = app.get(PrismaService);
    jwt = app.get(JwtService, { strict: false });

  });

  afterAll(async () => {
    await app.close();
    jest.restoreAllMocks();
  });

  it('la aplicación se conecta con el rol senas_app (sin DDL ni DELETE)', async () => {
    const [row] = await prisma.$queryRaw<{ u: string }[]>`SELECT current_user AS u`;
    expect(row.u).toBe('senas_app');
    await expect(prisma.$executeRaw`DELETE FROM sesiones`).rejects.toThrow();
    await expect(prisma.$executeRaw`DROP TABLE usuarios`).rejects.toThrow();
  });

  describe('POST /auth/register', () => {
    it('crea Usuario y Perfil (201), guarda un hash Argon2id y no devuelve información sensible', async () => {
      const body = newUser({ email: `  ${uniq('Mixto')}@Example.COM `, displayName: '  María  ' });
      const res = await register(body).expect(201);

      expect(res.body).toEqual({
        message: expect.stringContaining('Cuenta creada'),
        user: { id: expect.any(String), username: body.username, email: (body.email as string).trim().toLowerCase(), displayName: 'María' },
      });
      const json = JSON.stringify(res.body);
      expect(json).not.toMatch(/passwordHash|password_hash|argon2|tokenHash/i);
      expect(json).not.toContain(body.password as string);

      const usuario = await prisma.usuario.findUniqueOrThrow({ where: { id: res.body.user.id }, include: { perfil: true, sesiones: true } });
      expect(usuario.passwordHash).toMatch(/^\$argon2id\$v=19\$/);
      expect(usuario.passwordHash).not.toContain(body.password as string);
      expect(usuario.isActive).toBe(true);
      expect(usuario.email).toBe((body.email as string).trim().toLowerCase());
      // Perfil con los valores por defecto del esquema.
      expect(usuario.perfil).toMatchObject({ displayName: 'María', totalXp: 0, nivelActual: 1, avatarConfig: null });
      expect(Number(usuario.perfil!.precisionPromedio)).toBe(0);
      // El registro no inicia sesión.
      expect(usuario.sesiones).toHaveLength(0);
      expect(res.body).not.toHaveProperty('accessToken');
    });

    it('rechaza email duplicado (también con otra capitalización) sin dejar filas huérfanas', async () => {
      const first = newUser();
      await register(first).expect(201);
      const perfilesDe = () => prisma.perfil.count({ where: { usuario: { email: first.email } } }); // acotado: otras suites usan la misma base
      expect(await perfilesDe()).toBe(1);

      const res = await register({ ...first, username: uniq('otro'), email: first.email.toUpperCase() }).expect(409);
      expect(JSON.stringify(res.body)).not.toMatch(/Prisma|P2002|constraint|usuarios_|unique/i);
      expect(res.body.message).not.toMatch(/correo|email/i); // no confirma que la cuenta exista
      expect(await perfilesDe()).toBe(1);
      expect(await prisma.usuario.count({ where: { email: first.email } })).toBe(1);
    });

    it('rechaza username duplicado (insensible a mayúsculas)', async () => {
      const first = newUser();
      await register(first).expect(201);
      const res = await register({ ...newUser(), username: first.username.toUpperCase() }).expect(409);
      expect(res.body.message).toBe('El nombre de usuario ya está en uso.');
      expect(await prisma.usuario.count({ where: { username: first.username } })).toBe(1);
    });

    it.each([
      ['contraseña demasiado corta', { password: '1234567' }],
      ['email inválido', { email: 'no-es-un-correo' }],
      ['username inválido', { username: 'con espacios' }],
      ['displayName vacío', { displayName: '   ' }],
      ['campo adicional isActive', { isActive: false }],
      ['campo adicional id', { id: '018f0000-0000-7000-8000-000000000001' }],
      ['campo adicional totalXp', { totalXp: 99999 }],
      ['campo adicional passwordHash', { passwordHash: 'x' }],
    ])('400 por %s y no se crea nada', async (_n, over) => {
      const body = newUser(over);
      const res = await register(body).expect(400);
      expect(JSON.stringify(res.body)).not.toContain(body.password as string);
      expect(await prisma.usuario.count({ where: { OR: [{ username: body.username.toLowerCase() }, { email: body.email.toLowerCase() }] } })).toBe(0);
    });

    it('400 si faltan campos obligatorios', async () => {
      await register({}).expect(400);
      await register({ username: 'solo_usuario' }).expect(400);
    });

    it('es atómico: si falla la creación del perfil no queda el Usuario', async () => {
      const body = newUser();
      const real = prisma.$transaction.bind(prisma) as (fn: (tx: any) => Promise<unknown>) => Promise<unknown>;
      const spy = jest.spyOn(prisma, '$transaction').mockImplementation(((fn: (tx: any) => Promise<unknown>) =>
        real((tx) =>
          fn(new Proxy(tx, { get: (t, p) => (p === 'perfil' ? { create: () => Promise.reject(new Error('fallo simulado del perfil')) } : t[p]) })),
        )) as never);

      const res = await register(body).expect(500);
      spy.mockRestore();

      expect(res.body).toEqual({ statusCode: 500, message: 'Error interno del servidor.' });
      expect(await prisma.usuario.count({ where: { username: body.username } })).toBe(0); // rollback
    });
  });

  describe('POST /auth/login', () => {
    let account: ReturnType<typeof newUser>;
    let userId: string;

    beforeAll(async () => {
      account = newUser();
      userId = (await register(account).expect(201)).body.user.id;
    });

    it('credenciales correctas: 200 con tokens y datos públicos, y una sesión nueva en la base', async () => {
      const before = Date.now();
      const device = { plataforma: 'Windows', modelo: 'PC-Prueba', sistemaOperativo: 'Windows 11', versionApp: '0.1.0' };
      const res = await login({ email: account.email.toUpperCase(), password: account.password, informacionDispositivo: device }).expect(200);

      expect(res.body).toEqual({
        tokenType: 'Bearer',
        accessToken: expect.any(String),
        expiresIn: 900,
        refreshToken: expect.any(String),
        refreshTokenExpiresAt: expect.any(String),
        user: { id: userId, username: account.username, email: account.email, displayName: 'Jugador Uno' },
      });

      // --- Persistencia: solo el SHA-256 del refresh token.
      const expectedHash = sha256(res.body.refreshToken);
      const session = await prisma.sesion.findUniqueOrThrow({ where: { tokenHash: expectedHash } });
      expect(session.usuarioId).toBe(userId);
      expect(session.tokenHash).not.toBe(res.body.refreshToken);
      expect(session.fechaRevocacion).toBeNull();
      expect(session.ultimoUso).toBeNull();
      expect(session.informacionDispositivo).toEqual(device);
      const thirtyDays = 30 * 24 * 3600 * 1000;
      expect(session.fechaExpiracion.getTime()).toBeGreaterThanOrEqual(before + thirtyDays - 1000);
      expect(session.fechaExpiracion.getTime()).toBeLessThanOrEqual(Date.now() + thirtyDays + 1000);
      expect(session.fechaExpiracion.toISOString()).toBe(res.body.refreshTokenExpiresAt);
      // El token en claro no existe en ninguna columna de la tabla.
      expect(await prisma.sesion.count({ where: { tokenHash: res.body.refreshToken } })).toBe(0);
      const raw = await prisma.$queryRaw<{ n: bigint }[]>`SELECT count(*) AS n FROM sesiones s WHERE s::text LIKE ${'%' + res.body.refreshToken + '%'}`;
      expect(Number(raw[0].n)).toBe(0);

      // --- Respuesta sin datos internos.
      const json = JSON.stringify(res.body);
      expect(json).not.toMatch(/passwordHash|tokenHash|argon2|isActive/i);
      expect(json).not.toContain(expectedHash);

      // --- Access token: claims previstos y nada más.
      const claims = await jwt.verifyAsync(res.body.accessToken, { secret: SECRET, algorithms: ['HS256'] });
      expect(Object.keys(claims).sort()).toEqual(['email', 'exp', 'iat', 'sub']);
      expect(claims).toMatchObject({ sub: userId, email: account.email });
      expect(claims.exp - claims.iat).toBe(900);
      expect(res.body.accessToken).not.toContain(res.body.refreshToken);
      expect(res.body.refreshToken).not.toBe(res.body.accessToken);
    });

    it('varios dispositivos: cada login crea su propia sesión sin invalidar las anteriores', async () => {
      const a = await login({ email: account.email, password: account.password, informacionDispositivo: { plataforma: 'Android' } }).expect(200);
      const b = await login({ email: account.email, password: account.password, informacionDispositivo: { plataforma: 'iOS' } }).expect(200);
      expect(a.body.refreshToken).not.toBe(b.body.refreshToken);

      const sa = await prisma.sesion.findUniqueOrThrow({ where: { tokenHash: sha256(a.body.refreshToken) } });
      const sb = await prisma.sesion.findUniqueOrThrow({ where: { tokenHash: sha256(b.body.refreshToken) } });
      expect(sa.id).not.toBe(sb.id);
      expect(sa.fechaRevocacion).toBeNull();
      expect(sb.fechaRevocacion).toBeNull();
      expect(await prisma.sesion.count({ where: { usuarioId: userId } })).toBeGreaterThanOrEqual(3);
    });

    it('sin informacionDispositivo la sesión se guarda igualmente', async () => {
      const res = await login({ email: account.email, password: account.password }).expect(200);
      const s = await prisma.sesion.findUniqueOrThrow({ where: { tokenHash: sha256(res.body.refreshToken) } });
      expect(s.informacionDispositivo).toBeNull();
    });

    describe('credenciales inválidas', () => {
      it('email inexistente, contraseña incorrecta y usuario inactivo devuelven el mismo 401 y no crean sesión', async () => {
        const inactive = newUser();
        const inactiveId = (await register(inactive).expect(201)).body.user.id;
        await prisma.usuario.update({ where: { id: inactiveId }, data: { isActive: false } });
        const mine = { usuarioId: { in: [userId, inactiveId] } }; // acotado: otras suites usan la misma base
        const total = await prisma.sesion.count({ where: mine });

        const responses = [
          await login({ email: `nadie_${run}@example.com`, password: 'cualquiera-123' }).expect(401),
          await login({ email: account.email, password: 'contraseña-incorrecta' }).expect(401),
          await login({ email: inactive.email, password: inactive.password }).expect(401), // contraseña correcta, cuenta inactiva
        ];
        expect(new Set(responses.map((r) => JSON.stringify(r.body))).size).toBe(1);
        expect(responses[0].body).toEqual({ statusCode: 401, message: 'Credenciales inválidas.', error: 'Unauthorized' });
        expect(await prisma.sesion.count({ where: mine })).toBe(total);
        expect(JSON.stringify(responses.map((r) => r.body))).not.toMatch(/inactiv|existe|usuario no|password|argon2/i);
      });

      it('no autentica con un hash como contraseña ni con la contraseña de otro usuario', async () => {
        const other = newUser({ password: 'Otra-Contraseña-Distinta-77' });
        await register(other).expect(201);
        const hash = (await prisma.usuario.findUniqueOrThrow({ where: { email: account.email } })).passwordHash;
        await login({ email: account.email, password: hash }).expect(401);
        await login({ email: account.email, password: other.password }).expect(401);
      });

      it('400 ante campos adicionales (también en informacionDispositivo) y tipos inválidos', async () => {
        const ok = { email: account.email, password: account.password };
        await login({ ...ok, isActive: true }).expect(400);
        await login({ ...ok, informacionDispositivo: { plataforma: 'x', ip: '10.0.0.1' } }).expect(400);
        await login({ ...ok, informacionDispositivo: 'texto' }).expect(400);
        await login({ email: 'x', password: 'y' }).expect(400);
        await login({ email: account.email, password: { $ne: '' } }).expect(400); // sin inyección tipo NoSQL
        await login({}).expect(400);
      });
    });
  });

  describe('JwtAuthGuard / JwtStrategy', () => {
    let accessToken: string;
    let refreshToken: string;
    let claims: { sub: string; email: string };

    beforeAll(async () => {
      const u = newUser();
      await register(u).expect(201);
      const res = await login({ email: u.email, password: u.password }).expect(200);
      accessToken = res.body.accessToken;
      refreshToken = res.body.refreshToken;
      claims = { sub: res.body.user.id, email: u.email };
    });

    const get = (token?: string) => {
      const r = request(app.getHttpServer()).get('/probe/me');
      return token ? r.set('Authorization', `Bearer ${token}`) : r;
    };

    it('sin token: 401', () => get().expect(401));
    it('con un access token válido: 200 con { userId, email }', async () => {
      const res = await get(accessToken).expect(200);
      expect(res.body).toEqual({ userId: claims.sub, email: claims.email });
    });
    it('con la firma alterada: 401', () => get(accessToken.slice(0, -3) + (accessToken.endsWith('AAA') ? 'BBB' : 'AAA')).expect(401));
    it('firmado con otra clave: 401', async () => {
      const forged = await new JwtService({ secret: 'otra-clave-distinta-0123456789abcdef0123456789' }).signAsync(claims, { expiresIn: 900 });
      await get(forged).expect(401);
    });
    it('expirado: 401', async () => {
      const expired = await jwt.signAsync(claims, { secret: SECRET, expiresIn: -10 });
      await get(expired).expect(401);
    });
    it('algoritmo "none": 401', async () => {
      const b64 = (o: object) => Buffer.from(JSON.stringify(o)).toString('base64url');
      await get(`${b64({ alg: 'none', typ: 'JWT' })}.${b64({ ...claims, exp: Math.floor(Date.now() / 1000) + 900 })}.`).expect(401);
    });
    it('el refresh token no sirve como access token: 401', () => get(refreshToken).expect(401));
    it('esquema distinto de Bearer: 401', () => request(app.getHttpServer()).get('/probe/me').set('Authorization', `Basic ${accessToken}`).expect(401));
  });

  it('ni los logs ni los errores 500 contienen contraseñas, tokens o hashes', async () => {
    const u = newUser({ password: 'Clave-Que-No-Debe-Loguearse-1' });
    await register(u).expect(201);
    const ok = await login({ email: u.email, password: u.password }).expect(200);
    await login({ email: u.email, password: 'Intento-Fallido-Secreto-9' }).expect(401);

    // Un 500 real (fallo simulado en el repositorio de usuarios) con la contraseña en el cuerpo.
    const spy = jest.spyOn(prisma, '$transaction').mockRejectedValue(
      Object.assign(new Error(`Invalid prisma.usuario.create() invocation: passwordHash "$argon2id$secreto" email "${u.email}"`), { name: 'PrismaClientKnownRequestError' }),
    );
    const res500 = await register(newUser({ password: 'Otra-Clave-Secreta-55' })).expect(500);
    spy.mockRestore();
    expect(JSON.stringify(res500.body)).not.toMatch(/argon2|passwordHash|Prisma/);

    const logs = output.join('');
    const hash = (await prisma.usuario.findUniqueOrThrow({ where: { email: u.email } })).passwordHash;
    for (const secret of [
      'Clave-Que-No-Debe-Loguearse-1', 'Intento-Fallido-Secreto-9', 'Otra-Clave-Secreta-55',
      ok.body.refreshToken, ok.body.accessToken, sha256(ok.body.refreshToken), hash, SECRET, '$argon2id',
    ]) {
      expect(logs).not.toContain(secret);
    }
    expect(logs).toContain('Error no controlado'); // el 500 sí se registró, pero sin datos sensibles
  });
});
