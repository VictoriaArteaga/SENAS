import { ConflictException, Injectable } from '@nestjs/common';
import { Prisma } from '../generated/prisma/client.js';
import { isUuid } from '../auth/token.util';
import { normalizeEmail } from '../common/normalize';
import { PrismaService } from '../prisma/prisma.service';

/** Datos de un usuario que pueden salir por la API. Nunca incluye hashes. */
export interface PublicUser {
  id: string;
  username: string;
  email: string;
  displayName: string;
}

/** Perfil público de la cuenta (GET /auth/me). */
export interface MeProfile {
  id: string;
  username: string;
  email: string;
  displayName: string;
  createdAt: string;
  profile: {
    totalXp: number;
    nivelActual: number;
    precisionPromedio: number;
    avatarConfig: unknown;
  };
}

export interface CreateUserInput {
  username: string;
  email: string;
  passwordHash: string;
  displayName: string;
}

/**
 * Campos de la violación de unicidad (P2002) o null si el error es de otro tipo.
 * Prisma 7 con driver adapter puede informar el índice en `meta.target` o dentro de
 * `meta.driverAdapterError`; se revisan ambas formas y el nombre de la restricción.
 */
export function uniqueViolationFields(error: unknown): string[] | null {
  if (!(error instanceof Prisma.PrismaClientKnownRequestError) || error.code !== 'P2002') return null;
  const meta = (error.meta ?? {}) as Record<string, any>;
  const raw: unknown[] = [
    meta['target'],
    meta['driverAdapterError']?.cause?.constraint?.fields,
    meta['driverAdapterError']?.cause?.constraint?.index,
    meta['driverAdapterError']?.cause?.originalMessage,
  ];
  return raw.flat().filter((v): v is string => typeof v === 'string');
}

@Injectable()
export class UsersService {
  constructor(private readonly prisma: PrismaService) {}

  /** Usuario con su hash, solo para autenticación interna (nunca se devuelve tal cual). */
  findByEmail(email: string) {
    return this.prisma.usuario.findUnique({
      where: { email: normalizeEmail(email) },
      include: { perfil: { select: { displayName: true } } },
    });
  }

  /**
   * Usuario activo por id (identidad del JWT). Devuelve null si el id no es un UUID válido, no existe
   * o la cuenta está inactiva: así una cuenta desactivada pierde acceso aunque su access token siga vigente.
   */
  async findActiveById(id: string): Promise<{ id: string; email: string } | null> {
    if (!isUuid(id)) return null;
    const usuario = await this.prisma.usuario.findUnique({
      where: { id },
      select: { id: true, email: true, isActive: true },
    });
    return usuario?.isActive ? { id: usuario.id, email: usuario.email } : null;
  }

  /** Datos públicos de la cuenta y su perfil (sin hashes ni datos de sesiones). */
  async findMeById(id: string): Promise<MeProfile | null> {
    if (!isUuid(id)) return null;
    const usuario = await this.prisma.usuario.findUnique({
      where: { id },
      select: {
        id: true,
        username: true,
        email: true,
        createdAt: true,
        isActive: true,
        perfil: { select: { displayName: true, totalXp: true, nivelActual: true, precisionPromedio: true, avatarConfig: true } },
      },
    });
    if (!usuario || !usuario.isActive || !usuario.perfil) return null;
    return {
      id: usuario.id,
      username: usuario.username,
      email: usuario.email,
      displayName: usuario.perfil.displayName,
      createdAt: usuario.createdAt.toISOString(),
      profile: {
        totalXp: usuario.perfil.totalXp,
        nivelActual: usuario.perfil.nivelActual,
        precisionPromedio: Number(usuario.perfil.precisionPromedio),
        avatarConfig: usuario.perfil.avatarConfig,
      },
    };
  }

  /** Crea Usuario y Perfil en una sola transacción: o se crean los dos o ninguno. */
  async createWithProfile(input: CreateUserInput): Promise<PublicUser> {
    try {
      return await this.prisma.$transaction(async (tx) => {
        const usuario = await tx.usuario.create({
          data: { username: input.username, email: input.email, passwordHash: input.passwordHash },
          select: { id: true, username: true, email: true },
        });
        const perfil = await tx.perfil.create({
          data: { usuarioId: usuario.id, displayName: input.displayName },
          select: { displayName: true },
        });
        return { ...usuario, displayName: perfil.displayName };
      });
    } catch (error) {
      const fields = uniqueViolationFields(error);
      if (fields === null) throw error;
      if (fields.some((f) => f.includes('username'))) {
        throw new ConflictException('El nombre de usuario ya está en uso.');
      }
      // Correo repetido (o índice no identificable): mensaje que no confirma si una cuenta existe.
      throw new ConflictException('No se pudo completar el registro. Si ya tienes una cuenta, inicia sesión.');
    }
  }
}
