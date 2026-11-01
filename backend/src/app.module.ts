import { Module, ValidationPipe } from '@nestjs/common';
import { ConfigModule } from '@nestjs/config';
import { APP_FILTER, APP_PIPE } from '@nestjs/core';
import { ThrottlerModule } from '@nestjs/throttler';
import { AppController } from './app.controller';
import { AppService } from './app.service';
import { AuthModule } from './auth/auth.module';
import { AllExceptionsFilter } from './common/filters/all-exceptions.filter';
import { validateEnv } from './config/env.validation';
import { LevelsModule } from './levels/levels.module';
import { PrismaModule } from './prisma/prisma.module';
import { SenasModule } from './senas/senas.module';
import { SessionsModule } from './sessions/sessions.module';
import { UsersModule } from './users/users.module';

@Module({
  imports: [
    ConfigModule.forRoot({
      isGlobal: true,
      validate: validateEnv,
      // Las pruebas nunca leen .env (podría apuntar a la base real).
      ignoreEnvFile: process.env.NODE_ENV === 'test',
    }),
    // Rate limiting en memoria. El guard se aplica solo a login, register y refresh
    // (ver AuthController); los límites reales se resuelven por petición desde la configuración.
    ThrottlerModule.forRoot({
      throttlers: [{ name: 'default', ttl: 60_000, limit: 10 }],
      errorMessage: 'Demasiados intentos. Inténtalo de nuevo más tarde.',
    }),
    PrismaModule,
    UsersModule,
    SessionsModule,
    AuthModule,
    LevelsModule,
    SenasModule,
  ],
  controllers: [AppController],
  providers: [
    AppService,
    {
      // Validación global (también en las pruebas, porque vive en el módulo y no en main.ts).
      provide: APP_PIPE,
      useFactory: () =>
        new ValidationPipe({ whitelist: true, forbidNonWhitelisted: true, transform: true }),
    },
    { provide: APP_FILTER, useClass: AllExceptionsFilter },
  ],
})
export class AppModule {}
