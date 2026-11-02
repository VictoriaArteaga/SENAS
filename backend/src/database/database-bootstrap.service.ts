import {
  Inject,
  Injectable,
  Logger,
  OnApplicationBootstrap,
} from '@nestjs/common';
import { DataSource, EntityManager } from 'typeorm';
import { APP_CONFIG, type AppConfig } from '../config/configuration';
import { CatalogSeeder } from './seeds/catalog.seeder';

/**
 * Runs after TypeORM has synchronized the tables:
 *  1. hardens security (RLS + REVOKE from anon/authenticated),
 *  2. loads the catalog (if DB_SEED_ON_START=true),
 *  3. verifies the catalog.
 * All in one transaction: if anything fails, the application does not start.
 */
@Injectable()
export class DatabaseBootstrapService implements OnApplicationBootstrap {
  private readonly logger = new Logger(DatabaseBootstrapService.name);

  constructor(
    private readonly dataSource: DataSource,
    private readonly seeder: CatalogSeeder,
    @Inject(APP_CONFIG) private readonly config: AppConfig,
  ) {}

  async onApplicationBootstrap(): Promise<void> {
    // Host and database only: never the full URL or the password.
    this.logger.log(
      `Database: ${this.config.db.host}/${this.config.db.database} (synchronize=${this.config.db.synchronize})`,
    );
    await this.dataSource.transaction(async (em) => {
      await this.hardenSecurity(em);
      const verified = async () => this.seeder.verify(em);
      if (this.config.db.seedOnStart) {
        await this.seeder.seed(em);
        // A mismatch throws: the transaction rolls back and the app does not start.
        if (!(await verified())) {
          throw new Error(
            'The catalog does not match the official one (see the log above); rolled back.',
          );
        }
      } else {
        this.logger.log('Catalog seed disabled (DB_SEED_ON_START=false).');
        await verified();
      }
    });
  }

  /**
   * ENABLE ROW LEVEL SECURITY (no policies) and REVOKE ALL from
   * anon/authenticated on every project table. The backend uses the role that
   * owns the tables, which RLS does not affect. Idempotent; if the roles do not
   * exist (plain PostgreSQL without Supabase) the REVOKE is skipped.
   */
  private async hardenSecurity(em: EntityManager): Promise<void> {
    const tables = this.dataSource.entityMetadatas
      .map((m) => m.tableName)
      .filter((t) => /^[a-z_][a-z0-9_]*$/.test(t));
    if (tables.length === 0) return;

    const list = tables.map((t) => `'${t}'`).join(', ');
    await em.query(`
      DO $$
      DECLARE t text;
      BEGIN
        FOREACH t IN ARRAY ARRAY[${list}] LOOP
          EXECUTE format('ALTER TABLE public.%I ENABLE ROW LEVEL SECURITY', t);
          IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'anon') THEN
            EXECUTE format('REVOKE ALL ON TABLE public.%I FROM anon', t);
          END IF;
          IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'authenticated') THEN
            EXECUTE format('REVOKE ALL ON TABLE public.%I FROM authenticated', t);
          END IF;
        END LOOP;
      END
      $$;
    `);
    this.logger.log(
      `Security applied (RLS + REVOKE) to ${tables.length} tables.`,
    );
  }
}
