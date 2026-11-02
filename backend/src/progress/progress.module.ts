import { Module } from '@nestjs/common';
import { TypeOrmModule } from '@nestjs/typeorm';
import { LevelsModule } from '../levels/levels.module';
import { SignProgress } from './entities/sign-progress.entity';
import { ProgressController } from './progress.controller';
import { ProgressService } from './progress.service';

@Module({
  imports: [TypeOrmModule.forFeature([SignProgress]), LevelsModule],
  controllers: [ProgressController],
  providers: [ProgressService],
})
export class ProgressModule {}
