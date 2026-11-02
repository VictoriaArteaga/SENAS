import { Module } from '@nestjs/common';
import { TypeOrmModule } from '@nestjs/typeorm';
import { LevelsModule } from '../levels/levels.module';
import { Evaluation } from './entities/evaluation.entity';
import { EvaluationsController } from './evaluations.controller';
import { EvaluationsService } from './evaluations.service';

@Module({
  imports: [TypeOrmModule.forFeature([Evaluation]), LevelsModule],
  controllers: [EvaluationsController],
  providers: [EvaluationsService],
})
export class EvaluationsModule {}
