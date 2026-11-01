import { ConflictException } from '@nestjs/common';
import { Prisma } from '../generated/prisma/client.js';
import { PrismaService } from '../prisma/prisma.service';
import { UsersService, uniqueViolationFields } from './users.service';

const p2002 = (meta: Record<string, unknown>) =>
  new Prisma.PrismaClientKnownRequestError('Unique constraint failed', { code: 'P2002', clientVersion: '7.10.1', meta });

describe('UsersService', () => {
  const input = { username: 'maria', email: 'maria@example.com', passwordHash: '$argon2id$x', displayName: 'María' };
  let tx: { usuario: { create: jest.Mock }; perfil: { create: jest.Mock } };
  let prisma: { $transaction: jest.Mock; usuario: { findUnique: jest.Mock } };
  let service: UsersService;

  beforeEach(() => {
    tx = {
      usuario: { create: jest.fn().mockResolvedValue({ id: 'u1', username: 'maria', email: 'maria@example.com' }) },
      perfil: { create: jest.fn().mockResolvedValue({ displayName: 'María' }) },
    };
    prisma = { $transaction: jest.fn((cb) => cb(tx)), usuario: { findUnique: jest.fn() } };
    service = new UsersService(prisma as unknown as PrismaService);
  });

  it('crea usuario y perfil dentro de una única transacción', async () => {
    const res = await service.createWithProfile(input);
    expect(prisma.$transaction).toHaveBeenCalledTimes(1);
    expect(tx.usuario.create).toHaveBeenCalledWith(expect.objectContaining({ data: { username: 'maria', email: 'maria@example.com', passwordHash: '$argon2id$x' } }));
    // El perfil solo recibe usuarioId y displayName: el resto son los valores por defecto del esquema.
    expect(tx.perfil.create).toHaveBeenCalledWith(expect.objectContaining({ data: { usuarioId: 'u1', displayName: 'María' } }));
    expect(res).toEqual({ id: 'u1', username: 'maria', email: 'maria@example.com', displayName: 'María' });
  });

  it('la respuesta nunca contiene el hash de la contraseña', async () => {
    const res = await service.createWithProfile(input);
    expect(JSON.stringify(res)).not.toMatch(/argon2|passwordHash/);
    // y el SELECT del create no lo pide de vuelta
    expect(tx.usuario.create.mock.calls[0][0].select).toEqual({ id: true, username: true, email: true });
  });

  it('si falla la creación del perfil el error se propaga (la transacción revierte el usuario)', async () => {
    tx.perfil.create.mockRejectedValue(new Error('falla perfil'));
    await expect(service.createWithProfile(input)).rejects.toThrow('falla perfil');
  });

  describe('duplicados (P2002)', () => {
    it.each([
      ['target como arreglo', { target: ['username'] }],
      ['target como nombre de índice', { target: 'usuarios_username_key' }],
      ['driver adapter: campos', { driverAdapterError: { cause: { kind: 'UniqueConstraintViolation', constraint: { fields: ['username'] } } } }],
      ['driver adapter: índice', { driverAdapterError: { cause: { constraint: { index: 'usuarios_username_key' } } } }],
    ])('username repetido (%s) -> 409 con mensaje propio', async (_n, meta) => {
      tx.usuario.create.mockRejectedValue(p2002(meta));
      await expect(service.createWithProfile(input)).rejects.toThrow(new ConflictException('El nombre de usuario ya está en uso.'));
    });

    it('email repetido -> 409 con mensaje que no confirma que la cuenta exista', async () => {
      tx.usuario.create.mockRejectedValue(p2002({ target: ['email'] }));
      const err = await service.createWithProfile(input).catch((e) => e);
      expect(err).toBeInstanceOf(ConflictException);
      expect(err.message).toMatch(/No se pudo completar el registro/);
      expect(err.message).not.toMatch(/correo|email|existe|registrado/i);
    });

    it('el mensaje no filtra detalles internos de la base de datos', async () => {
      tx.usuario.create.mockRejectedValue(p2002({ target: ['email'], modelName: 'Usuario' }));
      const err = await service.createWithProfile(input).catch((e) => e);
      expect(JSON.stringify(err.getResponse())).not.toMatch(/Prisma|P2002|usuarios_|constraint|Usuario/);
    });
  });

  it('otros errores de base de datos no se convierten en 409', async () => {
    tx.usuario.create.mockRejectedValue(new Error('conexión perdida'));
    await expect(service.createWithProfile(input)).rejects.toThrow('conexión perdida');
  });

  it('findByEmail normaliza el correo', async () => {
    prisma.usuario.findUnique.mockResolvedValue(null);
    await service.findByEmail('  MARIA@Example.com ');
    expect(prisma.usuario.findUnique).toHaveBeenCalledWith(expect.objectContaining({ where: { email: 'maria@example.com' } }));
  });
});

describe('uniqueViolationFields', () => {
  it('devuelve null para errores que no son P2002', () => {
    expect(uniqueViolationFields(new Error('x'))).toBeNull();
    expect(uniqueViolationFields(new Prisma.PrismaClientKnownRequestError('x', { code: 'P2025', clientVersion: '7' }))).toBeNull();
  });
});
