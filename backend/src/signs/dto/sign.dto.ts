import { ApiProperty, ApiPropertyOptional } from '@nestjs/swagger';
import { IsEnum, IsOptional, IsString, Length } from 'class-validator';
import { SignType } from '../entities/sign.entity';

export class SignFilterDto {
  @ApiPropertyOptional({ enum: SignType })
  @IsOptional()
  @IsEnum(SignType)
  type?: SignType;

  @ApiPropertyOptional({ example: 'ALPHABET', description: 'Level code' })
  @IsOptional()
  @IsString()
  @Length(1, 40)
  level?: string;
}

export class SignLocationDto {
  @ApiProperty({ example: 'ALPHABET' }) level: string;
  @ApiProperty({ example: 1 }) position: number;
}

export class SignResponseDto {
  @ApiProperty({ example: 'A' }) code: string;
  @ApiProperty({ example: 'Letter A' }) name: string;
  @ApiProperty({ enum: SignType }) type: SignType;
  @ApiProperty({ nullable: true, type: String }) description: string | null;
  @ApiProperty({
    type: [SignLocationDto],
    description: 'Levels where the sign appears.',
  })
  levels: SignLocationDto[];
}
