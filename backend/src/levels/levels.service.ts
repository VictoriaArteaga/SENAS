import { Injectable, NotFoundException } from '@nestjs/common';
import { Prisma } from '../generated/prisma/client.js';
import { PrismaService } from '../prisma/prisma.service';

/** Solo los campos públicos del nivel: sin createdAt/updatedAt ni relaciones. */
export const levelSelect = {
  id: true,
  numero: true,
  nombre: true,
  descripcion: true,
  orden: true,
  xpRequerida: true,
  precisionMinima: true,
} satisfies Prisma.NivelSelect;

type LevelRow = Prisma.NivelGetPayload<{ select: typeof levelSelect }>;

export interface LevelResponse {
  id: string;
  numero: number;
  nombre: string;
  descripcion: string | null;
  orden: number;
  xpRequerida: number;
  /** Porcentaje 0-100 (en la base es DECIMAL(5,2)). */
  precisionMinima: number;
}

export function toLevelResponse(row: LevelRow): LevelResponse {
  return {
    id: row.id,
    numero: row.numero,
    nombre: row.nombre,
    descripcion: row.descripcion,
    orden: row.orden,
    xpRequerida: row.xpRequerida,
    precisionMinima: row.precisionMinima.toNumber(),
  };
}

/**
 * Consulta del catálogo de niveles. Solo lectura: el rol senas_app tiene únicamente SELECT sobre
 * estas tablas, así que este servicio no escribe ni expone ningún método de escritura.
 */
@Injectable()
export class LevelsService {
  constructor(private readonly prisma: PrismaService) {}

  /** Todos los niveles ordenados por `orden`. El catálogo es pequeño, por eso no se pagina. */
  async findAll(): Promise<LevelResponse[]> {
    const rows = await this.prisma.nivel.findMany({ select: levelSelect, orderBy: { orden: 'asc' } });
    return rows.map(toLevelResponse);
  }

  /** `id` debe ser un UUID ya validado por el controlador. */
  async findOne(id: string): Promise<LevelResponse> {
    const row = await this.prisma.nivel.findUnique({ where: { id }, select: levelSelect });
    if (!row) throw new NotFoundException('Nivel no encontrado.');
    return toLevelResponse(row);
  }
}
