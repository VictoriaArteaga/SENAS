/** Normalización única de identificadores de cuenta (DTOs y servicios usan la misma). */
export const normalizeEmail = (value: string): string => value.trim().toLowerCase();
export const normalizeUsername = (value: string): string => value.trim().toLowerCase();
