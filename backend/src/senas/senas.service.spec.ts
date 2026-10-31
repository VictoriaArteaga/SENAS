import { NotFoundException } from '@nestjs/common';
import { PrismaService } from '../prisma/prisma.service';
import { SenasService, senaSelect, toSenaResponse } from './senas.service';

const sena = (over: Record<string, unknown> = {}) => ({
  id: '018f0000-0000-7000-8000-0000000000a1',
  nombre: 'HOLA',
  descripcion: 'Saludo',
  tipo: 'SENA',
  dificultad: 'FACIL',
  animacion: 'anim_hola',
  patronReferencia: { version: 1, manos: [{ x: 0.123456789, y: -0.5 }], tags: ['a', null, true] },
  ...over,
});
const LEVEL = '018f0000-0000-7000-8000-000000000001';
const PUBLIC_FIELDS = ['animacion', 'descripcion', 'dificultad', 'id', 'nombre', 'patronReferencia', 'tipo'];

describe('SenasService', () => {
  let nivel: { findUnique: jest.Mock };
  let nivelSena: { findMany: jest.Mock };
  let senaModel: { findUnique: jest.Mock };
  let service: SenasService;

  beforeEach(() => {
    // Solo métodos de lectura: cualquier escritura fallaría con un TypeError.
    nivel = { findUnique: jest.fn() };
    nivelSena = { findMany: jest.fn() };
    senaModel = { findUnique: jest.fn() };
    service = new SenasService({ nivel, nivelSena, sena: senaModel } as unknown as PrismaService);
  });

  describe('findByLevel', () => {
    const item = (orden: number, esObligatoria: boolean, over: Record<string, unknown> = {}) => ({ orden, esObligatoria, sena: sena(over) });

    it('hace UNA sola consulta a nivel_senas, ordena por orden y selecciona solo campos públicos', async () => {
      nivelSena.findMany.mockResolvedValue([item(1, true)]);
      await service.findByLevel(LEVEL);

      expect(nivelSena.findMany).toHaveBeenCalledTimes(1);
      expect(nivelSena.findMany).toHaveBeenCalledWith({
        where: { nivelId: LEVEL },
        orderBy: { orden: 'asc' },
        select: { orden: true, esObligatoria: true, sena: { select: senaSelect } },
      });
      expect(nivel.findUnique).not.toHaveBeenCalled(); // con resultados no hace falta comprobar el nivel
      expect(senaModel.findUnique).not.toHaveBeenCalled(); // sin consulta por seña (N+1)
    });

    it('aplana cada elemento (seña + orden + esObligatoria) y conserva el orden recibido', async () => {
      nivelSena.findMany.mockResolvedValue([
        item(1, true, { id: 'a', nombre: 'HOLA' }),
        item(2, false, { id: 'b', nombre: 'GRACIAS', tipo: 'FRASE', dificultad: 'DIFICIL' }),
        item(7, true, { id: 'c', nombre: 'POR FAVOR' }),
      ]);
      const out = await service.findByLevel(LEVEL);
      expect(out.map((s) => [s.id, s.orden, s.esObligatoria])).toEqual([['a', 1, true], ['b', 2, false], ['c', 7, true]]);
      expect(Object.keys(out[0]).sort()).toEqual([...PUBLIC_FIELDS, 'esObligatoria', 'orden'].sort());
      expect(out[1]).toMatchObject({ tipo: 'FRASE', dificultad: 'DIFICIL' });
    });

    it('nivel existente sin señas: lista vacía (no es un error), comprobando que el nivel existe', async () => {
      nivelSena.findMany.mockResolvedValue([]);
      nivel.findUnique.mockResolvedValue({ id: LEVEL });
      await expect(service.findByLevel(LEVEL)).resolves.toEqual([]);
      expect(nivel.findUnique).toHaveBeenCalledWith({ where: { id: LEVEL }, select: { id: true } });
    });

    it('nivel inexistente: 404', async () => {
      nivelSena.findMany.mockResolvedValue([]);
      nivel.findUnique.mockResolvedValue(null);
      const err = await service.findByLevel(LEVEL).catch((e) => e);
      expect(err).toBeInstanceOf(NotFoundException);
      expect(err.getResponse()).toMatchObject({ statusCode: 404, message: 'Nivel no encontrado.' });
    });

    it('patronReferencia se entrega sin transformar (misma estructura y valores)', async () => {
      const patron = { version: 1, manos: [{ x: 0.123456789, y: -0.5 }], tags: ['a', null, true], anidado: { z: [1, 2, { k: 'ñ' }] } };
      nivelSena.findMany.mockResolvedValue([item(1, true, { patronReferencia: patron })]);
      const [out] = await service.findByLevel(LEVEL);
      expect(out.patronReferencia).toBe(patron); // misma referencia: ni copia ni conversión
    });
  });

  describe('findOne', () => {
    it('devuelve solo los campos públicos de la seña', async () => {
      senaModel.findUnique.mockResolvedValue({ ...sena(), createdAt: new Date(), updatedAt: new Date(), niveles: [], progresoSenas: [] });
      const out = await service.findOne('018f0000-0000-7000-8000-0000000000a1');
      expect(Object.keys(out).sort()).toEqual(PUBLIC_FIELDS);
      expect(senaModel.findUnique).toHaveBeenCalledWith({ where: { id: '018f0000-0000-7000-8000-0000000000a1' }, select: senaSelect });
    });

    it('seña inexistente: 404 en español', async () => {
      senaModel.findUnique.mockResolvedValue(null);
      const err = await service.findOne('018f0000-0000-7000-8000-0000000000ff').catch((e) => e);
      expect(err).toBeInstanceOf(NotFoundException);
      expect(err.getResponse()).toMatchObject({ statusCode: 404, message: 'Seña no encontrada.' });
    });

    it('campos opcionales nulos se conservan como null', () => {
      const out = toSenaResponse(sena({ descripcion: null, animacion: null, patronReferencia: null }) as never);
      expect(out).toMatchObject({ descripcion: null, animacion: null, patronReferencia: null });
    });
  });
});
