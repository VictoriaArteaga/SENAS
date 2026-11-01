import { Injectable, UnauthorizedException } from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import { PassportStrategy } from '@nestjs/passport';
import { ExtractJwt, Strategy } from 'passport-jwt';
import { UsersService } from '../../users/users.service';
import { AuthenticatedUser, JwtPayload } from '../auth.types';

/**
 * Autenticación de cada petición protegida, en dos pasos con responsabilidades distintas:
 *  1. passport-jwt valida el TOKEN: firma HS256 y expiración (stateless).
 *  2. validate() valida la CUENTA: debe existir y estar activa (una consulta por petición).
 *
 * No consulta `sesiones`: revocar una sesión (logout) impide renovar, pero no invalida un access
 * token ya emitido, que seguirá siendo válido hasta su expiración (15 min por defecto). Lo único con
 * efecto inmediato sobre los access tokens es desactivar la cuenta (is_active = false).
 */
@Injectable()
export class JwtStrategy extends PassportStrategy(Strategy, 'jwt') {
  constructor(
    config: ConfigService,
    private readonly users: UsersService,
  ) {
    super({
      jwtFromRequest: ExtractJwt.fromAuthHeaderAsBearerToken(),
      ignoreExpiration: false,
      secretOrKey: config.getOrThrow<string>('JWT_ACCESS_SECRET'),
      algorithms: ['HS256'],
    });
  }

  async validate(payload: JwtPayload): Promise<AuthenticatedUser> {
    const user = await this.users.findActiveById(payload?.sub);
    if (!user) throw new UnauthorizedException();
    // El correo se toma de la base de datos (puede haber cambiado desde que se emitió el token).
    return { userId: user.id, email: user.email };
  }
}
