import { Module } from '@nestjs/common';
import { TypeOrmModule } from '@nestjs/typeorm';
import { APP_CONFIG, type AppConfig } from '../config/configuration';
import { createTypeOrmOptions } from '../config/typeorm.config';
import { DatabaseBootstrapService } from './database-bootstrap.service';
import { CatalogSeeder } from './seeds/catalog.seeder';

@Module({
  imports: [
    TypeOrmModule.forRootAsync({
      inject: [APP_CONFIG],
      useFactory: (cfg: AppConfig) => createTypeOrmOptions(cfg),
    }),
  ],
  providers: [CatalogSeeder, DatabaseBootstrapService],
})
export class DatabaseModule {}
