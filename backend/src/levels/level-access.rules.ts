/**
 * Reglas de desbloqueo de niveles. Funciones PURAS: sin base de datos, sin efectos, sin hora ni azar.
 * Reciben los datos ya leídos y devuelven el estado de cada nivel para un jugador.
 *
 * Regla aprobada: un nivel se desbloquea cuando el jugador tiene la XP que ese nivel exige y demostró
 * la precisión mínima en CADA seña obligatoria del nivel anterior. Y, como no se puede saltar la
 * progresión, el nivel anterior también debe estar desbloqueado.
 *
 *   desbloqueado(N₁) = XP(N₁)
 *   desbloqueado(Nᵢ) = XP(Nᵢ) ∧ OBLIGATORIAS(Nᵢ₋₁) ∧ PRECISIÓN(Nᵢ₋₁) ∧ desbloqueado(Nᵢ₋₁)
 *
 *   XP(N)         ⇔ Perfil.totalXp ≥ N.xpRequerida                  (la XP exigida es la del nivel que se abre)
 *   OBLIGATORIAS  ⇔ cada seña con esObligatoria = true del nivel anterior tiene un ProgresoSena con completada = true
 *   PRECISIÓN     ⇔ para cada una de esas señas, mejorPrecision ≥ precisionMinima DEL NIVEL ANTERIOR
 *
 * La precisión se evalúa seña por seña: una seña con 100 % no compensa a otra por debajo del umbral, y
 * Perfil.precisionPromedio no interviene. El orden de progresión lo da `orden` (no `numero`, ni el id,
 * ni el orden de inserción).
 */

/** Códigos estables de bloqueo (los clientes se basan en ellos, no en textos). */
export const BLOCK_REASONS = [
  'XP_INSUFICIENTE',
  'SENAS_OBLIGATORIAS_PENDIENTES',
  'PRECISION_INSUFICIENTE',
  'NIVEL_ANTERIOR_BLOQUEADO',
] as const;
export type BlockReason = (typeof BLOCK_REASONS)[number];

export type LevelStatus = 'ACTUAL' | 'DESBLOQUEADO' | 'BLOQUEADO';

export interface RuleLevel {
  id: string;
  numero: number;
  orden: number;
  xpRequerida: number;
  /** Porcentaje 0-100. Se aplica a las señas obligatorias de ESTE nivel cuando se intenta abrir el siguiente. */
  precisionMinima: number;
}

export interface SenaProgress {
  completada: boolean;
  /** Porcentaje 0-100. */
  mejorPrecision: number;
}

export interface PlayerState {
  totalXp: number;
  /** Perfil.nivelActual. Se interpreta como Nivel.numero (no hay clave foránea; ver README). */
  nivelActual: number;
  /** Señas con esObligatoria = true, por id de nivel. Un nivel sin entrada no tiene señas obligatorias. */
  mandatorySenasByLevel: ReadonlyMap<string, readonly string[]>;
  /** Progreso del jugador por id de seña. Una seña sin entrada nunca se ha practicado. */
  progressBySena: ReadonlyMap<string, SenaProgress>;
}

export interface LevelAccess {
  levelId: string;
  estado: LevelStatus;
  desbloqueado: boolean;
  motivosBloqueo: BlockReason[];
}

/** Evalúa todos los niveles para un jugador. Devuelve los resultados ordenados por `orden`. */
export function evaluateLevelAccess(levels: readonly RuleLevel[], player: PlayerState): LevelAccess[] {
  const ordered = sortByProgression(levels);
  const result: LevelAccess[] = [];
  let previousUnlocked = true;

  ordered.forEach((level, index) => {
    const previous = index > 0 ? ordered[index - 1] : undefined;
    const reasons = new Set<BlockReason>();

    if (player.totalXp < level.xpRequerida) reasons.add('XP_INSUFICIENTE');

    if (previous) {
      // Se cuentan sin duplicados: una seña repetida en la lista no cambia el resultado.
      const mandatory = new Set(player.mandatorySenasByLevel.get(previous.id) ?? []);
      for (const senaId of mandatory) {
        const progress = player.progressBySena.get(senaId);
        if (!progress || !progress.completada) reasons.add('SENAS_OBLIGATORIAS_PENDIENTES');
        // Condiciones independientes: una seña sin completar con mejor precisión baja incumple ambas.
        if (progress && progress.mejorPrecision < previous.precisionMinima) reasons.add('PRECISION_INSUFICIENTE');
      }
      if (!previousUnlocked) reasons.add('NIVEL_ANTERIOR_BLOQUEADO');
    }

    const motivosBloqueo = BLOCK_REASONS.filter((reason) => reasons.has(reason));
    const desbloqueado = motivosBloqueo.length === 0;
    previousUnlocked = desbloqueado;

    // ACTUAL solo se asigna a un nivel accesible: si Perfil.nivelActual apunta a un nivel que no cumple
    // los requisitos, el nivel aparece BLOQUEADO con sus motivos reales (no se oculta la incoherencia).
    const estado: LevelStatus = !desbloqueado ? 'BLOQUEADO' : level.numero === player.nivelActual ? 'ACTUAL' : 'DESBLOQUEADO';
    result.push({ levelId: level.id, estado, desbloqueado, motivosBloqueo });
  });

  return result;
}

/** Niveles en orden de progresión (`orden` ascendente) sin modificar el arreglo recibido. */
export function sortByProgression<T extends { orden: number }>(levels: readonly T[]): T[] {
  return [...levels].sort((a, b) => a.orden - b.orden);
}

export interface NumberingInconsistency {
  previous: { orden: number; numero: number };
  current: { orden: number; numero: number };
}

/**
 * Parejas consecutivas (por `orden`) cuyo `numero` no es estrictamente creciente. No existe ninguna
 * restricción en la base de datos que relacione `numero` y `orden`, así que puede ocurrir al cargar
 * contenido. La progresión sigue usando `orden`; esto solo permite avisar del problema.
 */
export function findNumberingInconsistencies(levels: readonly RuleLevel[]): NumberingInconsistency[] {
  const ordered = sortByProgression(levels);
  const found: NumberingInconsistency[] = [];
  for (let i = 1; i < ordered.length; i++) {
    if (ordered[i].numero <= ordered[i - 1].numero) {
      found.push({
        previous: { orden: ordered[i - 1].orden, numero: ordered[i - 1].numero },
        current: { orden: ordered[i].orden, numero: ordered[i].numero },
      });
    }
  }
  return found;
}
