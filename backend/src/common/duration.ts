const UNIT_SECONDS = { s: 1, m: 60, h: 3600, d: 86400 } as const;

/**
 * Convierte duraciones tipo "15m", "30d", "12h" o "90s" a segundos.
 * Devuelve null si el formato no es válido (no lanza, para poder validar configuración).
 */
export function parseDurationToSeconds(value: unknown): number | null {
  if (typeof value !== 'string') return null;
  const match = /^(\d+)(s|m|h|d)$/.exec(value.trim());
  if (!match) return null;
  const seconds = Number(match[1]) * UNIT_SECONDS[match[2] as keyof typeof UNIT_SECONDS];
  return Number.isSafeInteger(seconds) && seconds > 0 ? seconds : null;
}
