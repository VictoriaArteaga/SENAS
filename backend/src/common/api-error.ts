import { HttpException, HttpStatus } from '@nestjs/common';

/** Machine-readable error codes that Unity can react to. */
export const ErrorCode = {
  VALIDATION_FAILED: 'VALIDATION_FAILED',
  UNAUTHORIZED: 'UNAUTHORIZED',
  INVALID_CREDENTIALS: 'INVALID_CREDENTIALS',
  INVALID_SESSION: 'INVALID_SESSION',
  INACTIVE_USER: 'INACTIVE_USER',
  FORBIDDEN: 'FORBIDDEN',
  NOT_FOUND: 'NOT_FOUND',
  EMAIL_IN_USE: 'EMAIL_IN_USE',
  INVALID_AVATAR: 'INVALID_AVATAR',
  INVALID_AVATAR_CONFIG: 'INVALID_AVATAR_CONFIG',
  LEVEL_NOT_FOUND: 'LEVEL_NOT_FOUND',
  SIGN_NOT_FOUND: 'SIGN_NOT_FOUND',
  LEVEL_LOCKED: 'LEVEL_LOCKED',
  SIGN_LOCKED: 'SIGN_LOCKED',
  DUPLICATE_ATTEMPT: 'DUPLICATE_ATTEMPT',
  INCONSISTENT_RESULT: 'INCONSISTENT_RESULT',
  TOO_MANY_REQUESTS: 'TOO_MANY_REQUESTS',
  INTERNAL_ERROR: 'INTERNAL_ERROR',
} as const;
export type ErrorCodeValue = (typeof ErrorCode)[keyof typeof ErrorCode];

/** HTTP exception with a stable `code`; the global filter serializes it. */
export class ApiError extends HttpException {
  constructor(
    status: HttpStatus,
    readonly code: ErrorCodeValue,
    message: string,
    readonly details?: string[],
  ) {
    super({ code, message, details }, status);
  }
}
