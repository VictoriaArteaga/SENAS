import { Injectable, Logger, NotFoundException } from '@nestjs/common';
import { PrismaService } from '../prisma/prisma.service';
import {
  BlockReason,
  evaluateLevelAccess,
  findNumberingInconsistencies,
  LevelStatus,
  SenaProgress,
} from './level-access.rules';
import { LevelLockedException } from './level-locked.exception';
import { LevelResponse, LevelsService } from './levels.service';

/** Nivel del catálogo más la disponibilidad calculada para el jugador autenticado. */
export interface PlayerLevelResponse extends LevelResponse {
  estado: LevelStatus;
  desbloqueado: boolean;
  motivosBloqueo: BlockReason[];
}

/**
 * Disponibilidad de niveles para un jugador. SOLO LECTURA: calcula el estado a partir de datos que ya
 * existen (Perfil, Nivel, NivelSena, ProgresoSena); no otorga XP, no registra progreso y no cambia
 * Perfil.nivelActual. Las reglas viven en `level-access.rules.ts`.
 */
@Injectable()
export class LevelAccessService {
  private readonly logger = new Logger(LevelAccessService.name);
  /** Evita repetir el mismo aviso de catálogo inconsistente en cada petición. */
  private readonly warned = new Set<string>();

  constructor(
    private readonly prisma: PrismaService,
    private readonly levels: LevelsService,
  ) {}

  /**
   * Niveles ordenados por `orden`, con `estado`, `desbloqueado` y `motivosBloqueo`. `userId` debe venir
   * del JWT verificado. Son 3 consultas en paralelo + 1 (progreso) que se omite si ningún nivel tiene
   * señas obligatorias; el número no depende de cuántos niveles o señas haya (sin N+1).
   */
  async listForPlayer(userId: string): Promise<PlayerLevelResponse[]> {
    const [perfil, levels, mandatoryLinks] = await Promise.all([
      this.prisma.perfil.findUnique({ where: { usuarioId: userId }, select: { totalXp: true, nivelActual: true } }),
      this.levels.findAll(),
      this.prisma.nivelSena.findMany({ where: { esObligatoria: true }, select: { nivelId: true, senaId: true } }),
    ]);
    // No se crea el perfil automáticamente: registrar la cuenta ya lo crea de forma atómica.
    if (!perfil) throw new NotFoundException('Perfil de jugador no encontrado.');

    const mandatorySenasByLevel = new Map<string, string[]>();
    for (const link of mandatoryLinks) {
      const list = mandatorySenasByLevel.get(link.nivelId) ?? [];
      list.push(link.senaId);
      mandatorySenasByLevel.set(link.nivelId, list);
    }

    const senaIds = [...new Set(mandatoryLinks.map((link) => link.senaId))];
    const progressRows = senaIds.length
      ? await this.prisma.progresoSena.findMany({
          where: { usuarioId: userId, senaId: { in: senaIds } },
          select: { senaId: true, completada: true, mejorPrecision: true },
        })
      : [];
    const progressBySena = new Map<string, SenaProgress>(
      progressRows.map((row) => [row.senaId, { completada: row.completada, mejorPrecision: row.mejorPrecision.toNumber() }]),
    );

    this.warnIfNumberingIsInconsistent(levels);

    const access = evaluateLevelAccess(levels, {
      totalXp: perfil.totalXp,
      nivelActual: perfil.nivelActual,
      mandatorySenasByLevel,
      progressBySena,
    });
    const accessByLevel = new Map(access.map((item) => [item.levelId, item]));

    // `levels` ya viene ordenado por `orden` (LevelsService.findAll).
    return levels.map((level) => {
      const item = accessByLevel.get(level.id)!;
      return { ...level, estado: item.estado, desbloqueado: item.desbloqueado, motivosBloqueo: item.motivosBloqueo };
    });
  }

  /**
   * Comprueba que el jugador puede acceder al contenido del nivel. 404 si el nivel no existe, 403
   * (`LEVEL_LOCKED`) si está bloqueado. Reutiliza `listForPlayer`: no hay una segunda versión de las reglas.
   */
  async assertLevelUnlocked(userId: string, levelId: string): Promise<void> {
    const level = (await this.listForPlayer(userId)).find((item) => item.id === levelId);
    if (!level) throw new NotFoundException('Nivel no encontrado.');
    if (!level.desbloqueado) throw new LevelLockedException();
  }

  /** Ids de los niveles que el jugador tiene desbloqueados (estado ACTUAL o DESBLOQUEADO). */
  async unlockedLevelIds(userId: string): Promise<Set<string>> {
    const levels = await this.listForPlayer(userId);
    return new Set(levels.filter((level) => level.desbloqueado).map((level) => level.id));
  }

  private warnIfNumberingIsInconsistent(levels: LevelResponse[]): void {
    for (const issue of findNumberingInconsistencies(levels)) {
      const signature = `${issue.previous.orden}:${issue.previous.numero}>${issue.current.orden}:${issue.current.numero}`;
      if (this.warned.has(signature)) continue;
      this.warned.add(signature);
      this.logger.warn(
        `Catálogo inconsistente: el nivel con orden ${issue.current.orden} (numero ${issue.current.numero}) ` +
          `sigue al de orden ${issue.previous.orden} (numero ${issue.previous.numero}) pero su numero no es mayor. ` +
          'La progresión usa `orden`; revisa la numeración del contenido.',
      );
    }
  }
}
