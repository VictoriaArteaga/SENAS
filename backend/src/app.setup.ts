import {
  HttpStatus,
  INestApplication,
  ValidationError,
  ValidationPipe,
} from '@nestjs/common';
import { DocumentBuilder, SwaggerModule } from '@nestjs/swagger';
import type { NextFunction, Request, Response } from 'express';
import helmet from 'helmet';
import { ApiError, ErrorCode } from './common/api-error';
import { AllExceptionsFilter } from './common/filters/all-exceptions.filter';
import { APP_CONFIG, type AppConfig } from './config/configuration';

function flattenErrors(errors: ValidationError[], prefix = ''): string[] {
  return errors.flatMap((e) => {
    const field = prefix ? `${prefix}.${e.property}` : e.property;
    const own = Object.values(e.constraints ?? {});
    return [...own, ...flattenErrors(e.children ?? [], field)];
  });
}

/** Common app configuration; shared by main.ts and the e2e tests. */
export function configureApp(app: INestApplication): AppConfig {
  const cfg = app.get<AppConfig>(APP_CONFIG);

  app.setGlobalPrefix(cfg.apiPrefix);
  // Behind Render's proxy, req.ip must be the real client IP (throttling).
  if (cfg.nodeEnv === 'production') {
    (
      app.getHttpAdapter().getInstance() as {
        set: (k: string, v: unknown) => void;
      }
    ).set('trust proxy', 1);
  }

  // The Swagger UI needs inline scripts/styles: no CSP on /docs only.
  const strictHelmet = helmet();
  const docsHelmet = helmet({ contentSecurityPolicy: false });
  app.use((req: Request, res: Response, next: NextFunction) =>
    req.path.startsWith('/docs')
      ? docsHelmet(req, res, next)
      : strictHelmet(req, res, next),
  );

  // CORS is off unless origins are given (the client is a desktop app).
  if (cfg.corsOrigins.length > 0) app.enableCors({ origin: cfg.corsOrigins });

  app.useGlobalPipes(
    new ValidationPipe({
      whitelist: true,
      forbidNonWhitelisted: true,
      transform: true,
      exceptionFactory: (errors) =>
        new ApiError(
          HttpStatus.BAD_REQUEST,
          ErrorCode.VALIDATION_FAILED,
          'The request is not valid.',
          flattenErrors(errors),
        ),
    }),
  );
  app.useGlobalFilters(new AllExceptionsFilter());

  if (cfg.swaggerEnabled) {
    const document = SwaggerModule.createDocument(
      app,
      new DocumentBuilder()
        .setTitle('Señas: El mundo de las manos — API')
        .setDescription(
          'Backend of the Colombian Sign Language educational game. Unity is the only client.',
        )
        .setVersion('1.0.0')
        .addBearerAuth()
        .build(),
    );
    SwaggerModule.setup('docs', app, document);
  }

  app.enableShutdownHooks();
  return cfg;
}
