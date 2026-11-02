import { HttpStatus, Inject, Injectable } from '@nestjs/common';
import { PassportStrategy } from '@nestjs/passport';
import { ExtractJwt, Strategy } from 'passport-jwt';
import { ApiError, ErrorCode } from '../common/api-error';
import { AuthenticatedUser } from '../common/decorators/current-user.decorator';
import { APP_CONFIG, type AppConfig } from '../config/configuration';
import { Role } from '../users/entities/user.entity';
import { AuthService } from './auth.service';

export interface JwtPayload {
  sub: string;
  sid: string;
  role: Role;
}

@Injectable()
export class JwtStrategy extends PassportStrategy(Strategy) {
  constructor(
    @Inject(APP_CONFIG) config: AppConfig,
    private readonly auth: AuthService,
  ) {
    super({
      jwtFromRequest: ExtractJwt.fromAuthHeaderAsBearerToken(),
      secretOrKey: config.auth.accessSecret,
      algorithms: ['HS256'],
      ignoreExpiration: false,
    });
  }

  /** Besides signature and expiry, the session must be neither revoked nor expired. */
  async validate(payload: JwtPayload): Promise<AuthenticatedUser> {
    if (!payload?.sub || !payload?.sid) throw this.invalid();
    const active = await this.auth.isSessionActive(payload.sid, payload.sub);
    if (!active) throw this.invalid();
    return { userId: payload.sub, sessionId: payload.sid, role: payload.role };
  }

  private invalid(): ApiError {
    return new ApiError(
      HttpStatus.UNAUTHORIZED,
      ErrorCode.INVALID_SESSION,
      'The session is not valid or was closed.',
    );
  }
}
