import { Module } from '@nestjs/common';
import { AuthModule } from '../auth/auth.module';
import { PrismaModule } from '../prisma/prisma.module';
import { LevelAccessService } from './level-access.service';
import { LevelsController } from './levels.controller';
import { LevelsService } from './levels.service';

@Module({
  imports: [PrismaModule, AuthModule],
  controllers: [LevelsController],
  providers: [LevelsService, LevelAccessService],
  exports: [LevelsService, LevelAccessService],
})
export class LevelsModule {}
