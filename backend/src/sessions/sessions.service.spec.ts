import { PrismaService } from '../prisma/prisma.service';
import { SessionsService } from './sessions.service';

describe('SessionsService (consultas atómicas y permisos de senas_app)', () => {
  let sesion: Record<string, jest.Mock>;
  let service: SessionsService;
  const now = new Date('2026-10-31T12:00:00.000Z');

  beforeEach(() => {
    sesion = {
      create: jest.fn().mockResolvedValue({ id: 's', fechaExpiracion: now }),
      findUnique: jest.fn(),
      updateMany: jest.fn().mockResolvedValue({ count: 1 }),
    };
    service = new SessionsService({ sesion } as unknown as PrismaService);
  });

  it('create usa el id recibido (parte del refresh token) y solo el hash', async () => {
    await service.create({ id: 'id-1', usuarioId: 'u1', tokenHash: 'h'.repeat(64), fechaExpiracion: now });
    expect(sesion.create).toHaveBeenCalledWith({
      data: { id: 'id-1', usuarioId: 'u1', tokenHash: 'h'.repeat(64), fechaExpiracion: now, informacionDispositivo: undefined },
      select: { id: true, fechaExpiracion: true },
    });
  });

  describe('rotate', () => {
    it('es UN updateMany condicionado a id, hash anterior, no revocada y no expirada', async () => {
      await service.rotate({ id: 'id-1', currentHash: 'old', newHash: 'new', now });
      expect(sesion.updateMany).toHaveBeenCalledTimes(1);
      expect(sesion.updateMany).toHaveBeenCalledWith({
        where: { id: 'id-1', tokenHash: 'old', fechaRevocacion: null, fechaExpiracion: { gt: now } },
        data: { tokenHash: 'new', ultimoUso: now },
      });
    });

    it('no modifica fechaExpiracion (vigencia absoluta) ni toca fechaRevocacion', async () => {
      await service.rotate({ id: 'id-1', currentHash: 'old', newHash: 'new', now });
      const { data } = sesion.updateMany.mock.calls[0][0];
      expect(Object.keys(data).sort()).toEqual(['tokenHash', 'ultimoUso']);
    });

    it('devuelve true solo si se actualizó exactamente una fila', async () => {
      await expect(service.rotate({ id: 'a', currentHash: 'o', newHash: 'n', now })).resolves.toBe(true);
      sesion.updateMany.mockResolvedValue({ count: 0 });
      await expect(service.rotate({ id: 'a', currentHash: 'o', newHash: 'n', now })).resolves.toBe(false);
    });
  });

  it('revoke solo actualiza sesiones aún no revocadas', async () => {
    await service.revoke('id-1', now);
    expect(sesion.updateMany).toHaveBeenCalledWith({ where: { id: 'id-1', fechaRevocacion: null }, data: { fechaRevocacion: now } });
  });

  it('revokeByToken exige id Y hash coincidentes', async () => {
    await service.revokeByToken('id-1', 'hash', now);
    expect(sesion.updateMany).toHaveBeenCalledWith({ where: { id: 'id-1', tokenHash: 'hash', fechaRevocacion: null }, data: { fechaRevocacion: now } });
  });

  it('revokeAllForUser solo filtra por el usuario recibido y solo sesiones activas', async () => {
    sesion.updateMany.mockResolvedValue({ count: 4 });
    await expect(service.revokeAllForUser('u1', now)).resolves.toBe(4);
    expect(sesion.updateMany).toHaveBeenCalledWith({ where: { usuarioId: 'u1', fechaRevocacion: null }, data: { fechaRevocacion: now } });
  });

  it('nunca borra: ni delete ni deleteMany (senas_app no tiene DELETE)', async () => {
    const methods = Object.getOwnPropertyNames(SessionsService.prototype);
    expect(methods.filter((m) => /delete|remove|destroy/i.test(m))).toEqual([]);
    await service.revoke('a', now);
    await service.revokeByToken('a', 'h', now);
    await service.revokeAllForUser('u', now);
    expect(Object.keys(sesion)).not.toContain('delete');
    expect(Object.keys(sesion)).not.toContain('deleteMany');
  });
});
