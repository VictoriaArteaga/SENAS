import { ArgumentsHost, Catch, ExceptionFilter, HttpException, HttpStatus, Logger } from '@nestjs/common';
import { HttpAdapterHost } from '@nestjs/core';

/**
 * Las excepciones HTTP (validación, 401, 409...) se devuelven tal cual. Cualquier otro error
 * (Prisma, driver, bug) devuelve un 500 genérico, y al log solo va el nombre/código del error:
 * los mensajes de Prisma pueden incluir los argumentos de la consulta (hashes, correos).
 */
@Catch()
export class AllExceptionsFilter implements ExceptionFilter {
  private readonly logger = new Logger('Exceptions');

  constructor(private readonly httpAdapterHost: HttpAdapterHost) {}

  catch(exception: unknown, host: ArgumentsHost): void {
    const { httpAdapter } = this.httpAdapterHost;
    const response = host.switchToHttp().getResponse();

    if (exception instanceof HttpException) {
      const body = exception.getResponse();
      httpAdapter.reply(
        response,
        typeof body === 'string' ? { statusCode: exception.getStatus(), message: body } : body,
        exception.getStatus(),
      );
      return;
    }

    const e = exception as { name?: string; code?: string };
    this.logger.error(`Error no controlado: ${e?.name ?? 'Error'}${e?.code ? ` (${e.code})` : ''}`);
    httpAdapter.reply(
      response,
      { statusCode: HttpStatus.INTERNAL_SERVER_ERROR, message: 'Error interno del servidor.' },
      HttpStatus.INTERNAL_SERVER_ERROR,
    );
  }
}
