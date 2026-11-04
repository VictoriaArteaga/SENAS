import { ApiProperty } from '@nestjs/swagger';
import { SignType } from '../../signs/entities/sign.entity';
import { SignStatus } from '../unlocks';

export class LevelSummaryDto {
  @ApiProperty({ example: 'ALPHABET' }) code: string;
  @ApiProperty({ example: 'Alphabet' }) name: string;
  @ApiProperty({ nullable: true, type: String }) description: string | null;
  @ApiProperty({ example: 1 }) position: number;
  @ApiProperty() unlocked: boolean;
  @ApiProperty({ example: 270 }) requiredXp: number;
  @ApiProperty({
    example: 150,
    description: 'XP still needed to unlock (0 if already unlocked).',
  })
  missingXp: number;
  @ApiProperty({ example: 5 }) passedSigns: number;
  @ApiProperty({ example: 27 }) totalSigns: number;
  @ApiProperty({ example: 11 }) earnedStars: number;
  @ApiProperty({ example: 81 }) possibleStars: number;
}

export class LevelSignDto {
  @ApiProperty({ example: 'A' }) code: string;
  @ApiProperty({ example: 'Letter A' }) name: string;
  @ApiProperty({ enum: SignType }) type: SignType;
  @ApiProperty({ example: 1 }) position: number;
  @ApiProperty({ enum: SignStatus }) status: SignStatus;
  @ApiProperty({ nullable: true, type: Number, example: 82 })
  bestPrecision: number | null;
  @ApiProperty({ minimum: 0, maximum: 3, example: 2 }) bestStars: number;
}

export class LevelDetailDto extends LevelSummaryDto {
  @ApiProperty({ type: [LevelSignDto] }) signs: LevelSignDto[];
}
