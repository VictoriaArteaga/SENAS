import { ApiProperty, ApiPropertyOptional } from '@nestjs/swagger';

export class ErrorResponseDto {
  @ApiProperty({ example: 403 })
  statusCode: number;

  @ApiProperty({
    example: 'SIGN_LOCKED',
    description: 'Machine-readable code',
  })
  code: string;

  @ApiProperty({ example: 'The sign is still locked.' })
  message: string;

  @ApiPropertyOptional({
    type: [String],
    description: 'Validation details (400 only)',
  })
  details?: string[];

  @ApiProperty({ example: '/api/v1/evaluations' })
  path: string;

  @ApiProperty({ example: '2026-11-01T12:00:00.000Z' })
  timestamp: string;
}
