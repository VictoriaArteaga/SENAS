import { ApiProperty } from '@nestjs/swagger';
import { SignStatus } from '../../levels/unlocks';

export class LevelReferenceDto {
  @ApiProperty({ example: 'NUMBERS' }) code: string;
  @ApiProperty({ example: 'Numbers' }) name: string;
  @ApiProperty({ example: 300 }) requiredXp: number;
}

export class NextLevelDto extends LevelReferenceDto {
  @ApiProperty({ example: 120 }) missingXp: number;
}

export class ProgressSummaryDto {
  @ApiProperty({ example: 180 }) totalXp: number;
  @ApiProperty({ type: LevelReferenceDto, nullable: true })
  highestUnlockedLevel: LevelReferenceDto | null;
  @ApiProperty({
    type: NextLevelDto,
    nullable: true,
    description: 'null if all levels are already unlocked.',
  })
  nextLevel: NextLevelDto | null;
  @ApiProperty({ example: 12 }) passedSigns: number;
  @ApiProperty({ example: 81 }) totalSigns: number;
  @ApiProperty({ example: 25 }) totalStars: number;
  @ApiProperty({ example: 40 }) totalAttempts: number;
  @ApiProperty({
    example: 0.55,
    description: 'passed attempts / total attempts (0 if there are none).',
  })
  passRate: number;
}

export class SignProgressDto {
  @ApiProperty({ example: 'A' }) signCode: string;
  @ApiProperty({ example: 'Letter A' }) name: string;
  @ApiProperty({ enum: SignStatus }) status: SignStatus;
  @ApiProperty() totalAttempts: number;
  @ApiProperty() passedAttempts: number;
  @ApiProperty({ nullable: true, type: Number }) bestPrecision: number | null;
  @ApiProperty({ minimum: 0, maximum: 3 }) bestStars: number;
  @ApiProperty({ minimum: 0, maximum: 30 }) earnedXp: number;
  @ApiProperty() passed: boolean;
  @ApiProperty({ nullable: true, type: Date }) firstPassedAt: Date | null;
  @ApiProperty({ nullable: true, type: Date }) lastAttemptAt: Date | null;
}
