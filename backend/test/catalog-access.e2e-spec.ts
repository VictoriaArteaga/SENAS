/**
 * Aplicación del bloqueo en GET /levels/:id/senas y GET /senas/:id contra un PostgreSQL REAL (local,
 * temporal), con la API conectada como `senas_app`. Se omite sin TEST_DATABASE_URL.
 *
 * Los datos (niveles, señas, vínculos, perfiles y progreso) los inserta el cliente de fixtures con el rol
 * `senas_migrator` y se borran al terminar. Debe ejecutarse en serie y con una base sin otros niveles
 * (test/jest-e2e.json fija maxWorkers: 1): la cadena de progresión depende de TODOS los niveles.
 */
import { ConsoleLogger, INestApplication } from '@nestjs/common';
import { JwtService } from '@nestjs/jwt';
import { Test } from '@nestjs/testing';
import { PrismaPg } from '@prisma/adapter-pg';
import { randomBytes, randomUUID } from 'node:crypto';
import request from 'supertest';
import { App } from 'supertest/types';
import { AppModule } from '../src/app.module';
import { Prisma, PrismaClient } from '../src/generated/prisma/client.js';
import { LevelAccessService } from '../src/levels/level-access.service';
import { LevelsService } from '../src/levels/levels.service';
import { PrismaService } from '../src/prisma/prisma.service';
import { SenasService } from '../src/senas/senas.service';
import { fixturesClient } from './support/fixtures-client';

const APP_URL = process.env.TEST_DATABASE_URL;
const describeDb = APP_URL ? describe : describe.skip;
const SECRET = process.env.JWT_ACCESS_SECRET as string;

type LevelKey = 'L1' | 'L2' | 'L3' | 'L4';
type SenaKey = 's1' | 's2' | 's4' | 'sShared12' | 'sOnlyL2' | 'sShared23' | 'sOnlyL3' | 'sOrphan';
type Progress = [sena: SenaKey, completada: boolean, mejorPrecision: string];
interface Player { id: string; token: string }

const SENA_FIELDS = ['animacion', 'descripcion', 'dificultad', 'id', 'nombre', 'patronReferencia', 'tipo'];
const LEVEL_SENA_FIELDS = [...SENA_FIELDS, 'esObligatoria', 'orden'].sort();
const LEVEL_LOCKED_MESSAGE = 'El nivel todavía no está desbloqueado.';
const SENA_LOCKED_MESSAGE = 'La seña solo pertenece a niveles que todavía no están desbloqueados.';
const PATRON = { version: 2, manos: [{ x: 0.1234567890123, y: -0.5, z: 1e-7 }], nota: 'ñandú ✋ 你好', flags: [true, false] };

describeDb('Aplicación del bloqueo en señas (PostgreSQL local, rol senas_app)', () => {
  let app: INestApplication<App>;
  let prisma: PrismaService;
  let fx: PrismaClient;
  let output: string[];

  const run = randomBytes(3).toString('hex');
  const base = 2_000_000 + Math.floor(Math.random() * 90_000_000);
  const ids = { levels: [] as string[], senas: [] as string[], users: [] as string[] };
  const level = {} as Record<LevelKey, string>;
  const sena = {} as Record<SenaKey, string>;
  const players = {} as Record<string, Player>;
  const http = () => request(app.getHttpServer());
  const get = (path: string, token: string | null) => {
    const r = http().get(path);
    return token ? r.set('Authorization', `Bearer ${token}`) : r;
  };
  const levelSenas = (levelId: string, p: Player) => get(`/levels/${levelId}/senas`, p.token);
  const oneSena = (senaId: string, p: Player) => get(`/senas/${senaId}`, p.token);

  /**
   * Cadena (por orden): L1 (xp 0, min 60) → L2 (xp 100, min 75) → L3 (xp 250, min 80) → L4 (xp 400, min 90, SIN señas).
   * Obligatorias: L1 {s1, s2}, L2 {s4}. sShared12 ∈ L1,L2; sShared23 ∈ L2,L3; sOrphan no pertenece a ningún nivel.
   */
  async function createCatalog() {
    const mk = async (key: LevelKey, n: number, orden: number, xp: number, prec: string) => {
      const row = await fx.nivel.create({ data: { numero: base + n, nombre: `T721_${run}_${key}`, orden: base + orden, xpRequerida: xp, precisionMinima: new Prisma.Decimal(prec) } });
      level[key] = row.id;
      ids.levels.push(row.id);
    };
    for (const [key, n, orden, xp, prec] of [['L3', 3, 30, 250, '80.00'], ['L1', 1, 10, 0, '60.00'], ['L4', 4, 40, 400, '90.00'], ['L2', 2, 20, 100, '75.00']] as const) {
      await mk(key, n, orden, xp, prec); // insertados fuera de orden
    }
    for (const key of ['sOnlyL3', 'sShared23', 's1', 'sOrphan', 's4', 'sShared12', 's2', 'sOnlyL2'] as SenaKey[]) {
      const row = await fx.sena.create({
        data: { nombre: `T721_${run}_${key}`, descripcion: `Descripción de ${key}`, tipo: key === 's4' ? 'FRASE' : 'SENA', dificultad: 'MEDIA', animacion: `anim_${key}`, patronReferencia: key === 's1' ? PATRON : undefined },
      });
      sena[key] = row.id;
      ids.senas.push(row.id);
    }
    const link = (l: LevelKey, s: SenaKey, orden: number, esObligatoria: boolean) => fx.nivelSena.create({ data: { nivelId: level[l], senaId: sena[s], orden, esObligatoria } });
    await link('L1', 'sShared12', 3, false);
    await link('L1', 's2', 2, true);
    await link('L1', 's1', 1, true);
    await link('L2', 'sShared23', 4, false);
    await link('L2', 's4', 1, true);
    await link('L2', 'sShared12', 2, false);
    await link('L2', 'sOnlyL2', 3, false);
    await link('L3', 'sOnlyL3', 2, false);
    await link('L3', 'sShared23', 1, false);
  }

  async function newPlayer(name: string, opts: { totalXp?: number; progress?: Progress[]; withoutProfile?: boolean } = {}): Promise<Player> {
    const username = `${name}_${run}_${randomBytes(2).toString('hex')}`.toLowerCase();
    const creds = { username, email: `${username}@example.com`, password: 'Contraseña-Segura-123', displayName: 'Jugador' };
    const id = (await http().post('/auth/register').send(creds).expect(201)).body.user.id as string;
    ids.users.push(id);
    const token = (await http().post('/auth/login').send({ email: creds.email, password: creds.password }).expect(200)).body.accessToken as string;
    if (opts.totalXp !== undefined) await fx.perfil.update({ where: { usuarioId: id }, data: { totalXp: opts.totalXp } });
    for (const [s, completada, mejor] of opts.progress ?? []) {
      await fx.progresoSena.create({ data: { usuarioId: id, senaId: sena[s], completada, mejorPrecision: new Prisma.Decimal(mejor), vecesPracticada: 1 } });
    }
    if (opts.withoutProfile) await fx.perfil.delete({ where: { usuarioId: id } });
    return { id, token };
  }

  const expectLocked = (res: request.Response, message: string) => {
    expect(res.status).toBe(403);
    expect(res.body).toEqual({ statusCode: 403, code: 'LEVEL_LOCKED', message, error: 'Forbidden' });
    expect(JSON.stringify(res.body)).not.toMatch(/prisma|P20\d\d|SELECT|invocation|motivos|XP_|T721_/i); // sin datos internos ni contenido
  };

  const snapshot = async () =>
    JSON.stringify({
      niveles: await fx.nivel.findMany({ where: { id: { in: ids.levels } }, orderBy: { id: 'asc' } }),
      senas: await fx.sena.findMany({ where: { id: { in: ids.senas } }, orderBy: { id: 'asc' } }),
      links: await fx.nivelSena.findMany({ where: { nivelId: { in: ids.levels } }, orderBy: { id: 'asc' } }),
      perfiles: await fx.perfil.findMany({ where: { usuarioId: { in: ids.users } }, orderBy: { id: 'asc' } }),
      progreso: await fx.progresoSena.findMany({ where: { usuarioId: { in: ids.users } }, orderBy: { id: 'asc' } }),
      evaluaciones: await fx.evaluacion.count({ where: { usuarioId: { in: ids.users } } }),
    });

  beforeAll(async () => {
    output = [];
    const capture = (chunk: unknown) => (output.push(String(chunk)), true);
    jest.spyOn(process.stdout, 'write').mockImplementation(capture as never);
    jest.spyOn(process.stderr, 'write').mockImplementation(capture as never);

    fx = fixturesClient();
    const foreign = await fx.nivel.count();
    if (foreign > 0) throw new Error(`La base de pruebas tiene ${foreign} niveles ajenos: la cadena de progresión no sería la esperada. Recrea la base de pruebas.`);
    await createCatalog();

    const moduleRef = await Test.createTestingModule({ imports: [AppModule] }).setLogger(new ConsoleLogger()).compile();
    app = moduleRef.createNestApplication();
    await app.init();
    prisma = app.get(PrismaService);

    players.novato = await newPlayer('novato'); // 0 XP, sin progreso: solo L1
    players.nivel2 = await newPlayer('nivel2', { totalXp: 100, progress: [['s1', true, '80.00'], ['s2', true, '60.00']] }); // L1 y L2
    players.precisionBaja = await newPlayer('precbaja', { totalXp: 500, progress: [['s1', true, '100.00'], ['s2', true, '59.99']] }); // XP de sobra, s2 bajo el umbral
    players.xpInsuficiente = await newPlayer('xpinsuf', { totalXp: 99, progress: [['s1', true, '90.00'], ['s2', true, '90.00']] }); // precisión ok, falta 1 XP
    players.saltador = await newPlayer('saltador', { totalXp: 5000, progress: [['s4', true, '100.00']] }); // L2 "hecho" sin haber hecho L1
    players.completo = await newPlayer('completo', { totalXp: 5000, progress: [['s1', true, '80.00'], ['s2', true, '80.00'], ['s4', true, '80.00']] }); // L1..L4
    players.sinPerfil = await newPlayer('sinperfil', { withoutProfile: true });
  });

  afterAll(async () => {
    await app?.close();
    await fx?.nivelSena.deleteMany({ where: { nivelId: { in: ids.levels } } });
    await fx?.progresoSena.deleteMany({ where: { usuarioId: { in: ids.users } } });
    await fx?.nivel.deleteMany({ where: { id: { in: ids.levels } } });
    await fx?.sena.deleteMany({ where: { id: { in: ids.senas } } });
    await fx?.$disconnect();
    jest.restoreAllMocks();
  });

  describe('GET /levels/:id/senas', () => {
    it('nivel desbloqueado: 200 con las señas en el orden de NivelSena y el contrato de siempre', async () => {
      const res = await levelSenas(level.L1, players.novato).expect(200);
      expect(Object.keys(res.body)).toEqual(['data']);
      const data = res.body.data as Array<Record<string, unknown>>;
      expect(data.map((s) => [s.orden, s.id, s.esObligatoria])).toEqual([[1, sena.s1, true], [2, sena.s2, true], [3, sena.sShared12, false]]); // insertadas como 3, 2, 1
      for (const item of data) expect(Object.keys(item).sort()).toEqual(LEVEL_SENA_FIELDS);
      expect(data[0]).toEqual({ id: sena.s1, nombre: `T721_${run}_s1`, descripcion: 'Descripción de s1', tipo: 'SENA', dificultad: 'MEDIA', animacion: 'anim_s1', patronReferencia: PATRON, orden: 1, esObligatoria: true });
    });

    it.each([
      ['L2', 'novato (0 XP, sin progreso)'],
      ['L3', 'novato'],
      ['L4', 'novato'],
    ])('nivel %s bloqueado para %s: 403 LEVEL_LOCKED sin entregar contenido', async (levelKey) => {
      expectLocked(await levelSenas(level[levelKey as LevelKey], players.novato), LEVEL_LOCKED_MESSAGE);
    });

    it('XP suficiente pero precisión insuficiente (59.99 < 60) en una obligatoria del nivel anterior: L2 bloqueado', async () => {
      expectLocked(await levelSenas(level.L2, players.precisionBaja), LEVEL_LOCKED_MESSAGE);
      await levelSenas(level.L1, players.precisionBaja).expect(200); // el anterior sigue accesible
    });

    it('precisión suficiente pero un punto de XP menos (99 < 100): L2 bloqueado', async () => {
      expectLocked(await levelSenas(level.L2, players.xpInsuficiente), LEVEL_LOCKED_MESSAGE);
    });

    it('progresión acumulativa: con XP de sobra y L2 "hecho", sin completar L1 se bloquean L2, L3 y L4', async () => {
      for (const key of ['L2', 'L3', 'L4'] as LevelKey[]) expectLocked(await levelSenas(level[key], players.saltador), LEVEL_LOCKED_MESSAGE);
    });

    it('al cumplir la regla se abre exactamente el nivel correspondiente', async () => {
      const res = await levelSenas(level.L2, players.nivel2).expect(200);
      expect((res.body.data as Array<{ id: string }>).map((s) => s.id)).toEqual([sena.s4, sena.sShared12, sena.sOnlyL2, sena.sShared23]);
      expectLocked(await levelSenas(level.L3, players.nivel2), LEVEL_LOCKED_MESSAGE); // L3 sigue cerrado
    });

    it('nivel desbloqueado SIN señas: 200 con lista vacía', async () => {
      expect((await levelSenas(level.L4, players.completo).expect(200)).body).toEqual({ data: [] });
    });

    it('nivel inexistente: 404; UUID inválido: 400; UUID canónico no RFC: 404', async () => {
      expect((await levelSenas(randomUUID(), players.novato).expect(404)).body).toMatchObject({ statusCode: 404, message: 'Nivel no encontrado.' });
      for (const bad of ['no-uuid', '123', "1'%20OR%20'1'='1", 'null']) {
        expect((await get(`/levels/${bad}/senas`, players.novato.token).expect(400)).body.message).toBe('El identificador no es un UUID válido.');
      }
      await get('/levels/00000000-0000-0000-0000-000000000000/senas', players.novato.token).expect(404);
    });

    it('usuario sin perfil: 404 controlado, no se crea el perfil y no se entrega contenido', async () => {
      const res = await levelSenas(level.L1, players.sinPerfil).expect(404);
      expect(res.body).toMatchObject({ statusCode: 404, message: 'Perfil de jugador no encontrado.' });
      expect(JSON.stringify(res.body)).not.toContain('T721_');
      expect(await fx.perfil.count({ where: { usuarioId: players.sinPerfil.id } })).toBe(0);
    });
  });

  describe('GET /senas/:id', () => {
    it('seña de un nivel desbloqueado: 200 con exactamente el contrato actual de la seña', async () => {
      const res = await oneSena(sena.s1, players.novato).expect(200);
      expect(Object.keys(res.body)).toEqual(['data']);
      expect(Object.keys(res.body.data).sort()).toEqual(SENA_FIELDS);
      expect(res.body.data).toEqual({ id: sena.s1, nombre: `T721_${run}_s1`, descripcion: 'Descripción de s1', tipo: 'SENA', dificultad: 'MEDIA', animacion: 'anim_s1', patronReferencia: PATRON });
    });

    it('seña solo de niveles bloqueados: 403 LEVEL_LOCKED sin entregar la seña', async () => {
      for (const key of ['sOnlyL2', 'sOnlyL3', 'sShared23', 's4'] as SenaKey[]) {
        expectLocked(await oneSena(sena[key], players.novato), SENA_LOCKED_MESSAGE);
      }
    });

    it('seña compartida entre un nivel DESBLOQUEADO y uno BLOQUEADO: se permite (basta una relación válida)', async () => {
      // sShared12 ∈ L1 (abierto) y L2 (cerrado para el novato).
      const res = await oneSena(sena.sShared12, players.novato).expect(200);
      expect(res.body.data.id).toBe(sena.sShared12);
      // sShared23 ∈ L2 (abierto para nivel2) y L3 (cerrado): permitido; para el novato (ninguno abierto): 403.
      await oneSena(sena.sShared23, players.nivel2).expect(200);
      expectLocked(await oneSena(sena.sShared23, players.novato), SENA_LOCKED_MESSAGE);
    });

    it('el acceso depende del jugador del JWT: la misma seña es 403 para uno y 200 para otro', async () => {
      expectLocked(await oneSena(sena.sOnlyL2, players.novato), SENA_LOCKED_MESSAGE);
      await oneSena(sena.sOnlyL2, players.nivel2).expect(200);
      expectLocked(await oneSena(sena.sOnlyL3, players.nivel2), SENA_LOCKED_MESSAGE);
      await oneSena(sena.sOnlyL3, players.completo).expect(200);
    });

    it('precisión insuficiente o saltarse la progresión también bloquea las señas de los niveles afectados', async () => {
      expectLocked(await oneSena(sena.sOnlyL2, players.precisionBaja), SENA_LOCKED_MESSAGE);
      expectLocked(await oneSena(sena.sOnlyL3, players.saltador), SENA_LOCKED_MESSAGE);
    });

    it('seña inexistente: 404; UUID inválido: 400', async () => {
      expect((await oneSena(randomUUID(), players.completo).expect(404)).body).toMatchObject({ statusCode: 404, message: 'Seña no encontrada.' });
      expect((await get('/senas/no-uuid', players.completo.token).expect(400)).body.message).toBe('El identificador no es un UUID válido.');
    });

    it('seña sin niveles asociados (política aprobada): 404 "Seña no encontrada." incluso para quien tiene todo desbloqueado', async () => {
      for (const player of [players.novato, players.completo]) {
        const res = await oneSena(sena.sOrphan, player).expect(404);
        expect(res.body).toMatchObject({ statusCode: 404, message: 'Seña no encontrada.' });
        expect(JSON.stringify(res.body)).not.toContain('T721_');
      }
    });

    it('usuario sin perfil: 404 controlado', async () => {
      expect((await oneSena(sena.s1, players.sinPerfil).expect(404)).body).toMatchObject({ message: 'Perfil de jugador no encontrado.' });
    });
  });

  describe('autenticación e identidad (ambos endpoints)', () => {
    const endpoints: Array<[string, () => string]> = [
      ['GET /levels/:id/senas', () => `/levels/${level.L1}/senas`],
      ['GET /senas/:id', () => `/senas/${sena.s1}`],
    ];

    it.each(endpoints)('%s sin token: 401', async (_n, path) => {
      const res = await get(path(), null).expect(401);
      expect(JSON.stringify(res.body)).not.toContain('T721_');
    });

    it.each(endpoints)('%s con credenciales inválidas: 401 (alterado, otra clave, expirado, esquema, refresh token)', async (_n, path) => {
      const token = players.novato.token;
      const claims = { sub: players.novato.id, email: 'x@example.com' };
      await get(path(), 'basura').expect(401);
      await get(path(), token.slice(0, -3) + (token.endsWith('AAA') ? 'BBB' : 'AAA')).expect(401);
      await get(path(), await new JwtService({ secret: 'otra-clave-distinta-0123456789abcdef0123456789' }).signAsync(claims, { expiresIn: 900 })).expect(401);
      await get(path(), await new JwtService({ secret: SECRET }).signAsync(claims, { expiresIn: -10 })).expect(401);
      await http().get(path()).set('Authorization', `Basic ${token}`).expect(401);
      // Un refresh token no se acepta como access token.
      const unique = `refresh_${run}_${randomBytes(2).toString('hex')}`; // único por ejecución de la prueba
      const creds = { email: `${unique}@example.com`, username: unique, password: 'Contraseña-Segura-123', displayName: 'R' };
      await http().post('/auth/register').send(creds).expect(201);
      const refreshToken = (await http().post('/auth/login').send({ email: creds.email, password: creds.password }).expect(200)).body.refreshToken;
      await get(path(), refreshToken).expect(401);
    });

    it.each(endpoints)('%s con la cuenta inactiva: 401 inmediato aunque el JWT siga vigente', async (_n, path) => {
      const p = await newPlayer('inactivo', { totalXp: 1000 });
      await get(path(), p.token).expect(200);
      await prisma.usuario.update({ where: { id: p.id }, data: { isActive: false } });
      await get(path(), p.token).expect(401);
    });

    it('la identidad sale solo del JWT: usuarioId por query, cabeceras o cuerpo no abre contenido bloqueado', async () => {
      const victim = players.completo.id;
      const spoof = `usuarioId=${victim}&userId=${victim}&sub=${victim}`;
      const spoofed = (path: string) =>
        get(`${path}${path.includes('?') ? '&' : '?'}${spoof}`, players.novato.token).set('X-User-Id', victim).set('X-Usuario-Id', victim).send({ usuarioId: victim });
      expectLocked(await spoofed(`/levels/${level.L2}/senas`), LEVEL_LOCKED_MESSAGE);
      expectLocked(await spoofed(`/senas/${sena.sOnlyL2}`), SENA_LOCKED_MESSAGE);
      // Y el otro jugador sigue teniendo su propio acceso con su propio token.
      await levelSenas(level.L2, players.completo).expect(200);
    });

    it('no se puede acceder a contenido bloqueado probando los ids conocidos de todos los niveles y señas', async () => {
      for (const key of ['L2', 'L3', 'L4'] as LevelKey[]) expect((await levelSenas(level[key], players.novato)).status).toBe(403);
      for (const key of ['sOnlyL2', 'sOnlyL3', 'sShared23', 's4'] as SenaKey[]) expect((await oneSena(sena[key], players.novato)).status).toBe(403);
    });
  });

  describe('solo lectura, permisos y contrato', () => {
    it('la API se conecta como senas_app', async () => {
      const [row] = await prisma.$queryRaw<{ u: string }[]>`SELECT current_user AS u`;
      expect(row.u).toBe('senas_app');
    });

    it('ninguna petición (permitida, 403, 404) escribe en la base de datos', async () => {
      const before = await snapshot();
      for (const p of [players.novato, players.nivel2, players.completo, players.saltador]) {
        for (const l of [level.L1, level.L2, level.L3, level.L4, randomUUID()]) await levelSenas(l, p);
        for (const s of [sena.s1, sena.sOnlyL2, sena.sOnlyL3, sena.sShared12, sena.sOrphan, randomUUID()]) await oneSena(s, p);
      }
      expect(await snapshot()).toBe(before);
    });

    it('las consultas son solo SELECT y su número es constante (no depende del jugador ni de las señas)', async () => {
      const sql: string[] = [];
      const logged = new PrismaClient({ adapter: new PrismaPg({ connectionString: APP_URL as string }), log: [{ emit: 'event', level: 'query' }] });
      (logged as unknown as { $on: (e: 'query', cb: (q: { query: string }) => void) => void }).$on('query', (q) => sql.push(q.query));
      try {
        const lv = logged as unknown as PrismaService;
        const service = new SenasService(lv, new LevelAccessService(lv, new LevelsService(lv)));
        const measure = async (fn: () => Promise<unknown>) => {
          sql.length = 0;
          await fn().catch(() => undefined);
          expect(sql.filter((q) => !/^\s*SELECT/i.test(q))).toEqual([]);
          return sql.length;
        };
        // Nivel desbloqueado: 4 (acceso) + 2 (señas) como máximo; las dos formas de jugador usan el mismo número.
        const a = await measure(() => service.findByLevelForPlayer(players.novato.id, level.L1));
        const b = await measure(() => service.findByLevelForPlayer(players.completo.id, level.L1));
        expect(a).toBe(b);
        expect(a).toBeLessThanOrEqual(6);
        // Nivel bloqueado: solo el cálculo de acceso (no se consulta el contenido).
        const locked = await measure(() => service.findByLevelForPlayer(players.novato.id, level.L2));
        expect(locked).toBeLessThan(a);
        // Seña: lectura de la seña (+ niveles) y cálculo de acceso.
        const one = await measure(() => service.findOneForPlayer(players.novato.id, sena.s1));
        expect(one).toBeLessThanOrEqual(6);
      } finally {
        await logged.$disconnect();
      }
    });

    it('GET /levels conserva el contrato de la Fase 7.2 (campos y estados)', async () => {
      const res = await get('/levels', players.nivel2.token).expect(200);
      const mine = (res.body.data as Array<Record<string, unknown> & { id: string }>).filter((n) => ids.levels.includes(n.id));
      expect(mine).toHaveLength(4);
      for (const n of mine) expect(Object.keys(n).sort()).toEqual(['desbloqueado', 'descripcion', 'estado', 'id', 'motivosBloqueo', 'nombre', 'numero', 'orden', 'precisionMinima', 'xpRequerida']);
      expect(mine.map((n) => n.desbloqueado)).toEqual([true, true, false, false]);
    });

    it('los logs no contienen tokens', async () => {
      const logs = output.join('');
      for (const p of Object.values(players)) expect(logs).not.toContain(p.token);
    });
  });
});
