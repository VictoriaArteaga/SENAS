import { NotFoundException } from '@nestjs/common';
import { Prisma } from '../generated/prisma/client.js';
import { PrismaService } from '../prisma/prisma.service';
import { levelSelect, LevelsService, toLevelResponse } from './levels.service';

const row = (over: Partial<Record<string, unknown>> = {}) => ({
  id: '018f0000-0000-7000-8000-000000000001',
  numero: 1,
  nombre: 'Primeros pasos',
  descripcion: 'Saludos básicos',
  orden: 1,
  xpRequerida: 0,
  precisionMinima: new Prisma.Decimal('72.50'),
  ...over,
});

describe('LevelsService', () => {
  // Solo existen los métodos de lectura: cualquier intento de escribir fallaría con un TypeError.
  let nivel: { findMany: jest.Mock; findUnique: jest.Mock };
  let service: LevelsService;

  beforeEach(() => {
    nivel = { findMany: jest.fn(), findUnique: jest.fn() };
    service = new LevelsService({ nivel } as unknown as PrismaService);
  });

  describe('findAll', () => {
    it('ordena por `orden` ascendente y selecciona solo los campos públicos', async () => {
      nivel.findMany.mockResolvedValue([row()]);
      await service.findAll();
      expect(nivel.findMany).toHaveBeenCalledTimes(1); // una sola consulta
      expect(nivel.findMany).toHaveBeenCalledWith({ select: levelSelect, orderBy: { orden: 'asc' } });
      expect(Object.keys(levelSelect).sort()).toEqual(['descripcion', 'id', 'nombre', 'numero', 'orden', 'precisionMinima', 'xpRequerida']);
    });

    it('conserva el orden que devuelve la base de datos', async () => {
      nivel.findMany.mockResolvedValue([row({ id: 'b', orden: 2 }), row({ id: 'a', orden: 5 }), row({ id: 'c', orden: 9 })]);
      expect((await service.findAll()).map((n) => n.id)).toEqual(['b', 'a', 'c']);
    });

    it('lista vacía si no hay niveles', async () => {
      nivel.findMany.mockResolvedValue([]);
      await expect(service.findAll()).resolves.toEqual([]);
    });
  });

  describe('findOne', () => {
    it('devuelve el nivel con la precisión como número', async () => {
      nivel.findUnique.mockResolvedValue(row());
      await expect(service.findOne('018f0000-0000-7000-8000-000000000001')).resolves.toEqual({
        id: '018f0000-0000-7000-8000-000000000001',
        numero: 1,
        nombre: 'Primeros pasos',
        descripcion: 'Saludos básicos',
        orden: 1,
        xpRequerida: 0,
        precisionMinima: 72.5,
      });
      expect(nivel.findUnique).toHaveBeenCalledWith({ where: { id: '018f0000-0000-7000-8000-000000000001' }, select: levelSelect });
    });

    it('nivel inexistente: 404 en español, sin detalles internos', async () => {
      nivel.findUnique.mockResolvedValue(null);
      const err = await service.findOne('018f0000-0000-7000-8000-000000000099').catch((e) => e);
      expect(err).toBeInstanceOf(NotFoundException);
      expect(err.getResponse()).toMatchObject({ statusCode: 404, message: 'Nivel no encontrado.' });
    });
  });

  describe('toLevelResponse', () => {
    it('devuelve exactamente los campos públicos (sin fechas ni relaciones) aunque la fila traiga más', () => {
      const out = toLevelResponse({ ...row(), createdAt: new Date(), updatedAt: new Date(), senas: [], evaluaciones: [] } as never);
      expect(Object.keys(out).sort()).toEqual(['descripcion', 'id', 'nombre', 'numero', 'orden', 'precisionMinima', 'xpRequerida']);
    });
    it('descripcion nula se conserva como null', () => {
      expect(toLevelResponse(row({ descripcion: null }) as never).descripcion).toBeNull();
    });
    it('convierte el decimal sin perder precisión de 2 decimales', () => {
      for (const [dec, num] of [['0.00', 0], ['60.00', 60], ['99.99', 99.99], ['100.00', 100]] as const) {
        expect(toLevelResponse(row({ precisionMinima: new Prisma.Decimal(dec) }) as never).precisionMinima).toBe(num);
      }
    });
  });
});
