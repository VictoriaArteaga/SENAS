import { ApiProperty, ApiPropertyOptional } from '@nestjs/swagger';
import { Transform } from 'class-transformer';
import {
  IsBoolean,
  IsISO8601,
  IsOptional,
  IsString,
  Length,
} from 'class-validator';
import { PaginationDto } from '../../common/dto/pagination.dto';

const toBoolean = ({ value }: { value: unknown }) =>
  value === 'true' || value === true
    ? true
    : value === 'false' || value === false
      ? false
      : value;

export class HistoryFilterDto extends PaginationDto {
  @ApiPropertyOptional({ example: 'A', description: 'Sign code' })
  @IsOptional()
  @IsString()
  @Length(1, 60)
  sign?: string;

  @ApiPropertyOptional({ example: 'ALPHABET', description: 'Level code' })
  @IsOptional()
  @IsString()
  @Length(1, 40)
  level?: string;

  @ApiPropertyOptional()
  @IsOptional()
  @Transform(toBoolean)
  @IsBoolean()
  passed?: boolean;

  @ApiPropertyOptional({ example: '2026-11-01T00:00:00Z' })
  @IsOptional()
  @IsISO8601()
  from?: string;

  @ApiPropertyOptional({ example: '2026-11-30T23:59:59Z' })
  @IsOptional()
  @IsISO8601()
  to?: string;
}

export class HistoryItemDto {
  @ApiProperty({ format: 'uuid' }) id: string;
  @ApiProperty({ format: 'uuid' }) attemptId: string;
  @ApiProperty({ example: 'A' }) signCode: string;
  @ApiProperty({ example: 'ALPHABET' }) levelCode: string;
  @ApiProperty({ example: 82 }) precision: number;
  @ApiProperty({ description: 'Stars as reported by the AI.' })
  reportedStars: number;
  @ApiProperty() reportedXp: number;
  @ApiProperty() passed: boolean;
  @ApiProperty() awardedXp: number;
  @ApiProperty({ nullable: true, type: Number }) durationMs: number | null;
  @ApiProperty({ nullable: true, type: String }) feedback: string | null;
  @ApiProperty() createdAt: Date;
}

export class HistoryResponseDto {
  @ApiProperty({ type: [HistoryItemDto] }) items: HistoryItemDto[];
  @ApiProperty() total: number;
  @ApiProperty() page: number;
  @ApiProperty() limit: number;
}
