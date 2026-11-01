import { Injectable, NotFoundException } from '@nestjs/common';
import { Dificultad, Prisma, TipoSena } from '../generated/prisma/client.js';
import { LevelAccessService } from '../levels/level-access.service';
import { LevelLockedException } from '../levels/level-locked.exception';
import { PrismaService } from '../prisma/prisma.service';

/** Solo los campos educativos públicos de la seña: sin createdAt/updatedAt ni relaciones. */
export const senaSelect = {
  id: true,
  nombre: true,
  descripcion: true,
  tipo: true,
  dificultad: true,
  animacion: true,
  patronReferencia: true,
} satisfies Prisma.SenaSelect;

type SenaRow = Prisma.SenaGetPayload<{ select: typeof senaSelect }>;

export interface SenaResponse {
  id: string;
  nombre: string;
  descripcion: string | null;
  tipo: TipoSena;
  dificultad: Dificultad;
  /** Referencia a la animación en Unity. */
  animacion: string | null;
  /** JSON tal como está almacenado (formato MediaPipe evolutivo): no se transforma. */
  patronReferencia: Prisma.JsonValue | null;
}

/** Una seña dentro de un nivel: sus datos más la posición y obligatoriedad que define NivelSena. */
export interface LevelSenaResponse extends SenaResponse {
  orden: number;
  esObligatoria: boolean;
}

export function toSenaResponse(row: SenaRow): SenaResponse {
  return {
    id: row.id,
    nombre: row.nombre,
    descripcion: row.descripcion,
    tipo: row.tipo,
    dificultad: row.dificultad,
    animacion: row.animacion,
    patronReferencia: row.patronReferencia,
  };
}

/**
 * Consulta de señas. Solo lectura: senas_app tiene únicamente SELECT sobre `senas` y `nivel_senas`.
 * Los métodos `*ForPlayer` aplican las reglas de desbloqueo (delegadas en LevelAccessService, que es
 * la única implementación de las reglas) antes de entregar contenido.
 */
@Injectable()
export class SenasService {
  constructor(
    private readonly prisma: PrismaService,
    private readonly levelAccess: LevelAccessService,
  ) {}

  /**
   * Señas de un nivel para el jugador autenticado. 404 si el nivel no existe y 403 (`LEVEL_LOCKED`) si no
   * lo tiene desbloqueado; el contenido solo se consulta si el acceso está permitido.
   */
  async findByLevelForPlayer(userId: string, levelId: string): Promise<LevelSenaResponse[]> {
    await this.levelAccess.assertLevelUnlocked(userId, levelId);
    return this.findByLevel(levelId, { levelKnownToExist: true });
  }

  /**
   * Una seña para el jugador autenticado. Una seña puede pertenecer a varios niveles (NivelSena): basta
   * con que UNO esté desbloqueado. Si solo pertenece a niveles bloqueados: 403 `LEVEL_LOCKED`. Una seña
   * sin ningún nivel asociado se trata como no disponible (404): falla cerrado, porque no hay evidencia
   * en el diseño de señas fuera de un nivel (decisión aprobada).
   */
  async findOneForPlayer(userId: string, id: string): Promise<SenaResponse> {
    const row = await this.prisma.sena.findUnique({
      where: { id },
      select: { ...senaSelect, niveles: { select: { nivelId: true } } },
    });
    if (!row || row.niveles.length === 0) throw new NotFoundException('Seña no encontrada.');

    const unlocked = await this.levelAccess.unlockedLevelIds(userId);
    if (!row.niveles.some((link) => unlocked.has(link.nivelId))) {
      throw new LevelLockedException('La seña solo pertenece a niveles que todavía no están desbloqueados.');
    }
    return toSenaResponse(row);
  }

  /**
   * Señas de un nivel en el orden de NivelSena, SIN comprobar el acceso del jugador (usar
   * `findByLevelForPlayer` desde los controladores). Sin N+1: Prisma 7 carga la relación por lotes, así
   * que son 2 sentencias SQL constantes (nivel_senas ordenado + todas las señas con un único `IN (...)`),
   * sin importar cuántas señas haya. `(nivel_id, orden)` es único, por lo que el orden es determinista.
   * Si el resultado viene vacío se consulta el nivel (salvo que ya se sepa que existe) para distinguir
   * "nivel sin señas" (lista vacía) de "nivel inexistente" (404).
   */
  async findByLevel(levelId: string, options: { levelKnownToExist?: boolean } = {}): Promise<LevelSenaResponse[]> {
    const rows = await this.prisma.nivelSena.findMany({
      where: { nivelId: levelId },
      orderBy: { orden: 'asc' },
      select: { orden: true, esObligatoria: true, sena: { select: senaSelect } },
    });
    if (rows.length === 0 && !options.levelKnownToExist) {
      const nivel = await this.prisma.nivel.findUnique({ where: { id: levelId }, select: { id: true } });
      if (!nivel) throw new NotFoundException('Nivel no encontrado.');
    }
    return rows.map((item) => ({
      ...toSenaResponse(item.sena),
      orden: item.orden,
      esObligatoria: item.esObligatoria,
    }));
  }

  /** Una seña SIN comprobar el acceso del jugador. `id` debe ser un UUID ya validado. */
  async findOne(id: string): Promise<SenaResponse> {
    const row = await this.prisma.sena.findUnique({ where: { id }, select: senaSelect });
    if (!row) throw new NotFoundException('Seña no encontrada.');
    return toSenaResponse(row);
  }
}
