import { UnauthorizedException } from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import { UsersService } from '../../users/users.service';
import { JwtStrategy } from './jwt.strategy';

describe('JwtStrategy.validate (estado de la cuenta)', () => {
  const config = { getOrThrow: () => 'q8Zr2LxV7mT4pN1cB6yH9wKd3sFgJa5eUo0iXvRb' } as unknown as ConfigService;
  let users: { findActiveById: jest.Mock };
  let strategy: JwtStrategy;

  beforeEach(() => {
    users = { findActiveById: jest.fn() };
    strategy = new JwtStrategy(config, users as unknown as UsersService);
  });

  it('cuenta activa: devuelve { userId, email } tomando el correo de la base de datos', async () => {
    users.findActiveById.mockResolvedValue({ id: 'u1', email: 'actual@example.com' });
    await expect(strategy.validate({ sub: 'u1', email: 'viejo@example.com' })).resolves.toEqual({ userId: 'u1', email: 'actual@example.com' });
    expect(users.findActiveById).toHaveBeenCalledTimes(1); // una sola consulta
  });

  it('cuenta inexistente o inactiva: 401', async () => {
    users.findActiveById.mockResolvedValue(null);
    await expect(strategy.validate({ sub: 'u1', email: 'x' })).rejects.toBeInstanceOf(UnauthorizedException);
  });

  it('payload sin sub: 401', async () => {
    users.findActiveById.mockResolvedValue(null);
    await expect(strategy.validate({} as never)).rejects.toBeInstanceOf(UnauthorizedException);
    await expect(strategy.validate(undefined as never)).rejects.toBeInstanceOf(UnauthorizedException);
  });
});
