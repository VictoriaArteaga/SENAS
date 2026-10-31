import { NestFactory } from '@nestjs/core';
import { AppModule } from './app.module';

async function bootstrap() {
  const app = await NestFactory.create(AppModule);
  // Permite que PrismaService cierre la conexión al recibir SIGTERM/SIGINT.
  app.enableShutdownHooks();
  // Pendiente para fases posteriores: prefijo /api/v1, ValidationPipe global y CORS restringido.
  await app.listen(process.env.PORT ?? 3000);
}
bootstrap();
