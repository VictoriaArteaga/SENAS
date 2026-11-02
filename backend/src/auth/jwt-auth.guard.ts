import { ExecutionContext, HttpStatus, Injectable } from '@nestjs/common';
import { Reflector } from '@nestjs/core';
import { AuthGuard } from '@nestjs/passport';
import { ApiError, ErrorCode } from '../common/api-error';
import { IS_PUBLIC } from '../common/decorators/public.decorator';

/** Global guard: every route requires a JWT unless marked with @Public(). */
@Injectable()
export class JwtAuthGuard extends AuthGuard('jwt') {
  constructor(private readonly reflector: Reflector) {
    super();
  }

  canActivate(context: ExecutionContext) {
    const isPublic = this.reflector.getAllAndOverride<boolean>(IS_PUBLIC, [
      context.getHandler(),
      context.getClass(),
    ]);
    if (isPublic) return true;
    return super.canActivate(context);
  }

  handleRequest<T>(err: unknown, user: T | false): T {
    if (err instanceof ApiError) throw err;
    if (err || !user) {
      throw new ApiError(
        HttpStatus.UNAUTHORIZED,
        ErrorCode.UNAUTHORIZED,
        'A valid token is required. Please sign in.',
      );
    }
    return user;
  }
}
