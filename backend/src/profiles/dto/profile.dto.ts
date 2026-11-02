import { ApiProperty, ApiPropertyOptional } from '@nestjs/swagger';
import { Transform, Type } from 'class-transformer';
import {
  IsIn,
  IsObject,
  IsOptional,
  IsString,
  Length,
  ValidateNested,
} from 'class-validator';
import type { Preferences } from '../entities/profile.entity';

const trim = ({ value }: { value: unknown }) =>
  typeof value === 'string' ? value.trim() : value;

export class PreferencesDto implements Preferences {
  @ApiPropertyOptional({ enum: ['es', 'en'], example: 'es' })
  @IsOptional()
  @IsIn(['es', 'en'])
  language?: 'es' | 'en';

  @ApiPropertyOptional({ enum: ['light', 'dark'], example: 'light' })
  @IsOptional()
  @IsIn(['light', 'dark'])
  theme?: 'light' | 'dark';
}

export class UpdateProfileDto {
  @ApiPropertyOptional({ minLength: 2, maxLength: 40, example: 'Maria' })
  @IsOptional()
  @Transform(trim)
  @IsString()
  @Length(2, 40)
  displayName?: string;

  @ApiPropertyOptional({ type: PreferencesDto })
  @IsOptional()
  @ValidateNested()
  @Type(() => PreferencesDto)
  preferences?: PreferencesDto;
}

export class UpdateAvatarDto {
  @ApiPropertyOptional({ example: 'luna', description: 'One of AVATAR_IDS' })
  @IsOptional()
  @IsString()
  @Length(1, 40)
  avatarId?: string;

  @ApiPropertyOptional({
    type: 'object',
    additionalProperties: true,
    description: 'Free-form avatar configuration (max. 4 KB).',
  })
  @IsOptional()
  @IsObject()
  avatarConfig?: Record<string, unknown>;
}

export class ProfileResponseDto {
  @ApiProperty({ format: 'uuid' }) userId: string;
  @ApiProperty({ example: 'ana@example.com' }) email: string;
  @ApiProperty({ example: 'Ana' }) displayName: string;
  @ApiProperty({ example: 'luna' }) avatarId: string;
  @ApiProperty({ type: 'object', additionalProperties: true })
  avatarConfig: Record<string, unknown>;
  @ApiProperty({ type: PreferencesDto }) preferences: Preferences;
  @ApiProperty({ example: 310 }) totalXp: number;
  @ApiProperty() createdAt: Date;
}
