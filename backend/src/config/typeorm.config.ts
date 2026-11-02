import { TypeOrmModuleOptions } from '@nestjs/typeorm';
import { AppConfig } from './configuration';

/**
 * Connection data goes in separate fields (never `url`) so that
 * username/password cannot be overridden by whatever the URL carries.
 * SSL is configured here, not with sslmode in the URL.
 */
export function createTypeOrmOptions(cfg: AppConfig): TypeOrmModuleOptions {
  return {
    type: 'postgres',
    host: cfg.db.host,
    port: cfg.db.port,
    database: cfg.db.database,
    username: cfg.db.user,
    password: cfg.db.password,
    schema: 'public',
    autoLoadEntities: true,
    synchronize: cfg.db.synchronize,
    ssl: cfg.db.ssl ? { rejectUnauthorized: false } : false,
    extra: { max: cfg.db.poolMax },
    // gen_random_uuid() instead of uuid_generate_v4() (no uuid-ossp dependency).
    uuidExtension: 'pgcrypto',
    // TypeORM's logger prints query parameters (password hashes, tokens): disabled.
    logging: false,
  };
}
