import { NestFactory } from '@nestjs/core';
import { Module } from '@nestjs/common';
import { ConfigModule } from '@nestjs/config';
import { PrismaModule } from './prisma.module';
import { PrismaService } from './prisma.service';

@Module({
  imports: [ConfigModule.forRoot({ isGlobal: true }), PrismaModule],
})
class CheckModule {}

/**
 * Verificación de solo lectura de la conexión con PostgreSQL (SELECT 1).
 * Uso: npm run build && npm run db:check
 */
async function main(): Promise<void> {
  const app = await NestFactory.createApplicationContext(CheckModule, {
    logger: ['error', 'warn', 'log'],
  });
  try {
    const rows = await app
      .get(PrismaService)
      .$queryRaw<{ ok: number }[]>`SELECT 1 AS ok`;
    if (Number(rows[0]?.ok) !== 1) throw new Error('Respuesta inesperada');
    console.log('OK: SELECT 1 ejecutado correctamente');
  } finally {
    await app.close();
  }
}

main().catch((error: unknown) => {
  // Solo se muestra el nombre/código del error, nunca la URL de conexión.
  const e = error as { name?: string; code?: string; message?: string };
  console.error(`FALLO: ${e.name ?? 'Error'} ${e.code ?? ''}`.trim());
  process.exitCode = 1;
});
