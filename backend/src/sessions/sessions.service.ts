import { Injectable } from '@nestjs/common';
import { PrismaService } from '../prisma/prisma.service';

export interface CreateSessionInput {
  /** UUID generado por la aplicación: forma parte del refresh token (`<id>.<secreto>`). */
  id: string;
  usuarioId: string;
  /** SHA-256 del refresh token. Nunca el token en claro. */
  tokenHash: string;
  fechaExpiracion: Date;
  informacionDispositivo?: Record<string, string>;
}

export interface SessionForRefresh {
  id: string;
  usuarioId: string;
  tokenHash: string;
  fechaExpiracion: Date;
  fechaRevocacion: Date | null;
  usuario: { email: string; isActive: boolean };
}

/**
 * Acceso a `sesiones`. El rol senas_app solo tiene SELECT, INSERT y UPDATE: revocar es poner
 * `fecha_revocacion`, nunca borrar. Todas las escrituras son UPDATE condicionales atómicos
 * (una sola sentencia), de modo que dos solicitudes concurrentes no pueden ambas "ganar".
 */
@Injectable()
export class SessionsService {
  constructor(private readonly prisma: PrismaService) {}

  /** Crea la sesión de un dispositivo. No invalida las demás sesiones del usuario. */
  create(input: CreateSessionInput): Promise<{ id: string; fechaExpiracion: Date }> {
    return this.prisma.sesion.create({
      data: {
        id: input.id,
        usuarioId: input.usuarioId,
        tokenHash: input.tokenHash,
        fechaExpiracion: input.fechaExpiracion,
        informacionDispositivo: input.informacionDispositivo,
      },
      select: { id: true, fechaExpiracion: true },
    });
  }

  /** Sesión (con el estado de su usuario) para decidir una renovación. `id` ya debe ser un UUID válido. */
  findForRefresh(id: string): Promise<SessionForRefresh | null> {
    return this.prisma.sesion.findUnique({
      where: { id },
      select: {
        id: true,
        usuarioId: true,
        tokenHash: true,
        fechaExpiracion: true,
        fechaRevocacion: true,
        usuario: { select: { email: true, isActive: true } },
      },
    });
  }

  /**
   * Rotación atómica: cambia el hash solo si la fila sigue con el hash anterior, sin revocar y sin
   * expirar. Devuelve false si alguien se adelantó (otra renovación, revocación o expiración), por
   * lo que nunca puede haber dos rotaciones exitosas del mismo token. NO modifica fecha_expiracion:
   * la vigencia de la sesión es absoluta.
   */
  async rotate(input: { id: string; currentHash: string; newHash: string; now: Date }): Promise<boolean> {
    const { count } = await this.prisma.sesion.updateMany({
      where: {
        id: input.id,
        tokenHash: input.currentHash,
        fechaRevocacion: null,
        fechaExpiracion: { gt: input.now },
      },
      data: { tokenHash: input.newHash, ultimoUso: input.now },
    });
    return count === 1;
  }

  /** Revoca una sesión (idempotente). Devuelve true si esta llamada la revocó. */
  async revoke(id: string, now: Date = new Date()): Promise<boolean> {
    const { count } = await this.prisma.sesion.updateMany({
      where: { id, fechaRevocacion: null },
      data: { fechaRevocacion: now },
    });
    return count === 1;
  }

  /** Revoca la sesión solo si el hash coincide con el del token presentado (logout). */
  async revokeByToken(id: string, tokenHash: string, now: Date = new Date()): Promise<boolean> {
    const { count } = await this.prisma.sesion.updateMany({
      where: { id, tokenHash, fechaRevocacion: null },
      data: { fechaRevocacion: now },
    });
    return count === 1;
  }

  /** Revoca todas las sesiones activas de un usuario. Devuelve cuántas revocó. */
  async revokeAllForUser(usuarioId: string, now: Date = new Date()): Promise<number> {
    const { count } = await this.prisma.sesion.updateMany({
      where: { usuarioId, fechaRevocacion: null },
      data: { fechaRevocacion: now },
    });
    return count;
  }
}
