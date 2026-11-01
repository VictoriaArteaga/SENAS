import { randomUUID } from 'node:crypto';
import { PrismaService } from '../prisma/prisma.service';
import { UsersService } from './users.service';

describe('UsersService: findActiveById / findMeById', () => {
  let usuario: { findUnique: jest.Mock };
  let service: UsersService;
  const id = randomUUID();

  beforeEach(() => {
    usuario = { findUnique: jest.fn() };
    service = new UsersService({ usuario } as unknown as PrismaService);
  });

  describe('findActiveById', () => {
    it('devuelve la cuenta activa', async () => {
      usuario.findUnique.mockResolvedValue({ id, email: 'a@b.co', isActive: true });
      await expect(service.findActiveById(id)).resolves.toEqual({ id, email: 'a@b.co' });
    });
    it('devuelve null si la cuenta está inactiva o no existe', async () => {
      usuario.findUnique.mockResolvedValue({ id, email: 'a@b.co', isActive: false });
      await expect(service.findActiveById(id)).resolves.toBeNull();
      usuario.findUnique.mockResolvedValue(null);
      await expect(service.findActiveById(id)).resolves.toBeNull();
    });
    it.each([undefined, null, '', 'no-uuid', "1' OR '1'='1", 123, `${id} `])(
      'un sub inválido (%j) devuelve null SIN consultar la base de datos',
      async (bad) => {
        await expect(service.findActiveById(bad as never)).resolves.toBeNull();
        expect(usuario.findUnique).not.toHaveBeenCalled();
      },
    );
    it('no pide hashes de contraseña', async () => {
      usuario.findUnique.mockResolvedValue(null);
      await service.findActiveById(id);
      expect(JSON.stringify(usuario.findUnique.mock.calls[0][0])).not.toMatch(/passwordHash/);
    });
  });

  describe('findMeById', () => {
    const row = {
      id, username: 'maria', email: 'maria@example.com', isActive: true, createdAt: new Date('2026-10-31T00:00:00Z'),
      perfil: { displayName: 'María', totalXp: 120, nivelActual: 2, precisionPromedio: { toString: () => '87.50', valueOf: () => 87.5 }, avatarConfig: { hair: 'h1' } },
    };

    it('devuelve solo datos públicos, con la precisión como número', async () => {
      usuario.findUnique.mockResolvedValue(row);
      const me = await service.findMeById(id);
      expect(me).toEqual({
        id, username: 'maria', email: 'maria@example.com', displayName: 'María', createdAt: '2026-10-31T00:00:00.000Z',
        profile: { totalXp: 120, nivelActual: 2, precisionPromedio: 87.5, avatarConfig: { hair: 'h1' } },
      });
    });

    it('la consulta no selecciona passwordHash ni sesiones', async () => {
      usuario.findUnique.mockResolvedValue(row);
      await service.findMeById(id);
      const query = JSON.stringify(usuario.findUnique.mock.calls[0][0]);
      expect(query).not.toMatch(/passwordHash|sesiones|tokenHash/);
    });

    it('devuelve null si no existe, está inactiva, no tiene perfil o el id es inválido', async () => {
      usuario.findUnique.mockResolvedValue(null);
      await expect(service.findMeById(id)).resolves.toBeNull();
      usuario.findUnique.mockResolvedValue({ ...row, isActive: false });
      await expect(service.findMeById(id)).resolves.toBeNull();
      usuario.findUnique.mockResolvedValue({ ...row, perfil: null });
      await expect(service.findMeById(id)).resolves.toBeNull();
      usuario.findUnique.mockClear();
      await expect(service.findMeById('basura')).resolves.toBeNull();
      expect(usuario.findUnique).not.toHaveBeenCalled();
    });
  });
});
