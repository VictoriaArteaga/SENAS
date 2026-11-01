/**
 * Rate limiting de /auth/login, /auth/register y /auth/refresh. No necesita base de datos:
 * PrismaService se sustituye por un doble que simula "usuario inexistente".
 */
import { INestApplication } from '@nestjs/common';
import { Test } from '@nestjs/testing';
import request from 'supertest';
import { App } from 'supertest/types';
import { AppModule } from '../src/app.module';
import { PrismaService } from '../src/prisma/prisma.service';

const LIMITS = { RATE_LIMIT_TTL_SECONDS: '60', RATE_LIMIT_LOGIN: '3', RATE_LIMIT_REGISTER: '2', RATE_LIMIT_REFRESH: '4' };
const sleep = (ms: number) => new Promise((r) => setTimeout(r, ms));

describe('Rate limiting de autenticación', () => {
  let app: INestApplication<App>;
  const saved: Record<string, string | undefined> = {};
  const http = () => request(app.getHttpServer());
  const login = () => http().post('/auth/login').send({ email: 'nadie@example.com', password: 'contraseña-incorrecta' });
  const register = () => http().post('/auth/register').send({}); // 400 por validación, pero cuenta como intento
  const refresh = () => http().post('/auth/refresh').send({ refreshToken: 'basura' });

  beforeAll(async () => {
    for (const k of Object.keys(LIMITS)) saved[k] = process.env[k];
    Object.assign(process.env, LIMITS);

    const prismaDouble = { usuario: { findUnique: jest.fn().mockResolvedValue(null) } };
    const moduleRef = await Test.createTestingModule({ imports: [AppModule] })
      .overrideProvider(PrismaService)
      .useValue(prismaDouble)
      .compile();
    app = moduleRef.createNestApplication();
    await app.init();
  });
  afterAll(async () => {
    for (const [k, v] of Object.entries(saved)) {
      if (v === undefined) delete process.env[k];
      else process.env[k] = v;
    }
    await app.close();
  });

  it('el límite se reinicia al terminar la ventana (ventana de 1 s en register)', async () => {
    process.env.RATE_LIMIT_TTL_SECONDS = '1';
    await register().expect(400);
    await register().expect(400);
    await register().expect(429);
    await sleep(1100);
    await register().expect(400); // ventana nueva
    process.env.RATE_LIMIT_TTL_SECONDS = '60';
    await sleep(1100); // deja expirar el registro creado con la ventana de 1 s
  });

  it('login: tras superar el límite responde 429 con Retry-After y un mensaje que no revela nada', async () => {
    for (let i = 0; i < 3; i++) await login().expect(401);
    const res = await login().expect(429);

    expect(res.body).toEqual({ statusCode: 429, message: 'Demasiados intentos. Inténtalo de nuevo más tarde.' });
    expect(Number(res.headers['retry-after'])).toBeGreaterThan(0);
    expect(Number(res.headers['retry-after'])).toBeLessThanOrEqual(60);
    expect(JSON.stringify(res.body)).not.toMatch(/nadie@example|contraseña|usuario|existe/i);
    await login().expect(429); // sigue bloqueado dentro de la ventana
  });

  it('cada ruta tiene su propio contador: login bloqueado no afecta a register', async () => {
    await register().expect(400);
    await register().expect(400);
    await register().expect(429); // register tiene su límite (2)
  });

  it('refresh se limita por separado (4 intentos)', async () => {
    for (let i = 0; i < 4; i++) await refresh().expect(401);
    const res = await refresh().expect(429);
    expect(res.body.statusCode).toBe(429);
    expect(res.headers['retry-after']).toBeDefined();
  });

  it('no se puede evadir el límite falsificando X-Forwarded-For (sin trust proxy)', async () => {
    await login().set('X-Forwarded-For', '203.0.113.77').expect(429);
    await refresh().set('X-Forwarded-For', '198.51.100.9').expect(429);
  });

  it('las demás rutas no se ven afectadas, aunque se llamen muchas veces', async () => {
    for (let i = 0; i < 30; i++) {
      await http().get('/').expect(200);
      await http().post('/auth/logout').send({ refreshToken: 'basura' }).expect(204);
      await http().get('/auth/me').expect(401); // 401 (no autenticado), no 429
      await http().post('/auth/logout-all').expect(401);
    }
  });
});
