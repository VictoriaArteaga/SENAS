import { ArgumentsHost, ForbiddenException, NotFoundException } from '@nestjs/common';
import { HttpAdapterHost } from '@nestjs/core';
import { AllExceptionsFilter } from '../common/filters/all-exceptions.filter';
import { LevelAccessService } from '../levels/level-access.service';
import { LEVEL_LOCKED_CODE, LevelLockedException } from '../levels/level-locked.exception';
import { LevelsService } from '../levels/levels.service';
import { PrismaService } from '../prisma/prisma.service';
import { SenasService } from './senas.service';

/**
 * Composición REAL de SenasService + LevelAccessService + LevelsService sobre una base de datos simulada en
 * memoria que SOLO ofrece lecturas (cualquier escritura fallaría con un TypeError). Así se prueba la
 * aplicación del bloqueo con las reglas de dominio auténticas, no con una copia.
 *
 * Cadena (por orden): L1 (xp 0, min 60) → L2 (xp 100, min 75) → L3 (xp 250, min 80) → L4 (xp 400, min 90, sin señas)
 * Obligatorias: L1 {s1, s2}, L2 {s4}.
 */
const dec = (n: number) => ({ toNumber: () => n });
const LEVELS = [
  { id: 'L1', numero: 1, nombre: 'N1', descripcion: null, orden: 10, xpRequerida: 0, precisionMinima: dec(60) },
  { id: 'L2', numero: 2, nombre: 'N2', descripcion: null, orden: 20, xpRequerida: 100, precisionMinima: dec(75) },
  { id: 'L3', numero: 3, nombre: 'N3', descripcion: null, orden: 30, xpRequerida: 250, precisionMinima: dec(80) },
  { id: 'L4', numero: 4, nombre: 'N4', descripcion: null, orden: 40, xpRequerida: 400, precisionMinima: dec(90) },
];
const sena = (id: string) => ({ id, nombre: id.toUpperCase(), descripcion: null, tipo: 'SENA', dificultad: 'FACIL', animacion: null, patronReferencia: { v: 1 } });
const SENAS: Record<string, ReturnType<typeof sena>> = Object.fromEntries(['s1', 's2', 's4', 'sShared12', 'sShared23', 'sOnlyL2', 'sOnlyL3', 'sOrphan'].map((id) => [id, sena(id)]));
const LINKS = [
  { nivelId: 'L1', senaId: 's2', orden: 2, esObligatoria: true },
  { nivelId: 'L1', senaId: 's1', orden: 1, esObligatoria: true },
  { nivelId: 'L1', senaId: 'sShared12', orden: 3, esObligatoria: false },
  { nivelId: 'L2', senaId: 's4', orden: 1, esObligatoria: true },
  { nivelId: 'L2', senaId: 'sShared12', orden: 2, esObligatoria: false },
  { nivelId: 'L2', senaId: 'sOnlyL2', orden: 3, esObligatoria: false },
  { nivelId: 'L2', senaId: 'sShared23', orden: 4, esObligatoria: false },
  { nivelId: 'L3', senaId: 'sShared23', orden: 1, esObligatoria: false },
  { nivelId: 'L3', senaId: 'sOnlyL3', orden: 2, esObligatoria: false },
]; // sOrphan no tiene filas; L4 no tiene señas

interface Player { totalXp: number; nivelActual?: number; progress?: Record<string, [boolean, number]> }

function buildFake(players: Record<string, Player | null>) {
  const calls = { perfil: jest.fn(), progreso: jest.fn(), nivelSenasDeNivel: jest.fn(), nivelSenasObligatorias: jest.fn(), senaFind: jest.fn(), nivelFind: jest.fn() };
  const prisma = {
    perfil: {
      findUnique: async (args: { where: { usuarioId: string } }) => {
        calls.perfil(args);
        const p = players[args.where.usuarioId];
        return p ? { totalXp: p.totalXp, nivelActual: p.nivelActual ?? 1 } : null;
      },
    },
    nivel: {
      findMany: async () => [...LEVELS].sort((a, b) => a.orden - b.orden),
      findUnique: async (args: { where: { id: string } }) => (calls.nivelFind(args), LEVELS.find((l) => l.id === args.where.id) ?? null),
    },
    nivelSena: {
      findMany: async (args: { where: { esObligatoria?: boolean; nivelId?: string } }) => {
        if (args.where.esObligatoria) {
          calls.nivelSenasObligatorias(args);
          return LINKS.filter((l) => l.esObligatoria).map(({ nivelId, senaId }) => ({ nivelId, senaId }));
        }
        calls.nivelSenasDeNivel(args);
        return LINKS.filter((l) => l.nivelId === args.where.nivelId)
          .sort((a, b) => a.orden - b.orden)
          .map((l) => ({ orden: l.orden, esObligatoria: l.esObligatoria, sena: SENAS[l.senaId] }));
      },
    },
    progresoSena: {
      findMany: async (args: { where: { usuarioId: string; senaId: { in: string[] } } }) => {
        calls.progreso(args);
        const p = players[args.where.usuarioId];
        return Object.entries(p?.progress ?? {})
          .filter(([senaId]) => args.where.senaId.in.includes(senaId))
          .map(([senaId, [completada, mejor]]) => ({ senaId, completada, mejorPrecision: dec(mejor) }));
      },
    },
    sena: {
      findUnique: async (args: { where: { id: string }; select: { niveles?: unknown } }) => {
        calls.senaFind(args);
        const row = SENAS[args.where.id];
        if (!row) return null;
        return args.select.niveles ? { ...row, niveles: LINKS.filter((l) => l.senaId === row.id).map((l) => ({ nivelId: l.nivelId })) } : row;
      },
    },
  };
  const levels = new LevelsService(prisma as unknown as PrismaService);
  const access = new LevelAccessService(prisma as unknown as PrismaService, levels);
  return { calls, service: new SenasService(prisma as unknown as PrismaService, access) };
}

const OK_L1 = { s1: [true, 80], s2: [true, 60] } as Record<string, [boolean, number]>;
const PLAYERS: Record<string, Player | null> = {
  novato: { totalXp: 0 },
  nivel2: { totalXp: 100, progress: OK_L1 }, // L1 ok → L2 desbloqueado
  precisionBaja: { totalXp: 500, progress: { s1: [true, 100], s2: [true, 59.99] } }, // XP de sobra pero s2 bajo el umbral
  saltador: { totalXp: 5000, progress: { s4: [true, 100] } }, // L2 "hecho" sin L1: no se puede saltar
  completo: { totalXp: 5000, progress: { ...OK_L1, s4: [true, 80] } }, // L1..L4 desbloqueados
  sinPerfil: null,
};

async function failure(promise: Promise<unknown>): Promise<any> {
  try {
    await promise;
  } catch (e) {
    return e;
  }
  throw new Error('Se esperaba un error y la operación terminó bien');
}
const expectLocked = (e: any, message?: string) => {
  expect(e).toBeInstanceOf(ForbiddenException);
  expect(e.getStatus()).toBe(403);
  expect(e.getResponse()).toEqual({ statusCode: 403, code: LEVEL_LOCKED_CODE, message: message ?? 'El nivel todavía no está desbloqueado.', error: 'Forbidden' });
};

describe('LevelLockedException', () => {
  it('cuerpo estable con código LEVEL_LOCKED y sin datos internos', () => {
    expect(new LevelLockedException().getResponse()).toEqual({
      statusCode: 403, code: 'LEVEL_LOCKED', message: 'El nivel todavía no está desbloqueado.', error: 'Forbidden',
    });
  });
  it('el filtro global lo entrega tal cual por HTTP', () => {
    const reply = jest.fn();
    const filter = new AllExceptionsFilter({ httpAdapter: { reply } } as unknown as HttpAdapterHost);
    filter.catch(new LevelLockedException(), { switchToHttp: () => ({ getResponse: () => 'res' }) } as unknown as ArgumentsHost);
    expect(reply).toHaveBeenCalledWith('res', { statusCode: 403, code: 'LEVEL_LOCKED', message: 'El nivel todavía no está desbloqueado.', error: 'Forbidden' }, 403);
  });
});

describe('GET /levels/:id/senas — findByLevelForPlayer', () => {
  it('nivel desbloqueado: devuelve las señas en el orden de NivelSena con los campos de siempre', async () => {
    const { service } = buildFake(PLAYERS);
    const out = await service.findByLevelForPlayer('novato', 'L1');
    expect(out.map((s) => [s.orden, s.id, s.esObligatoria])).toEqual([[1, 's1', true], [2, 's2', true], [3, 'sShared12', false]]);
    expect(Object.keys(out[0]).sort()).toEqual(['animacion', 'descripcion', 'dificultad', 'esObligatoria', 'id', 'nombre', 'orden', 'patronReferencia', 'tipo']);
  });

  it('nivel bloqueado: 403 LEVEL_LOCKED y NO se consultan las señas del nivel', async () => {
    const { service, calls } = buildFake(PLAYERS);
    expectLocked(await failure(service.findByLevelForPlayer('novato', 'L2')));
    expect(calls.nivelSenasDeNivel).not.toHaveBeenCalled(); // el contenido no se toca
  });

  it('nivel inexistente: 404', async () => {
    const { service } = buildFake(PLAYERS);
    const e = await failure(service.findByLevelForPlayer('novato', 'NO-EXISTE'));
    expect(e).toBeInstanceOf(NotFoundException);
    expect(e.getResponse()).toMatchObject({ statusCode: 404, message: 'Nivel no encontrado.' });
  });

  it('nivel desbloqueado sin señas: lista vacía y sin consulta adicional de existencia', async () => {
    const { service, calls } = buildFake(PLAYERS);
    await expect(service.findByLevelForPlayer('completo', 'L4')).resolves.toEqual([]);
    expect(calls.nivelFind).not.toHaveBeenCalled(); // ya se sabe que existe
  });

  it('usuario sin perfil: 404 de perfil, sin crearlo y sin devolver contenido', async () => {
    const { service, calls } = buildFake(PLAYERS);
    const e = await failure(service.findByLevelForPlayer('sinPerfil', 'L1'));
    expect(e).toBeInstanceOf(NotFoundException);
    expect(e.getResponse()).toMatchObject({ message: 'Perfil de jugador no encontrado.' });
    expect(calls.nivelSenasDeNivel).not.toHaveBeenCalled();
  });

  it('XP suficiente pero precisión insuficiente en una seña obligatoria del nivel anterior: L2 bloqueado', async () => {
    const { service } = buildFake(PLAYERS);
    expectLocked(await failure(service.findByLevelForPlayer('precisionBaja', 'L2')));
    await expect(service.findByLevelForPlayer('precisionBaja', 'L1')).resolves.toHaveLength(3); // el anterior sigue accesible
  });

  it('progresión acumulativa: aunque cumpla las condiciones propias de L3, con L2 bloqueado no accede a L3', async () => {
    const { service } = buildFake(PLAYERS);
    expectLocked(await failure(service.findByLevelForPlayer('saltador', 'L2')));
    expectLocked(await failure(service.findByLevelForPlayer('saltador', 'L3')));
    expectLocked(await failure(service.findByLevelForPlayer('saltador', 'L4')));
  });

  it('un nivel se abre cuando se cumple la regla (nivel2 accede a L2 pero no a L3)', async () => {
    const { service } = buildFake(PLAYERS);
    await expect(service.findByLevelForPlayer('nivel2', 'L2')).resolves.toHaveLength(4);
    expectLocked(await failure(service.findByLevelForPlayer('nivel2', 'L3')));
  });

  it('la identidad usada en las consultas es SIEMPRE el userId recibido', async () => {
    const { service, calls } = buildFake(PLAYERS);
    await service.findByLevelForPlayer('nivel2', 'L2');
    expect(calls.perfil).toHaveBeenCalledWith({ where: { usuarioId: 'nivel2' }, select: { totalXp: true, nivelActual: true } });
    expect(calls.progreso.mock.calls[0][0].where.usuarioId).toBe('nivel2');
  });
});

describe('GET /senas/:id — findOneForPlayer', () => {
  it('seña de un nivel desbloqueado: se entrega con exactamente el contrato actual (sin la relación)', async () => {
    const { service } = buildFake(PLAYERS);
    const out = await service.findOneForPlayer('novato', 's1');
    expect(out).toEqual({ id: 's1', nombre: 'S1', descripcion: null, tipo: 'SENA', dificultad: 'FACIL', animacion: null, patronReferencia: { v: 1 } });
    expect(Object.keys(out)).not.toContain('niveles');
  });

  it('seña solo de niveles bloqueados: 403 LEVEL_LOCKED', async () => {
    const { service } = buildFake(PLAYERS);
    expectLocked(await failure(service.findOneForPlayer('novato', 'sOnlyL2')), 'La seña solo pertenece a niveles que todavía no están desbloqueados.');
    expectLocked(await failure(service.findOneForPlayer('novato', 'sOnlyL3')), 'La seña solo pertenece a niveles que todavía no están desbloqueados.');
  });

  it('seña en niveles bloqueados Y desbloqueados: basta uno desbloqueado (acceso permitido)', async () => {
    const { service } = buildFake(PLAYERS);
    // sShared12 está en L1 (desbloqueado) y L2 (bloqueado para el novato).
    await expect(service.findOneForPlayer('novato', 'sShared12')).resolves.toMatchObject({ id: 'sShared12' });
    // sShared23 está en L2 y L3: con L2 abierto y L3 cerrado también se permite.
    await expect(service.findOneForPlayer('nivel2', 'sShared23')).resolves.toMatchObject({ id: 'sShared23' });
    // Pero si ninguno de sus niveles está abierto, 403.
    expectLocked(await failure(service.findOneForPlayer('novato', 'sShared23')), 'La seña solo pertenece a niveles que todavía no están desbloqueados.');
  });

  it('seña inexistente: 404 sin calcular el acceso', async () => {
    const { service, calls } = buildFake(PLAYERS);
    const e = await failure(service.findOneForPlayer('novato', 'NO-EXISTE'));
    expect(e).toBeInstanceOf(NotFoundException);
    expect(e.getResponse()).toMatchObject({ statusCode: 404, message: 'Seña no encontrada.' });
    expect(calls.perfil).not.toHaveBeenCalled();
  });

  it('seña sin niveles asociados (política aprobada): 404 "Seña no encontrada." sin calcular el acceso, aunque el jugador lo tenga todo', async () => {
    const { service, calls } = buildFake(PLAYERS);
    const e = await failure(service.findOneForPlayer('completo', 'sOrphan'));
    expect(e).toBeInstanceOf(NotFoundException);
    expect(e.getResponse()).toMatchObject({ statusCode: 404, message: 'Seña no encontrada.' });
    expect(calls.perfil).not.toHaveBeenCalled();
  });

  it('usuario sin perfil: 404 de perfil', async () => {
    const { service } = buildFake(PLAYERS);
    const e = await failure(service.findOneForPlayer('sinPerfil', 's1'));
    expect(e).toBeInstanceOf(NotFoundException);
    expect(e.getResponse()).toMatchObject({ message: 'Perfil de jugador no encontrado.' });
  });

  it('el acceso depende del jugador: la misma seña es 403 para uno y 200 para otro', async () => {
    const { service } = buildFake(PLAYERS);
    expectLocked(await failure(service.findOneForPlayer('novato', 'sOnlyL2')), 'La seña solo pertenece a niveles que todavía no están desbloqueados.');
    await expect(service.findOneForPlayer('nivel2', 'sOnlyL2')).resolves.toMatchObject({ id: 'sOnlyL2' });
  });

  it('con precisión insuficiente o saltándose la progresión, las señas de los niveles afectados siguen bloqueadas', async () => {
    const { service } = buildFake(PLAYERS);
    expectLocked(await failure(service.findOneForPlayer('precisionBaja', 'sOnlyL2')), 'La seña solo pertenece a niveles que todavía no están desbloqueados.');
    expectLocked(await failure(service.findOneForPlayer('saltador', 'sOnlyL3')), 'La seña solo pertenece a niveles que todavía no están desbloqueados.');
  });

  it('solo pide a la base de datos lo necesario de la seña: campos públicos + ids de nivel', async () => {
    const { service, calls } = buildFake(PLAYERS);
    await service.findOneForPlayer('novato', 's1');
    expect(calls.senaFind.mock.calls[0][0].select).toEqual({
      id: true, nombre: true, descripcion: true, tipo: true, dificultad: true, animacion: true, patronReferencia: true,
      niveles: { select: { nivelId: true } },
    });
  });
});
