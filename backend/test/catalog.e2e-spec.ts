/**
 * Integración del catálogo educativo (GET /levels, /levels/:id, /levels/:id/senas, /senas/:id) contra un
 * PostgreSQL REAL (local, temporal), conectado como `senas_app` (solo SELECT sobre estas tablas).
 * Se omite sin TEST_DATABASE_URL. Ver auth.e2e-spec.ts para levantar la base de pruebas.
 *
 * `senas_app` NO puede escribir en niveles/senas/nivel_senas, así que los datos de prueba los inserta un
 * cliente de FIXTURES con el rol propietario `senas_migrator` (mismo host/base, solo en loopback) y los
 * borra al terminar. La aplicación bajo prueba nunca usa ese cliente.
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
import { PrismaService } from '../src/prisma/prisma.service';
import { LevelAccessService } from '../src/levels/level-access.service';
import { LevelsService } from '../src/levels/levels.service';
import { SenasService } from '../src/senas/senas.service';

const APP_URL = process.env.TEST_DATABASE_URL;
const describeDb = APP_URL ? describe : describe.skip;
const SECRET = process.env.JWT_ACCESS_SECRET as string;

const LEVEL_FIELDS = ['descripcion', 'id', 'nombre', 'numero', 'orden', 'precisionMinima', 'xpRequerida'];
const SENA_FIELDS = ['animacion', 'descripcion', 'dificultad', 'id', 'nombre', 'patronReferencia', 'tipo'];
const LEVEL_SENA_FIELDS = [...SENA_FIELDS, 'esObligatoria', 'orden'].sort();

/** Cliente con el rol propietario SOLO para preparar y limpiar datos. Rechaza hosts que no sean locales. */
function fixturesClient(): PrismaClient {
  const url = new URL(APP_URL as string);
  if (!['127.0.0.1', 'localhost', '::1', '[::1]'].includes(url.hostname)) {
    throw new Error('Los fixtures solo pueden ejecutarse contra una base de datos local.');
  }
  url.username = 'senas_migrator';
  url.password = '';
  return new PrismaClient({ adapter: new PrismaPg({ connectionString: url.toString() }) });
}

describeDb('Catálogo educativo (PostgreSQL local, rol senas_app)', () => {
  let app: INestApplication<App>;
  let prisma: PrismaService;
  let fx: PrismaClient;
  let token: string;
  let output: string[];

  const run = randomBytes(3).toString('hex');
  const base = 500_000 + Math.floor(Math.random() * 400_000); // numero/orden únicos por ejecución
  const ids = { levels: [] as string[], senas: [] as string[] };
  const L = {} as Record<'A' | 'B' | 'C', Prisma.NivelGetPayload<object>>;
  const S = {} as Record<'hola' | 'gracias' | 'porFavor' | 'solo', Prisma.SenaGetPayload<object>>;

  const PATRON_HOLA = {
    version: 2,
    manos: [{ x: 0.1234567890123, y: -0.5, z: 1e-7 }],
    trayectoria: [[0, 1], [2.5, 3]],
    expresion: null,
    nota: 'ñandú ✋ 你好',
    flags: [true, false],
  };

  const http = () => request(app.getHttpServer());
  // `jwt = null` envía la petición SIN Authorization (un `undefined` activaría el valor por defecto).
  const get = (path: string, jwt: string | null = token) => {
    const r = http().get(path);
    return jwt ? r.set('Authorization', `Bearer ${jwt}`) : r;
  };
  const snapshot = async () =>
    JSON.stringify({
      niveles: await fx.nivel.findMany({ orderBy: { id: 'asc' } }),
      senas: await fx.sena.findMany({ orderBy: { id: 'asc' } }),
      nivelSenas: await fx.nivelSena.findMany({ orderBy: { id: 'asc' } }),
    });

  beforeAll(async () => {
    output = [];
    const capture = (chunk: unknown) => (output.push(String(chunk)), true);
    jest.spyOn(process.stdout, 'write').mockImplementation(capture as never);
    jest.spyOn(process.stderr, 'write').mockImplementation(capture as never);

    fx = fixturesClient();
    const mkLevel = (key: 'A' | 'B' | 'C', over: { numero: number; nombre: string; orden: number; descripcion: string | null; xp: number; prec: string }) =>
      fx.nivel
        .create({ data: { numero: base + over.numero, nombre: `T7_${run}_${over.nombre}`, descripcion: over.descripcion, orden: base + over.orden, xpRequerida: over.xp, precisionMinima: new Prisma.Decimal(over.prec) } })
        .then((n) => ((L[key] = n), ids.levels.push(n.id)));
    // Se insertan A, B, C pero su `orden` es B(10) < C(20) < A(30): el orden de inserción no es el orden pedido.
    await mkLevel('A', { numero: 1, nombre: 'A', orden: 30, descripcion: 'Nivel con tres señas', xp: 300, prec: '72.50' });
    await mkLevel('B', { numero: 2, nombre: 'B', orden: 10, descripcion: null, xp: 0, prec: '0.00' });
    await mkLevel('C', { numero: 3, nombre: 'C', orden: 20, descripcion: 'Comparte una seña con A', xp: 120, prec: '99.99' });

    const mkSena = (key: keyof typeof S, data: Omit<Prisma.SenaCreateInput, 'nombre'> & { nombre: string }) =>
      fx.sena.create({ data: { ...data, nombre: `T7_${run}_${data.nombre}` } }).then((s) => ((S[key] = s), ids.senas.push(s.id)));
    await mkSena('hola', { nombre: 'HOLA', descripcion: 'Saludo', tipo: 'SENA', dificultad: 'FACIL', animacion: 'anim_hola', patronReferencia: PATRON_HOLA });
    await mkSena('gracias', { nombre: 'GRACIAS', descripcion: null, tipo: 'FRASE', dificultad: 'DIFICIL', animacion: null }); // patrón null
    await mkSena('porFavor', { nombre: 'POR FAVOR', descripcion: 'Cortesía', tipo: 'SENA', dificultad: 'MEDIA', animacion: 'anim_porfavor', patronReferencia: [{ frame: 0 }, { frame: 1 }] });
    await mkSena('solo', { nombre: 'SOLO_EN_C', descripcion: 'Solo en el nivel C', tipo: 'SENA', dificultad: 'FACIL', animacion: 'anim_solo' });

    // Nivel A (insertadas fuera de orden): orden 3, 1, 2. Nivel C: comparte HOLA con otro orden y flag.
    await fx.nivelSena.create({ data: { nivelId: L.A.id, senaId: S.porFavor.id, orden: 3, esObligatoria: false } });
    await fx.nivelSena.create({ data: { nivelId: L.A.id, senaId: S.hola.id, orden: 1, esObligatoria: true } });
    await fx.nivelSena.create({ data: { nivelId: L.A.id, senaId: S.gracias.id, orden: 2, esObligatoria: true } });
    await fx.nivelSena.create({ data: { nivelId: L.C.id, senaId: S.hola.id, orden: 5, esObligatoria: false } });
    await fx.nivelSena.create({ data: { nivelId: L.C.id, senaId: S.solo.id, orden: 4, esObligatoria: true } });

    const moduleRef = await Test.createTestingModule({ imports: [AppModule] }).setLogger(new ConsoleLogger()).compile();
    app = moduleRef.createNestApplication();
    await app.init();
    prisma = app.get(PrismaService);

    const username = `jugador_${run}`;
    const creds = { username, email: `${username}@example.com`, password: 'Contraseña-Segura-123', displayName: 'Jugador' };
    const playerId = (await http().post('/auth/register').send(creds).expect(201)).body.user.id as string;
    // 7.2.1: las rutas de señas aplican el bloqueo. Este jugador cumple los requisitos de TODOS los niveles de la
    // prueba (cadena B → C → A): XP de sobra y la única seña obligatoria de C (`solo`) completada al 100 %.
    await fx.perfil.update({ where: { usuarioId: playerId }, data: { totalXp: 100_000 } });
    await fx.progresoSena.create({ data: { usuarioId: playerId, senaId: S.solo.id, completada: true, mejorPrecision: new Prisma.Decimal('100.00'), vecesPracticada: 1 } });
    token = (await http().post('/auth/login').send({ email: creds.email, password: creds.password }).expect(200)).body.accessToken;
  });

  afterAll(async () => {
    await app?.close();
    // Limpieza de fixtures (solo lo creado por esta ejecución).
    await fx?.nivelSena.deleteMany({ where: { nivelId: { in: ids.levels } } });
    await fx?.progresoSena.deleteMany({ where: { senaId: { in: ids.senas } } });
    await fx?.sena.deleteMany({ where: { id: { in: ids.senas } } });
    await fx?.nivel.deleteMany({ where: { id: { in: ids.levels } } });
    await fx?.$disconnect();
    jest.restoreAllMocks();
  });

  it('la aplicación usa el rol senas_app y NO puede escribir en las tablas educativas', async () => {
    const before = await snapshot();
    const [row] = await prisma.$queryRaw<{ u: string }[]>`SELECT current_user AS u`;
    expect(row.u).toBe('senas_app');

    await expect(prisma.$executeRaw`INSERT INTO niveles (id, numero, nombre, orden, updated_at) VALUES (${randomUUID()}::uuid, 999999991, 'x', 999999991, now())`).rejects.toThrow();
    await expect(prisma.$executeRaw`UPDATE senas SET nombre = 'hackeada'`).rejects.toThrow();
    await expect(prisma.$executeRaw`UPDATE niveles SET nombre = 'hackeado'`).rejects.toThrow();
    await expect(prisma.$executeRaw`DELETE FROM nivel_senas`).rejects.toThrow();
    await expect(prisma.$executeRaw`DELETE FROM senas`).rejects.toThrow();
    expect(await snapshot()).toBe(before); // nada cambió
  });

  describe('autenticación obligatoria en los cuatro endpoints', () => {
    const endpoints: Array<[string, () => string]> = [
      ['GET /levels', () => '/levels'],
      ['GET /levels/:id', () => `/levels/${L.A.id}`],
      ['GET /levels/:id/senas', () => `/levels/${L.A.id}/senas`],
      ['GET /senas/:id', () => `/senas/${S.hola.id}`],
    ];

    it.each(endpoints)('%s sin token: 401', async (_n, path) => {
      const res = await get(path(), null).expect(401);
      expect(JSON.stringify(res.body)).not.toMatch(/T7_|HOLA|Primeros/);
    });

    it.each(endpoints)('%s con credenciales inválidas: 401', async (_n, path) => {
      const jwt = new JwtService({ secret: SECRET });
      const claims = { sub: randomUUID(), email: 'x@example.com' };
      await get(path(), 'basura').expect(401);
      await get(path(), token.slice(0, -3) + (token.endsWith('AAA') ? 'BBB' : 'AAA')).expect(401); // firma alterada
      await get(path(), await jwt.signAsync(claims, { expiresIn: 900 })).expect(401); // firmado con otra... mismo secreto pero usuario inexistente
      await get(path(), await new JwtService({ secret: 'otra-clave-distinta-0123456789abcdef0123456789' }).signAsync(claims, { expiresIn: 900 })).expect(401);
      await get(path(), await jwt.signAsync(claims, { expiresIn: -10 })).expect(401); // expirado
      await http().get(path()).set('Authorization', `Basic ${token}`).expect(401); // esquema equivocado
    });

    it('un refresh token no sirve como access token', async () => {
      const username = `otro_${run}`;
      const c = { username, email: `${username}@example.com`, password: 'Contraseña-Segura-123', displayName: 'Otro' };
      await http().post('/auth/register').send(c).expect(201);
      const refreshToken = (await http().post('/auth/login').send({ email: c.email, password: c.password }).expect(200)).body.refreshToken;
      await get('/levels', refreshToken).expect(401);
    });

    it('una cuenta inactiva pierde el acceso al catálogo aunque su JWT siga vigente', async () => {
      const username = `inactivo_${run}`;
      const c = { username, email: `${username}@example.com`, password: 'Contraseña-Segura-123', displayName: 'Inactivo' };
      const id = (await http().post('/auth/register').send(c).expect(201)).body.user.id;
      const jwt = (await http().post('/auth/login').send({ email: c.email, password: c.password }).expect(200)).body.accessToken;
      await get('/levels', jwt).expect(200);
      await prisma.usuario.update({ where: { id }, data: { isActive: false } });
      await get('/levels', jwt).expect(401);
    });
  });

  describe('GET /levels', () => {
    it('200 con { data: [...] } ordenado por `orden` (no por orden de inserción) y datos iguales a la base', async () => {
      const res = await get('/levels').expect(200);
      expect(res.headers['content-type']).toMatch(/application\/json/);
      expect(Object.keys(res.body)).toEqual(['data']);

      const data: Array<{ id: string; orden: number }> = res.body.data;
      for (let i = 1; i < data.length; i++) expect(data[i].orden).toBeGreaterThan(data[i - 1].orden); // orden global ascendente

      const mine = data.filter((n) => ids.levels.includes(n.id));
      expect(mine.map((n) => n.id)).toEqual([L.B.id, L.C.id, L.A.id]); // B(10) < C(20) < A(30)

      // Mismos datos que la base, leídos de forma independiente.
      const dbRows = await fx.nivel.findMany({ orderBy: { orden: 'asc' } });
      expect(data.map((n) => n.id)).toEqual(dbRows.map((n) => n.id));
    });

    it('cada nivel tiene exactamente los campos públicos con sus tipos reales', async () => {
      const data = (await get('/levels').expect(200)).body.data as Array<Record<string, unknown>>;
      const a = data.find((n) => n.id === L.A.id)!;
      const b = data.find((n) => n.id === L.B.id)!;
      // 7.2: /levels añade la disponibilidad del jugador a los campos públicos del catálogo (que se mantienen).
      expect(Object.keys(a).sort()).toEqual([...LEVEL_FIELDS, 'desbloqueado', 'estado', 'motivosBloqueo'].sort());
      expect(a).toMatchObject({ id: L.A.id, numero: base + 1, nombre: `T7_${run}_A`, descripcion: 'Nivel con tres señas', orden: base + 30, xpRequerida: 300, precisionMinima: 72.5 });
      expect(['ACTUAL', 'DESBLOQUEADO', 'BLOQUEADO']).toContain(a.estado);
      expect(typeof a.desbloqueado).toBe('boolean');
      expect(Array.isArray(a.motivosBloqueo)).toBe(true);
      expect(typeof a.precisionMinima).toBe('number'); // DECIMAL de la base entregado como número
      expect(b).toMatchObject({ descripcion: null, xpRequerida: 0, precisionMinima: 0 });
      expect(data.find((n) => n.id === L.C.id)).toMatchObject({ precisionMinima: 99.99 });
    });

    it('ignora parámetros de consulta (incluida cualquier identidad del cliente)', async () => {
      const normal = (await get('/levels').expect(200)).body;
      const conQuery = (await get(`/levels?usuarioId=${randomUUID()}&userId=1&orden=desc&limit=1`).expect(200)).body;
      expect(conQuery).toEqual(normal);
    });
  });

  describe('GET /levels/:id', () => {
    it('200 con { data: nivel }', async () => {
      const res = await get(`/levels/${L.C.id}`).expect(200);
      expect(Object.keys(res.body)).toEqual(['data']);
      expect(Object.keys(res.body.data).sort()).toEqual(LEVEL_FIELDS);
      expect(res.body.data).toEqual({ id: L.C.id, numero: base + 3, nombre: `T7_${run}_C`, descripcion: 'Comparte una seña con A', orden: base + 20, xpRequerida: 120, precisionMinima: 99.99 });
    });

    it('acepta el UUID en mayúsculas', async () => {
      await get(`/levels/${L.A.id.toUpperCase()}`).expect(200);
    });

    it.each(['no-es-uuid', '123', "1'%20OR%20'1'='1", 'null', '%00', `${'a'.repeat(40)}`])('UUID inválido (%s): 400 en español', async (bad) => {
      const res = await get(`/levels/${bad}`).expect(400);
      expect(res.body).toMatchObject({ statusCode: 400, message: 'El identificador no es un UUID válido.' });
    });

    it('un UUID de formato canónico pero no RFC (p. ej. de un seed manual) llega a la base: 404, no 400', async () => {
      for (const id of ['00000000-0000-0000-0000-000000000000', '11111111-1111-1111-1111-111111111111']) {
        await get(`/levels/${id}`).expect(404);
        await get(`/levels/${id}/senas`).expect(404);
        await get(`/senas/${id}`).expect(404);
      }
    });

    it('nivel inexistente: 404 sin detalles internos', async () => {
      const res = await get(`/levels/${randomUUID()}`).expect(404);
      expect(res.body).toMatchObject({ statusCode: 404, message: 'Nivel no encontrado.' });
      expect(JSON.stringify(res.body)).not.toMatch(/prisma|P20\d\d|findUnique|SELECT|invocation/i);
    });
  });

  describe('GET /levels/:id/senas', () => {
    it('devuelve las señas en el orden de NivelSena (1, 2, 3), no en el de inserción (3, 1, 2)', async () => {
      const res = await get(`/levels/${L.A.id}/senas`).expect(200);
      expect(Object.keys(res.body)).toEqual(['data']);
      const data = res.body.data as Array<{ id: string; nombre: string; orden: number; esObligatoria: boolean }>;
      expect(data.map((s) => [s.orden, s.id])).toEqual([[1, S.hola.id], [2, S.gracias.id], [3, S.porFavor.id]]);
      expect(data.map((s) => s.esObligatoria)).toEqual([true, true, false]);

      // Contraste con la base leída de forma independiente.
      const rows = await fx.nivelSena.findMany({ where: { nivelId: L.A.id }, orderBy: { orden: 'asc' }, include: { sena: true } });
      expect(data.map((s) => s.id)).toEqual(rows.map((r) => r.senaId));
    });

    it('cada elemento tiene los campos de la seña más orden y esObligatoria, con los valores reales', async () => {
      const data = (await get(`/levels/${L.A.id}/senas`).expect(200)).body.data as Array<Record<string, unknown>>;
      for (const item of data) expect(Object.keys(item).sort()).toEqual(LEVEL_SENA_FIELDS);
      expect(data[0]).toEqual({
        id: S.hola.id, nombre: `T7_${run}_HOLA`, descripcion: 'Saludo', tipo: 'SENA', dificultad: 'FACIL',
        animacion: 'anim_hola', patronReferencia: PATRON_HOLA, orden: 1, esObligatoria: true,
      });
      expect(data[1]).toMatchObject({ tipo: 'FRASE', dificultad: 'DIFICIL', descripcion: null, animacion: null, patronReferencia: null });
      expect(data[2]).toMatchObject({ dificultad: 'MEDIA', patronReferencia: [{ frame: 0 }, { frame: 1 }] });
    });

    it('relación N:M: la misma seña aparece en otro nivel con su propio orden y obligatoriedad', async () => {
      const c = (await get(`/levels/${L.C.id}/senas`).expect(200)).body.data as Array<{ id: string; orden: number; esObligatoria: boolean }>;
      expect(c.map((s) => [s.orden, s.id, s.esObligatoria])).toEqual([[4, S.solo.id, true], [5, S.hola.id, false]]);
    });

    it('nivel existente sin señas: 200 con lista vacía', async () => {
      const res = await get(`/levels/${L.B.id}/senas`).expect(200);
      expect(res.body).toEqual({ data: [] });
    });

    it('nivel inexistente: 404; UUID inválido: 400', async () => {
      expect((await get(`/levels/${randomUUID()}/senas`).expect(404)).body).toMatchObject({ statusCode: 404, message: 'Nivel no encontrado.' });
      expect((await get('/levels/xyz/senas').expect(400)).body.message).toBe('El identificador no es un UUID válido.');
    });

    it('patronReferencia llega sin transformar (valores, anidación y Unicode idénticos a la base)', async () => {
      const dbHola = await fx.sena.findUniqueOrThrow({ where: { id: S.hola.id } });
      const data = (await get(`/levels/${L.A.id}/senas`).expect(200)).body.data as Array<{ patronReferencia: unknown }>;
      expect(data[0].patronReferencia).toEqual(dbHola.patronReferencia);
      expect(JSON.parse(JSON.stringify(data[0].patronReferencia))).toEqual(PATRON_HOLA);
    });

    it('una sola llamada a Prisma por petición y un número CONSTANTE de sentencias SQL, solo SELECT (sin N+1)', async () => {
      const spyNs = jest.spyOn(prisma.nivelSena, 'findMany');
      const spyNivel = jest.spyOn(prisma.nivel, 'findUnique');
      const spySena = jest.spyOn(prisma.sena, 'findUnique');
      await get(`/levels/${L.A.id}/senas`).expect(200);
      // 7.2.1: además de las señas del nivel (where.nivelId), el cálculo de acceso lee las obligatorias (where.esObligatoria).
      const wheres = spyNs.mock.calls.map((call) => (call[0] as { where: Record<string, unknown> }).where);
      expect(wheres.filter((w) => 'nivelId' in w)).toHaveLength(1); // las señas del nivel: UNA consulta
      expect(wheres.filter((w) => w.esObligatoria === true)).toHaveLength(1); // reglas de acceso: UNA consulta
      expect(spyNivel).not.toHaveBeenCalled(); // con resultados no se consulta el nivel aparte
      expect(spySena).not.toHaveBeenCalled(); // ninguna consulta por seña
      spyNs.mockRestore();
      spyNivel.mockRestore();
      spySena.mockRestore();

      // Mide las sentencias SQL reales con un cliente propio (rol senas_app) que registra las consultas.
      const sql: string[] = [];
      const logged = new PrismaClient({ adapter: new PrismaPg({ connectionString: APP_URL as string }), log: [{ emit: 'event', level: 'query' }] });
      (logged as unknown as { $on: (e: 'query', cb: (q: { query: string }) => void) => void }).$on('query', (q) => sql.push(q.query));
      try {
        const service = new SenasService(logged as unknown as PrismaService, new LevelAccessService(logged as unknown as PrismaService, new LevelsService(logged as unknown as PrismaService)));
        const counts: Record<string, number> = {};
        for (const [key, level] of [['2 señas (C)', L.C], ['3 señas (A)', L.A], ['sin señas (B)', L.B]] as const) {
          sql.length = 0;
          await service.findByLevel(level.id);
          counts[key] = sql.length;
          expect(sql.filter((q) => !/^\s*SELECT/i.test(q))).toEqual([]); // ninguna sentencia que no sea SELECT
        }
        expect(counts['2 señas (C)']).toBe(counts['3 señas (A)']); // no crece con el número de señas
        expect(counts['3 señas (A)']).toBe(2); // nivel_senas + señas en lote (IN), pase lo que pase con el número de señas
        // Nivel vacío: Prisma lanza igualmente la consulta de señas (IN vacío) y se añade la comprobación del nivel.
        expect(counts['sin señas (B)']).toBe(3);
      } finally {
        await logged.$disconnect();
      }
    });
  });

  describe('GET /senas/:id', () => {
    it('200 con { data: seña } y solo campos públicos', async () => {
      const res = await get(`/senas/${S.hola.id}`).expect(200);
      expect(Object.keys(res.body)).toEqual(['data']);
      expect(Object.keys(res.body.data).sort()).toEqual(SENA_FIELDS);
      expect(res.body.data).toEqual({
        id: S.hola.id, nombre: `T7_${run}_HOLA`, descripcion: 'Saludo', tipo: 'SENA', dificultad: 'FACIL', animacion: 'anim_hola', patronReferencia: PATRON_HOLA,
      });
    });

    it('campos opcionales nulos llegan como null', async () => {
      const res = await get(`/senas/${S.gracias.id}`).expect(200);
      expect(res.body.data).toMatchObject({ descripcion: null, animacion: null, patronReferencia: null, tipo: 'FRASE', dificultad: 'DIFICIL' });
    });

    it('UUID inválido: 400; inexistente: 404', async () => {
      expect((await get('/senas/no-uuid').expect(400)).body.message).toBe('El identificador no es un UUID válido.');
      expect((await get(`/senas/${randomUUID()}`).expect(404)).body).toMatchObject({ statusCode: 404, message: 'Seña no encontrada.' });
    });
  });

  describe('seguridad y solo lectura', () => {
    it('ninguna respuesta expone datos internos ni de autenticación', async () => {
      const bodies = [
        (await get('/levels').expect(200)).body,
        (await get(`/levels/${L.A.id}`).expect(200)).body,
        (await get(`/levels/${L.A.id}/senas`).expect(200)).body,
        (await get(`/senas/${S.hola.id}`).expect(200)).body,
      ];
      const json = JSON.stringify(bodies);
      expect(json).not.toMatch(/createdAt|updatedAt|created_at|updated_at|passwordHash|tokenHash|argon2|refresh|usuario|session|isActive|evaluacion|progreso/i);
      expect(json).not.toContain(token);
    });

    it('no existen endpoints de escritura: POST, PUT, PATCH y DELETE responden 404', async () => {
      const before = await snapshot();
      const paths = ['/levels', `/levels/${L.A.id}`, `/levels/${L.A.id}/senas`, `/senas/${S.hola.id}`, '/senas'];
      for (const path of paths) {
        for (const method of ['post', 'put', 'patch', 'delete'] as const) {
          await http()[method](path).set('Authorization', `Bearer ${token}`).send({ nombre: 'hackeado' }).expect(404);
        }
      }
      expect(await snapshot()).toBe(before);
    });

    it('tras todas las consultas los datos educativos siguen intactos', async () => {
      const before = await snapshot();
      for (const path of ['/levels', `/levels/${L.A.id}`, `/levels/${L.A.id}/senas`, `/levels/${L.B.id}/senas`, `/senas/${S.hola.id}`]) {
        await get(path).expect(200);
      }
      expect(await snapshot()).toBe(before);
    });

    it('los logs no contienen tokens ni credenciales', async () => {
      const logs = output.join('');
      expect(logs).not.toContain(token);
      expect(logs).not.toContain('Contraseña-Segura-123');
    });
  });
});
