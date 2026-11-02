import { Module } from '@nestjs/common';
import { TypeOrmModule } from '@nestjs/typeorm';
import { LevelSign } from '../levels/entities/level-sign.entity';
import { Sign } from './entities/sign.entity';
import { SignsController } from './signs.controller';
import { SignsService } from './signs.service';

@Module({
  imports: [TypeOrmModule.forFeature([Sign, LevelSign])],
  controllers: [SignsController],
  providers: [SignsService],
})
export class SignsModule {}
