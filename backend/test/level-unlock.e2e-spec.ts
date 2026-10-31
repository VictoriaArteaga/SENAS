/**
 * Integración de las reglas de desbloqueo (GET /levels personalizado) contra un PostgreSQL REAL
 * (local, temporal), con la API conectada como `senas_app`. Se omite sin TEST_DATABASE_URL.
 *
 * Los datos (niveles, señas, progreso y perfiles de los jugadores) los inserta el cliente de fixtures con
 * el rol `senas_migrator` y se borran al terminar. Requiere ejecutarse en serie y con una base sin otros
 * niveles (test/jest-e2e.json fija maxWorkers: 1): la cadena de progresión depende de TODOS los niveles.
 */
import { ConsoleLogger, INestApplication } from '@nestjs/common';
import { JwtService } from '@nestjs/jwt';
import { Test } from '@nestjs/testing';
import { PrismaPg } from '@prisma/adapter-pg';
import { randomBytes } from 'node:crypto';
import request from 'supertest';
import { App } from 'supertest/types';
import { AppModule } from '../src/app.module';
import { Prisma, PrismaClient } from '../src/generated/prisma/client.js';
import { LevelAccessService } from '../src/levels/level-access.service';
import { LevelsService } from '../src/levels/levels.service';
import { PrismaService } from '../src/prisma/prisma.service';
import { fixturesClient } from './support/fixtures-client';

const APP_URL = process.env.TEST_DATABASE_URL;
const describeDb = APP_URL ? describe : describe.skip;
const SECRET = process.env.JWT_ACCESS_SECRET as string;

type LevelKey = 'L1' | 'L2' | 'L3' | 'L4' | 'L5';
type SenaKey = 's1' | 's2' | 's3' | 's4' | 's5' | 's6';
type Progress = [sena: SenaKey, completada: boolean, mejorPrecision: string];
interface Player { id: string; token: string }
type View = Record<string, [estado: string, motivos: string[]]>;

describeDb('Reglas de desbloqueo de niveles (GET /levels, PostgreSQL local, rol senas_app)', () => {
  let app: INestApplication<App>;
  let prisma: PrismaService;
  let fx: PrismaClient;
  let output: string[];

  const run = randomBytes(3).toString('hex');
  const base = 1_000_000 + Math.floor(Math.random() * 900_000); // numero/orden únicos por ejecución
  const ids = { levels: [] as string[], senas: [] as string[], users: [] as string[] };
  const level = {} as Record<LevelKey, { id: string; numero: number }>;
  const sena = {} as Record<SenaKey, string>;
  const keyOf = (id: string) => (Object.keys(level) as LevelKey[]).find((k) => level[k].id === id) ?? id;
  const http = () => request(app.getHttpServer());
  const get = (path: string, token: string | null) => {
    const r = http().get(path);
    return token ? r.set('Authorization', `Bearer ${token}`) : r;
  };

  /**
   * Cadena de progresión (por `orden`):   L1 (xp 0,  min 60) → L2 (xp 100, min 75) → L3 (xp 250, min 80) → L4 (xp 400, min 90)
   * Obligatorias: L1 {s1, s2}  L2 {s4}  L3 ninguna.   No obligatorias: L1 {s3}  L2 {s5}  L3 {s6}.
   */
  async function createCatalog() {
    const mk = async (key: LevelKey, n: number, orden: number, xp: number, prec: string) => {
      const row = await fx.nivel.create({ data: { numero: base + n, nombre: `T72_${run}_${key}`, orden: base + orden, xpRequerida: xp, precisionMinima: new Prisma.Decimal(prec) } });
      level[key] = { id: row.id, numero: row.numero };
      ids.levels.push(row.id);
    };
    // Se insertan L3, L1, L4, L2: el orden de inserción NO es el orden lógico.
    await mk('L3', 3, 30, 250, '80.00');
    await mk('L1', 1, 10, 0, '60.00');
    await mk('L4', 4, 40, 400, '90.00');
    await mk('L2', 2, 20, 100, '75.00');
    for (const key of ['s6', 's4', 's2', 's5', 's1', 's3'] as SenaKey[]) {
      const row = await fx.sena.create({ data: { nombre: `T72_${run}_${key}`, tipo: 'SENA', dificultad: 'FACIL' } });
      sena[key] = row.id;
      ids.senas.push(row.id);
    }
    const link = (l: LevelKey, s: SenaKey, orden: number, esObligatoria: boolean) =>
      fx.nivelSena.create({ data: { nivelId: level[l].id, senaId: sena[s], orden, esObligatoria } });
    await link('L1', 's3', 3, false);
    await link('L1', 's1', 1, true);
    await link('L1', 's2', 2, true);
    await link('L2', 's5', 2, false);
    await link('L2', 's4', 1, true);
    await link('L3', 's6', 1, false);
  }

  async function newPlayer(opts: { totalXp?: number; nivelActual?: number; progress?: Progress[] } = {}): Promise<Player> {
    const username = `j_${run}_${randomBytes(2).toString('hex')}`;
    const creds = { username, email: `${username}@example.com`, password: 'Contraseña-Segura-123', displayName: 'Jugador' };
    const id = (await http().post('/auth/register').send(creds).expect(201)).body.user.id as string;
    ids.users.push(id);
    const token = (await http().post('/auth/login').send({ email: creds.email, password: creds.password }).expect(200)).body.accessToken as string;
    if (opts.totalXp !== undefined || opts.nivelActual !== undefined) {
      await fx.perfil.update({ where: { usuarioId: id }, data: { totalXp: opts.totalXp, nivelActual: opts.nivelActual } });
    }
    for (const [s, completada, mejor] of opts.progress ?? []) {
      await fx.progresoSena.create({ data: { usuarioId: id, senaId: sena[s], completada, mejorPrecision: new Prisma.Decimal(mejor), vecesPracticada: 1 } });
    }
    return { id, token };
  }

  /** Estado de los niveles de la prueba, con las comprobaciones comunes de coherencia y orden. */
  async function view(player: Player, expectedOrder: LevelKey[] = ['L1', 'L2', 'L3', 'L4']): Promise<View> {
    const res = await get('/levels', player.token).expect(200);
    expect(Object.keys(res.body)).toEqual(['data']);
    const data = res.body.data as Array<Record<string, unknown> & { id: string; orden: number; estado: string; desbloqueado: boolean; motivosBloqueo: string[] }>;
    for (let i = 1; i < data.length; i++) expect(data[i].orden).toBeGreaterThan(data[i - 1].orden); // orden global
    const mine = data.filter((n) => ids.levels.includes(n.id));
    expect(mine.map((n) => keyOf(n.id))).toEqual(expectedOrder); // por `orden`, no por inserción
    for (const n of mine) {
      expect(Object.keys(n).sort()).toEqual(['desbloqueado', 'descripcion', 'estado', 'id', 'motivosBloqueo', 'nombre', 'numero', 'orden', 'precisionMinima', 'xpRequerida']);
      expect(n.desbloqueado).toBe(n.motivosBloqueo.length === 0);
      expect(n.estado === 'BLOQUEADO').toBe(!n.desbloqueado);
    }
    expect(mine.filter((n) => n.estado === 'ACTUAL').length).toBeLessThanOrEqual(1);
    return Object.fromEntries(mine.map((n) => [keyOf(n.id), [n.estado, n.motivosBloqueo]])) as View;
  }

  const snapshot = async () =>
    JSON.stringify({
      niveles: await fx.nivel.findMany({ where: { id: { in: ids.levels } }, orderBy: { id: 'asc' } }),
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
  });

  afterAll(async () => {
    await app?.close();
    await fx?.nivelSena.deleteMany({ where: { nivelId: { in: ids.levels } } });
    await fx?.nivel.deleteMany({ where: { id: { in: ids.levels } } });
    await fx?.progresoSena.deleteMany({ where: { usuarioId: { in: ids.users } } });
    await fx?.sena.deleteMany({ where: { id: { in: ids.senas } } });
    await fx?.$disconnect();
    jest.restoreAllMocks();
  });

  describe('escenarios de la regla de desbloqueo', () => {
    it('1. usuario recién registrado (0 XP, sin progreso, nivelActual por defecto): solo el primer nivel; no hay ACTUAL', async () => {
      const p = await newPlayer();
      // Perfil por defecto: totalXp 0 y nivelActual 1, que no coincide con ningún `numero` de los niveles de la prueba.
      expect(await view(p)).toEqual({
        L1: ['DESBLOQUEADO', []],
        L2: ['BLOQUEADO', ['XP_INSUFICIENTE', 'SENAS_OBLIGATORIAS_PENDIENTES']],
        L3: ['BLOQUEADO', ['XP_INSUFICIENTE', 'SENAS_OBLIGATORIAS_PENDIENTES', 'NIVEL_ANTERIOR_BLOQUEADO']],
        L4: ['BLOQUEADO', ['XP_INSUFICIENTE', 'NIVEL_ANTERIOR_BLOQUEADO']],
      });
    });

    it('9. el primer nivel no necesita nivel anterior y es ACTUAL cuando nivelActual coincide con su numero', async () => {
      const p = await newPlayer({ nivelActual: base + 1 });
      const v = await view(p);
      expect(v.L1).toEqual(['ACTUAL', []]);
      expect(v.L2[0]).toBe('BLOQUEADO');
    });

    it('2/6. XP insuficiente aunque la precisión cumpla en todas las obligatorias: solo XP_INSUFICIENTE para L2', async () => {
      const p = await newPlayer({ totalXp: 99, nivelActual: base + 1, progress: [['s1', true, '90.00'], ['s2', true, '90.00']] });
      const v = await view(p);
      expect(v.L2).toEqual(['BLOQUEADO', ['XP_INSUFICIENTE']]);
    });

    it('3. XP suficiente pero sin completar las señas obligatorias: SENAS_OBLIGATORIAS_PENDIENTES', async () => {
      const p = await newPlayer({ totalXp: 100, nivelActual: base + 1 });
      expect((await view(p)).L2).toEqual(['BLOQUEADO', ['SENAS_OBLIGATORIAS_PENDIENTES']]);
    });

    it('4. una seña obligatoria sin registro de progreso basta para bloquear', async () => {
      const p = await newPlayer({ totalXp: 100, nivelActual: base + 1, progress: [['s1', true, '100.00']] });
      expect((await view(p)).L2).toEqual(['BLOQUEADO', ['SENAS_OBLIGATORIAS_PENDIENTES']]);
    });

    it('4b. una seña obligatoria con registro pero sin completar (completada = false) cuenta como pendiente', async () => {
      const p = await newPlayer({ totalXp: 100, nivelActual: base + 1, progress: [['s1', true, '90.00'], ['s2', false, '90.00']] });
      expect((await view(p)).L2).toEqual(['BLOQUEADO', ['SENAS_OBLIGATORIAS_PENDIENTES']]);
    });

    it('5. todas completadas pero una por debajo del umbral (59.99 < 60): PRECISION_INSUFICIENTE; no se compensa con otra al 100 %', async () => {
      const p = await newPlayer({ totalXp: 100, nivelActual: base + 1, progress: [['s1', true, '100.00'], ['s2', true, '59.99']] });
      expect((await view(p)).L2).toEqual(['BLOQUEADO', ['PRECISION_INSUFICIENTE']]);
    });

    it('5b. sin completar y con precisión baja se informan ambos motivos', async () => {
      const p = await newPlayer({ totalXp: 100, nivelActual: base + 1, progress: [['s1', true, '90.00'], ['s2', false, '50.00']] });
      expect((await view(p)).L2).toEqual(['BLOQUEADO', ['SENAS_OBLIGATORIAS_PENDIENTES', 'PRECISION_INSUFICIENTE']]);
    });

    it('7. cumple todos los requisitos (precisión exactamente igual al umbral): L2 DESBLOQUEADO; L3 y L4 siguen bloqueados', async () => {
      const p = await newPlayer({ totalXp: 100, nivelActual: base + 1, progress: [['s1', true, '80.00'], ['s2', true, '60.00']] });
      expect(await view(p)).toEqual({
        L1: ['ACTUAL', []],
        L2: ['DESBLOQUEADO', []],
        L3: ['BLOQUEADO', ['XP_INSUFICIENTE', 'SENAS_OBLIGATORIAS_PENDIENTES']],
        L4: ['BLOQUEADO', ['XP_INSUFICIENTE', 'NIVEL_ANTERIOR_BLOQUEADO']],
      });
    });

    it('8/11. el nivel anterior (L3) sin señas obligatorias solo exige XP; el nivel actual puede no ser el primero', async () => {
      const p = await newPlayer({ totalXp: 1000, nivelActual: base + 2, progress: [['s1', true, '90.00'], ['s2', true, '90.00'], ['s4', true, '90.00']] });
      expect(await view(p)).toEqual({
        L1: ['DESBLOQUEADO', []],
        L2: ['ACTUAL', []],
        L3: ['DESBLOQUEADO', []],
        L4: ['DESBLOQUEADO', []],
      });
    });

    it('8b. sin señas obligatorias en L3, con XP 399 (<400) solo falta la XP de L4', async () => {
      const p = await newPlayer({ totalXp: 399, nivelActual: base + 2, progress: [['s1', true, '90.00'], ['s2', true, '90.00'], ['s4', true, '90.00']] });
      expect((await view(p)).L4).toEqual(['BLOQUEADO', ['XP_INSUFICIENTE']]);
    });

    it('13. practicar señas NO obligatorias no desbloquea nada', async () => {
      const p = await newPlayer({ totalXp: 100, nivelActual: base + 1, progress: [['s3', true, '100.00'], ['s5', true, '100.00']] });
      expect((await view(p)).L2).toEqual(['BLOQUEADO', ['SENAS_OBLIGATORIAS_PENDIENTES']]);
    });

    it('12. varios niveles bloqueados por motivos distintos, todos informados', async () => {
      const p = await newPlayer({ totalXp: 0, nivelActual: base + 1, progress: [['s1', true, '40.00']] });
      const v = await view(p);
      expect(v.L2[1]).toEqual(['XP_INSUFICIENTE', 'SENAS_OBLIGATORIAS_PENDIENTES', 'PRECISION_INSUFICIENTE']);
      expect(v.L3[1]).toEqual(['XP_INSUFICIENTE', 'SENAS_OBLIGATORIAS_PENDIENTES', 'NIVEL_ANTERIOR_BLOQUEADO']);
      expect(v.L4[1]).toEqual(['XP_INSUFICIENTE', 'NIVEL_ANTERIOR_BLOQUEADO']);
    });

    it('no se puede saltar la progresión: con XP y progreso de sobra en niveles altos, un nivel intermedio bloqueado bloquea los siguientes', async () => {
      const p = await newPlayer({ totalXp: 5000, nivelActual: base + 1, progress: [['s4', true, '100.00']] }); // L2 "hecho" sin haber hecho L1
      expect(await view(p)).toEqual({
        L1: ['ACTUAL', []],
        L2: ['BLOQUEADO', ['SENAS_OBLIGATORIAS_PENDIENTES']],
        L3: ['BLOQUEADO', ['NIVEL_ANTERIOR_BLOQUEADO']],
        L4: ['BLOQUEADO', ['NIVEL_ANTERIOR_BLOQUEADO']],
      });
    });

    it('si Perfil.nivelActual apunta a un nivel bloqueado, ese nivel sigue BLOQUEADO (con sus motivos) y no hay ACTUAL', async () => {
      const p = await newPlayer({ totalXp: 0, nivelActual: base + 3 });
      const v = await view(p);
      expect(v.L3[0]).toBe('BLOQUEADO');
      expect(Object.values(v).some(([estado]) => estado === 'ACTUAL')).toBe(false);
    });

    it('si nivelActual no corresponde a ningún nivel, no hay ACTUAL y no falla', async () => {
      const p = await newPlayer({ nivelActual: 999_999_999 });
      expect((await view(p)).L1).toEqual(['DESBLOQUEADO', []]);
    });

    it('los niveles se devuelven por `orden` aunque se insertaron como L3, L1, L4, L2', async () => {
      const inserted = await fx.nivel.findMany({ where: { id: { in: ids.levels } }, orderBy: { createdAt: 'asc' } });
      expect(inserted.map((n) => keyOf(n.id))).toEqual(['L3', 'L1', 'L4', 'L2']); // el orden de inserción realmente fue ese
      const p = await newPlayer();
      await view(p); // view() ya exige ['L1','L2','L3','L4']
    });
  });

  describe('autenticación, identidad y perfil', () => {
    it('sin JWT: 401; con JWT alterado, de otra clave o expirado: 401', async () => {
      const p = await newPlayer();
      await get('/levels', null).expect(401);
      await get('/levels', p.token.slice(0, -3) + (p.token.endsWith('AAA') ? 'BBB' : 'AAA')).expect(401);
      const claims = { sub: p.id, email: 'x@example.com' };
      await get('/levels', await new JwtService({ secret: 'otra-clave-distinta-0123456789abcdef0123456789' }).signAsync(claims, { expiresIn: 900 })).expect(401);
      await get('/levels', await new JwtService({ secret: SECRET }).signAsync(claims, { expiresIn: -10 })).expect(401);
    });

    it('cuenta inactiva: 401 inmediato aunque el JWT siga vigente', async () => {
      const p = await newPlayer();
      await get('/levels', p.token).expect(200);
      await prisma.usuario.update({ where: { id: p.id }, data: { isActive: false } });
      await get('/levels', p.token).expect(401);
    });

    it('la identidad sale solo del JWT: usuarioId por query, cabeceras o cuerpo se ignoran', async () => {
      const novato = await newPlayer();
      const avanzado = await newPlayer({ totalXp: 1000, nivelActual: base + 2, progress: [['s1', true, '90.00'], ['s2', true, '90.00'], ['s4', true, '90.00']] });
      const propio = await view(novato);
      expect(propio.L2[0]).toBe('BLOQUEADO');

      const res = await get(`/levels?usuarioId=${avanzado.id}&userId=${avanzado.id}&sub=${avanzado.id}`, novato.token)
        .set('X-User-Id', avanzado.id)
        .set('X-Usuario-Id', avanzado.id)
        .send({ usuarioId: avanzado.id })
        .expect(200);
      const mine = (res.body.data as Array<{ id: string; estado: string; motivosBloqueo: string[] }>).filter((n) => ids.levels.includes(n.id));
      expect(Object.fromEntries(mine.map((n) => [keyOf(n.id), [n.estado, n.motivosBloqueo]]))).toEqual(propio); // siguen siendo los del JWT
      expect((await view(avanzado)).L2[0]).toBe('ACTUAL'); // y el otro jugador ve lo suyo
    });

    it('10. perfil inexistente: 404 controlado y NO se crea el perfil', async () => {
      const p = await newPlayer();
      await fx.perfil.delete({ where: { usuarioId: p.id } });
      const res = await get('/levels', p.token).expect(404);
      expect(res.body).toMatchObject({ statusCode: 404, message: 'Perfil de jugador no encontrado.' });
      expect(JSON.stringify(res.body)).not.toMatch(/prisma|P20\d\d|findUnique|invocation/i);
      expect(await fx.perfil.count({ where: { usuarioId: p.id } })).toBe(0); // no se creó automáticamente
    });
  });

  describe('solo lectura y datos expuestos', () => {
    it('GET /levels no escribe nada: perfiles, progreso, niveles, vínculos y evaluaciones quedan idénticos', async () => {
      const p = await newPlayer({ totalXp: 100, nivelActual: base + 1, progress: [['s1', true, '80.00'], ['s2', true, '60.00'], ['s3', false, '10.00']] });
      const before = await snapshot();
      for (let i = 0; i < 5; i++) await get('/levels', p.token).expect(200);
      await get('/levels?usuarioId=x', p.token).expect(200);
      expect(await snapshot()).toBe(before);
    });

    it('la respuesta no expone datos internos (XP, progreso, perfil, evaluaciones, sesiones, tokens)', async () => {
      const p = await newPlayer({ totalXp: 4242, nivelActual: base + 1, progress: [['s1', true, '87.65']] });
      const json = JSON.stringify((await get('/levels', p.token).expect(200)).body);
      expect(json).not.toMatch(/totalXp|4242|87\.65|mejorPrecision|completada|senaId|usuario|perfil|passwordHash|tokenHash|refresh|sesion|evaluacion|createdAt|updatedAt/i);
      expect(json).not.toContain(p.token);
    });

    it('la API se conecta como senas_app', async () => {
      const [row] = await prisma.$queryRaw<{ u: string }[]>`SELECT current_user AS u`;
      expect(row.u).toBe('senas_app');
    });

    it('consultas: solo SELECT, y un número constante que no depende del jugador', async () => {
      const novato = await newPlayer();
      const avanzado = await newPlayer({ totalXp: 1000, progress: [['s1', true, '90.00'], ['s2', true, '90.00'], ['s4', true, '90.00']] });
      const sql: string[] = [];
      const logged = new PrismaClient({ adapter: new PrismaPg({ connectionString: APP_URL as string }), log: [{ emit: 'event', level: 'query' }] });
      (logged as unknown as { $on: (e: 'query', cb: (q: { query: string }) => void) => void }).$on('query', (q) => sql.push(q.query));
      try {
        const service = new LevelAccessService(logged as unknown as PrismaService, new LevelsService(logged as unknown as PrismaService));
        const counts: number[] = [];
        for (const player of [novato, avanzado]) {
          sql.length = 0;
          await service.listForPlayer(player.id);
          counts.push(sql.length);
          expect(sql.filter((q) => !/^\s*SELECT/i.test(q))).toEqual([]);
        }
        expect(counts[0]).toBe(counts[1]);
        expect(counts[0]).toBeLessThanOrEqual(4); // perfil + niveles + obligatorias + progreso
      } finally {
        await logged.$disconnect();
      }
    });
  });

  describe('catálogo con numeración inconsistente (numero no crece con orden)', () => {
    it('14. calcula por `orden`, avisa en el log y no falla', async () => {
      // numero base+0 (menor que el de L1..L4) pero orden posterior a L4: numeración incoherente con el orden.
      const extra = await fx.nivel.create({ data: { numero: base, nombre: `T72_${run}_L5`, orden: base + 50, xpRequerida: 0, precisionMinima: new Prisma.Decimal('50.00') } });
      level.L5 = { id: extra.id, numero: extra.numero };
      ids.levels.push(extra.id);

      const p = await newPlayer({ totalXp: 5000, progress: [['s1', true, '90.00'], ['s2', true, '90.00'], ['s4', true, '90.00']] });
      const v = await view(p, ['L1', 'L2', 'L3', 'L4', 'L5']);
      expect(v.L5).toEqual(['DESBLOQUEADO', []]); // su anterior por `orden` es L4 (sin señas obligatorias) y hay XP
      expect(output.join('')).toContain('Catálogo inconsistente');
    });
  });
});
