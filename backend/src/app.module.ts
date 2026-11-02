import { Module } from '@nestjs/common';
import { APP_GUARD } from '@nestjs/core';
import { ThrottlerGuard, ThrottlerModule } from '@nestjs/throttler';
import { AuthModule } from './auth/auth.module';
import { JwtAuthGuard } from './auth/jwt-auth.guard';
import { STRICT_THROTTLE } from './common/decorators/strict-throttle.decorator';
import { AppConfigModule } from './config/app-config.module';
import { APP_CONFIG, type AppConfig } from './config/configuration';
import { DatabaseModule } from './database/database.module';
import { EvaluationsModule } from './evaluations/evaluations.module';
import { HealthModule } from './health/health.module';
import { LevelsModule } from './levels/levels.module';
import { ProfilesModule } from './profiles/profiles.module';
import { ProgressModule } from './progress/progress.module';
import { SignsModule } from './signs/signs.module';
import { UsersModule } from './users/users.module';

@Module({
  imports: [
    AppConfigModule,
    ThrottlerModule.forRootAsync({
      inject: [APP_CONFIG],
      useFactory: (cfg: AppConfig) => ({
        throttlers: [
          // Global, wider limit.
          {
            name: 'global',
            ttl: cfg.throttle.ttlMs,
            limit: cfg.throttle.limit,
          },
          // Strict limit, only on register / login / refresh (@StrictThrottle).
          {
            name: 'auth',
            ttl: cfg.throttle.ttlMs,
            limit: cfg.throttle.authLimit,
            skipIf: (ctx) =>
              !Reflect.getMetadata(STRICT_THROTTLE, ctx.getHandler()),
          },
        ],
      }),
    }),
    DatabaseModule,
    UsersModule,
    ProfilesModule,
    AuthModule,
    SignsModule,
    LevelsModule,
    EvaluationsModule,
    ProgressModule,
    HealthModule,
  ],
  providers: [
    // Order matters: rate limiting first, then authentication.
    { provide: APP_GUARD, useClass: ThrottlerGuard },
    { provide: APP_GUARD, useClass: JwtAuthGuard },
  ],
})
export class AppModule {}
