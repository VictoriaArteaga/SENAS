import { Global, Module } from '@nestjs/common';
import { ConfigModule } from '@nestjs/config';
import { APP_CONFIG, loadConfig } from './configuration';

/**
 * Loads .env (if present), validates the variables and exposes the typed
 * configuration through the APP_CONFIG token. If anything is missing, the
 * application does not start.
 */
@Global()
@Module({
  imports: [
    ConfigModule.forRoot({
      envFilePath: '.env',
      // Tests never read .env, so they cannot touch the real Supabase database.
      ignoreEnvFile: process.env['NODE_ENV'] === 'test',
    }),
  ],
  providers: [{ provide: APP_CONFIG, useFactory: loadConfig }],
  exports: [APP_CONFIG],
})
export class AppConfigModule {}
