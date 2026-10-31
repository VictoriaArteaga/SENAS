import { NotFoundException } from '@nestjs/common';
import { Prisma } from '../generated/prisma/client.js';
import { PrismaService } from '../prisma/prisma.service';
import { LevelAccessService } from './level-access.service';
import { LevelLockedException } from './level-locked.exception';
import { LevelResponse, LevelsService } from './levels.service';

const USER = '018f0000-0000-7000-8000-0000000000aa';
const lvl = (id: string, numero: number, orden: number, xpRequerida: number, precisionMinima: number): LevelResponse => ({
  id, numero, orden, nombre: `Nivel ${id}`, descripcion: null, xpRequerida, precisionMinima,
});
const LEVELS = [lvl('L1', 1, 10, 0, 60), lvl('L2', 2, 20, 100, 75), lvl('L3', 3, 30, 250, 80)];
const dec = (v: string) => new Prisma.Decimal(v);

describe('LevelAccessService', () => {
  // Solo existen métodos de LECTURA: cualquier create/update/delete/upsert/$executeRaw fallaría con TypeError.
  let perfil: { findUnique: jest.Mock };
  let nivelSena: { findMany: jest.Mock };
  let progresoSena: { findMany: jest.Mock };
  let levelsService: { findAll: jest.Mock };
  let service: LevelAccessService;

  beforeEach(() => {
    perfil = { findUnique: jest.fn().mockResolvedValue({ totalXp: 0, nivelActual: 1 }) };
    nivelSena = { findMany: jest.fn().mockResolvedValue([]) };
    progresoSena = { findMany: jest.fn().mockResolvedValue([]) };
    levelsService = { findAll: jest.fn().mockResolvedValue(LEVELS) };
    service = new LevelAccessService(
      { perfil, nivelSena, progresoSena } as unknown as PrismaService,
      levelsService as unknown as LevelsService,
    );
  });

  describe('consultas (solo lectura, con select explícito y sin N+1)', () => {
    beforeEach(() => {
      nivelSena.findMany.mockResolvedValue([
        { nivelId: 'L1', senaId: 's1' },
        { nivelId: 'L1', senaId: 's2' },
        { nivelId: 'L2', senaId: 's4' },
        { nivelId: 'L9', senaId: 's1' }, // misma seña en otro nivel: no se duplica en la consulta de progreso
      ]);
    });

    it('perfil por usuarioId del JWT con solo los campos necesarios', async () => {
      await service.listForPlayer(USER);
      expect(perfil.findUnique).toHaveBeenCalledTimes(1);
      expect(perfil.findUnique).toHaveBeenCalledWith({ where: { usuarioId: USER }, select: { totalXp: true, nivelActual: true } });
    });

    it('señas obligatorias en UNA consulta, solo ids', async () => {
      await service.listForPlayer(USER);
      expect(nivelSena.findMany).toHaveBeenCalledTimes(1);
      expect(nivelSena.findMany).toHaveBeenCalledWith({ where: { esObligatoria: true }, select: { nivelId: true, senaId: true } });
    });

    it('progreso en UNA consulta filtrada por el usuario y por las señas obligatorias (sin repetidas)', async () => {
      await service.listForPlayer(USER);
      expect(progresoSena.findMany).toHaveBeenCalledTimes(1);
      expect(progresoSena.findMany).toHaveBeenCalledWith({
        where: { usuarioId: USER, senaId: { in: ['s1', 's2', 's4'] } },
        select: { senaId: true, completada: true, mejorPrecision: true },
      });
    });

    it('no consulta el progreso si ningún nivel tiene señas obligatorias', async () => {
      nivelSena.findMany.mockResolvedValue([]);
      await service.listForPlayer(USER);
      expect(progresoSena.findMany).not.toHaveBeenCalled();
    });

    it('el número de consultas no depende de cuántos niveles hay', async () => {
      levelsService.findAll.mockResolvedValue(Array.from({ length: 40 }, (_, i) => lvl(`N${i}`, i + 1, (i + 1) * 10, i * 10, 60)));
      await service.listForPlayer(USER);
      expect(perfil.findUnique).toHaveBeenCalledTimes(1);
      expect(nivelSena.findMany).toHaveBeenCalledTimes(1);
      expect(progresoSena.findMany).toHaveBeenCalledTimes(1);
      expect(levelsService.findAll).toHaveBeenCalledTimes(1);
    });

    it('solo usa métodos de lectura de Prisma', async () => {
      const used = new Set<string>();
      for (const [model, mock] of Object.entries({ perfil, nivelSena, progresoSena })) {
        for (const [method, fn] of Object.entries(mock)) if ((fn as jest.Mock).mock.calls.length >= 0) used.add(`${model}.${method}`);
      }
      expect([...used].sort()).toEqual(['nivelSena.findMany', 'perfil.findUnique', 'progresoSena.findMany']);
    });
  });

  describe('perfil inexistente', () => {
    it('404 controlado y NO crea el perfil', async () => {
      perfil.findUnique.mockResolvedValue(null);
      const err = await service.listForPlayer(USER).catch((e) => e);
      expect(err).toBeInstanceOf(NotFoundException);
      expect(err.getResponse()).toMatchObject({ statusCode: 404, message: 'Perfil de jugador no encontrado.' });
      // El doble de Prisma no tiene métodos de escritura: si se hubiera intentado crear, habría un TypeError distinto.
      expect(Object.keys(perfil)).toEqual(['findUnique']);
    });
  });

  describe('respuesta', () => {
    it('conserva los campos del catálogo y añade estado, desbloqueado y motivosBloqueo', async () => {
      const out = await service.listForPlayer(USER);
      expect(Object.keys(out[0]).sort()).toEqual(['id', 'numero', 'nombre', 'descripcion', 'orden', 'xpRequerida', 'precisionMinima', 'estado', 'desbloqueado', 'motivosBloqueo'].sort());
      expect(out[0]).toMatchObject({ id: 'L1', estado: 'ACTUAL', desbloqueado: true, motivosBloqueo: [] });
    });

    it('mantiene el orden por `orden` que entrega el catálogo', async () => {
      expect((await service.listForPlayer(USER)).map((l) => l.id)).toEqual(['L1', 'L2', 'L3']);
    });

    it('integra XP, señas obligatorias y precisión (Decimal de la base convertido a número)', async () => {
      perfil.findUnique.mockResolvedValue({ totalXp: 100, nivelActual: 1 });
      nivelSena.findMany.mockResolvedValue([{ nivelId: 'L1', senaId: 's1' }, { nivelId: 'L1', senaId: 's2' }]);
      progresoSena.findMany.mockResolvedValue([
        { senaId: 's1', completada: true, mejorPrecision: dec('60.00') }, // = umbral: cumple
        { senaId: 's2', completada: true, mejorPrecision: dec('59.99') }, // una centésima por debajo
      ]);
      const out = await service.listForPlayer(USER);
      expect(out.find((l) => l.id === 'L2')).toMatchObject({ estado: 'BLOQUEADO', motivosBloqueo: ['PRECISION_INSUFICIENTE'] });

      progresoSena.findMany.mockResolvedValue([
        { senaId: 's1', completada: true, mejorPrecision: dec('60.00') },
        { senaId: 's2', completada: true, mejorPrecision: dec('60.00') },
      ]);
      expect((await service.listForPlayer(USER)).find((l) => l.id === 'L2')).toMatchObject({ estado: 'DESBLOQUEADO', desbloqueado: true });
    });

    it('no incluye datos internos (perfil, progreso, usuario)', async () => {
      perfil.findUnique.mockResolvedValue({ totalXp: 777, nivelActual: 1 });
      nivelSena.findMany.mockResolvedValue([{ nivelId: 'L1', senaId: 's1' }]);
      progresoSena.findMany.mockResolvedValue([{ senaId: 's1', completada: true, mejorPrecision: dec('88.00') }]);
      const json = JSON.stringify(await service.listForPlayer(USER));
      expect(json).not.toMatch(/totalXp|mejorPrecision|completada|senaId|usuario|777|88/);
    });
  });

  describe('assertLevelUnlocked / unlockedLevelIds (aplicación del bloqueo)', () => {
    // L1 (xp 0), L2 (xp 100, requiere obligatorias de L1), L3 (xp 250)
    beforeEach(() => {
      nivelSena.findMany.mockResolvedValue([{ nivelId: 'L1', senaId: 's1' }]);
    });

    it('nivel desbloqueado: no lanza', async () => {
      await expect(service.assertLevelUnlocked(USER, 'L1')).resolves.toBeUndefined();
    });

    it('nivel bloqueado: 403 LEVEL_LOCKED', async () => {
      const err = await service.assertLevelUnlocked(USER, 'L2').catch((e) => e);
      expect(err).toBeInstanceOf(LevelLockedException);
      expect(err.getStatus()).toBe(403);
      expect(err.getResponse()).toMatchObject({ code: 'LEVEL_LOCKED' });
    });

    it('nivel inexistente: 404', async () => {
      const err = await service.assertLevelUnlocked(USER, 'NO-EXISTE').catch((e) => e);
      expect(err).toBeInstanceOf(NotFoundException);
      expect(err.getResponse()).toMatchObject({ message: 'Nivel no encontrado.' });
    });

    it('perfil inexistente: 404 de perfil (no se crea)', async () => {
      perfil.findUnique.mockResolvedValue(null);
      const err = await service.assertLevelUnlocked(USER, 'L1').catch((e) => e);
      expect(err).toBeInstanceOf(NotFoundException);
      expect(err.getResponse()).toMatchObject({ message: 'Perfil de jugador no encontrado.' });
    });

    it('reutiliza las reglas existentes: XP y señas obligatorias deciden el acceso', async () => {
      perfil.findUnique.mockResolvedValue({ totalXp: 100, nivelActual: 1 });
      progresoSena.findMany.mockResolvedValue([{ senaId: 's1', completada: true, mejorPrecision: dec('60.00') }]);
      await expect(service.assertLevelUnlocked(USER, 'L2')).resolves.toBeUndefined();
      progresoSena.findMany.mockResolvedValue([{ senaId: 's1', completada: true, mejorPrecision: dec('59.99') }]);
      await expect(service.assertLevelUnlocked(USER, 'L2')).rejects.toBeInstanceOf(LevelLockedException);
    });

    it('unlockedLevelIds devuelve solo los niveles accesibles (ACTUAL o DESBLOQUEADO)', async () => {
      expect([...(await service.unlockedLevelIds(USER))]).toEqual(['L1']);
      perfil.findUnique.mockResolvedValue({ totalXp: 1000, nivelActual: 1 });
      progresoSena.findMany.mockResolvedValue([{ senaId: 's1', completada: true, mejorPrecision: dec('90.00') }]);
      expect([...(await service.unlockedLevelIds(USER))].sort()).toEqual(['L1', 'L2', 'L3']);
    });

    it('no escribe nada: solo se usan lecturas (el doble de Prisma no tiene métodos de escritura)', async () => {
      await service.assertLevelUnlocked(USER, 'L1');
      await service.unlockedLevelIds(USER);
      expect(Object.keys(perfil)).toEqual(['findUnique']);
      expect(Object.keys(nivelSena)).toEqual(['findMany']);
      expect(Object.keys(progresoSena)).toEqual(['findMany']);
    });
  });

  describe('catálogo con numeración inconsistente', () => {
    it('no falla, calcula por `orden` y avisa UNA sola vez por inconsistencia', async () => {
      const writes: string[] = [];
      jest.spyOn(process.stdout, 'write').mockImplementation(((c: unknown) => (writes.push(String(c)), true)) as never);
      jest.spyOn(process.stderr, 'write').mockImplementation(((c: unknown) => (writes.push(String(c)), true)) as never);
      try {
        levelsService.findAll.mockResolvedValue([lvl('X', 5, 10, 0, 60), lvl('Y', 2, 20, 0, 60)]); // numero baja al subir orden
        const first = await service.listForPlayer(USER);
        await service.listForPlayer(USER);
        expect(first.map((l) => l.id)).toEqual(['X', 'Y']);
        const warns = writes.join('').split('Catálogo inconsistente').length - 1;
        expect(warns).toBe(1);
      } finally {
        jest.restoreAllMocks();
      }
    });
  });
});
