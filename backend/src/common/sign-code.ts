/**
 * Sign codes are the identifiers Unity uses: bare letters (A…Z, Ñ), numbers
 * (numeros.0 … numeros.30) and, for levels Unity has not implemented yet, the
 * backend's own codes (MONTH_JANUARY, WORD_HELLO, ...). They may contain
 * letters (including Ñ), digits, underscores and dots.
 */
export const SIGN_CODE_PATTERN = /^[\p{L}\p{N}_.]+$/u;
export const SIGN_CODE_MAX_LENGTH = 60;

/** Sign codes are matched case-insensitively; the catalog's spelling is canonical. */
export function sameSignCode(a: string, b: string): boolean {
  return a.toLowerCase() === b.toLowerCase();
}
