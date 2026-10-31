import { ForbiddenException } from '@nestjs/common';

/** Código estable del error 403 de contenido bloqueado. Los clientes deben basarse en él, no en el texto. */
export const LEVEL_LOCKED_CODE = 'LEVEL_LOCKED';

/**
 * 403 cuando el jugador intenta acceder a contenido de un nivel que todavía no tiene desbloqueado.
 * Cuerpo: { statusCode: 403, code: 'LEVEL_LOCKED', message, error: 'Forbidden' }. No incluye motivos ni datos
 * internos: el cliente puede consultarlos en GET /levels (`motivosBloqueo`).
 */
export class LevelLockedException extends ForbiddenException {
  constructor(message = 'El nivel todavía no está desbloqueado.') {
    super({ statusCode: 403, code: LEVEL_LOCKED_CODE, message, error: 'Forbidden' });
  }
}
