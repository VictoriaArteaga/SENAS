import { Module } from '@nestjs/common';
import { AuthModule } from '../auth/auth.module';
import { LevelsModule } from '../levels/levels.module';
import { PrismaModule } from '../prisma/prisma.module';
import { LevelSenasController, SenasController } from './senas.controller';
import { SenasService } from './senas.service';

@Module({
  // LevelsModule aporta LevelAccessService, la única implementación de las reglas de desbloqueo.
  imports: [PrismaModule, AuthModule, LevelsModule],
  controllers: [LevelSenasController, SenasController],
  providers: [SenasService],
  exports: [SenasService],
})
export class SenasModule {}
