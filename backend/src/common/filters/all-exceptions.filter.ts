import {
  ArgumentsHost,
  Catch,
  ExceptionFilter,
  HttpException,
  HttpStatus,
  Logger,
} from '@nestjs/common';
import type { Request, Response } from 'express';
import { ApiError, ErrorCode } from '../api-error';

const CODE_BY_STATUS: Record<number, string> = {
  400: ErrorCode.VALIDATION_FAILED,
  401: ErrorCode.UNAUTHORIZED,
  403: ErrorCode.FORBIDDEN,
  404: ErrorCode.NOT_FOUND,
  429: ErrorCode.TOO_MANY_REQUESTS,
};

/** Uniform error format. 500 errors never expose internal details. */
@Catch()
export class AllExceptionsFilter implements ExceptionFilter {
  private readonly logger = new Logger('Exceptions');

  catch(exception: unknown, host: ArgumentsHost): void {
    const ctx = host.switchToHttp();
    const req = ctx.getRequest<Request>();
    const res = ctx.getResponse<Response>();

    let status = HttpStatus.INTERNAL_SERVER_ERROR;
    let code: string = ErrorCode.INTERNAL_ERROR;
    let message = 'Internal server error.';
    let details: string[] | undefined;

    if (exception instanceof ApiError) {
      status = exception.getStatus();
      code = exception.code;
      message = (exception.getResponse() as { message: string }).message;
      details = exception.details;
    } else if (exception instanceof HttpException) {
      status = exception.getStatus();
      code = CODE_BY_STATUS[status] ?? `HTTP_${status}`;
      const body = exception.getResponse();
      const raw =
        typeof body === 'string'
          ? body
          : ((body as { message?: string | string[] }).message ??
            exception.message);
      if (Array.isArray(raw)) {
        details = raw;
        message = 'The request is not valid.';
      } else {
        message = raw;
      }
      if (status === HttpStatus.TOO_MANY_REQUESTS) {
        message = 'Too many requests. Try again in a moment.';
      }
    } else {
      // Only the error name and message: never request bodies or credentials.
      const e = exception as Error;
      this.logger.error(
        `${req.method} ${req.url}: ${e?.name}: ${e?.message}`,
        e?.stack,
      );
    }

    res.status(status).json({
      statusCode: status,
      code,
      message,
      ...(details ? { details } : {}),
      path: req.originalUrl.split('?')[0],
      timestamp: new Date().toISOString(),
    });
  }
}
