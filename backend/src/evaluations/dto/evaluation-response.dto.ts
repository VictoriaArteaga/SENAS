import { ApiProperty } from '@nestjs/swagger';

export class EvaluationResponseDto {
  @ApiProperty({ format: 'uuid' }) evaluationId: string;
  @ApiProperty({ format: 'uuid' }) attemptId: string;
  @ApiProperty({ example: 'A' }) signCode: string;
  @ApiProperty({
    description:
      'Decided by the backend by comparing precision with the threshold.',
  })
  passed: boolean;
  @ApiProperty({ example: 82 }) precision: number;
  @ApiProperty({
    minimum: 0,
    maximum: 3,
    description: '0 if the attempt did not pass.',
  })
  stars: number;
  @ApiProperty({
    example: 20,
    description:
      'Only the improvement over what was already earned on the sign.',
  })
  awardedXp: number;
  @ApiProperty({ example: 310 }) totalXp: number;
  @ApiProperty({ example: 82 }) bestPrecision: number;
  @ApiProperty({ example: 2 }) bestStars: number;
  @ApiProperty({ nullable: true, type: String, example: 'B' })
  unlockedSign: string | null;
  @ApiProperty({
    type: [String],
    example: ['NUMBERS'],
    description: 'Only the levels unlocked by this attempt.',
  })
  unlockedLevels: string[];
}
