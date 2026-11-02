import { createParamDecorator, ExecutionContext } from '@nestjs/common';
import type { Request } from 'express';
import { Role } from '../../users/entities/user.entity';

/** Data from the already verified JWT, available in `req.user`. */
export interface AuthenticatedUser {
  userId: string;
  sessionId: string;
  role: Role;
}

export const CurrentUser = createParamDecorator(
  (_: unknown, ctx: ExecutionContext): AuthenticatedUser =>
    ctx.switchToHttp().getRequest<Request & { user: AuthenticatedUser }>().user,
);
