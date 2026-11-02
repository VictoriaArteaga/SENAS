import { LogLevel, Logger } from '@nestjs/common';
import { NestFactory } from '@nestjs/core';
import { AppModule } from './app.module';
import { configureApp } from './app.setup';

const LOG_LEVELS: Record<string, LogLevel[]> = {
  production: ['log', 'warn', 'error'],
  test: ['warn', 'error'],
  development: ['log', 'warn', 'error', 'debug', 'verbose'],
};

async function bootstrap() {
  const app = await NestFactory.create(AppModule, {
    logger:
      LOG_LEVELS[process.env['NODE_ENV'] ?? 'development'] ??
      LOG_LEVELS['development'],
  });
  const cfg = configureApp(app);
  await app.listen(cfg.port);

  const logger = new Logger('Bootstrap');
  logger.log(`API ready on port ${cfg.port} (/${cfg.apiPrefix})`);
  if (cfg.swaggerEnabled) logger.log('Swagger at /docs');
}

bootstrap().catch((e: unknown) => {
  // Message only: never environment variables or credentials.
  new Logger('Bootstrap').error(`Could not start: ${(e as Error).message}`);
  process.exit(1);
});
