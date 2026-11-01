import { createParamDecorator, ExecutionContext } from '@nestjs/common';
import { AuthenticatedUser } from '../auth.types';

/**
 * Usuario autenticado por JwtAuthGuard (identidad tomada del JWT verificado y comprobada contra la
 * base de datos). Es la única fuente de identidad: nunca se usa un id enviado por el cliente.
 */
export const CurrentUser = createParamDecorator((_data: unknown, ctx: ExecutionContext): AuthenticatedUser => {
  return ctx.switchToHttp().getRequest<{ user: AuthenticatedUser }>().user;
});
