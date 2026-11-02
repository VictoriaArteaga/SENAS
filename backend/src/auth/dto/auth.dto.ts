import { ApiProperty, ApiPropertyOptional } from '@nestjs/swagger';
import { Transform } from 'class-transformer';
import {
  IsEmail,
  IsOptional,
  IsString,
  Length,
  Matches,
  MaxLength,
} from 'class-validator';

const toLowerCase = ({ value }: { value: unknown }) =>
  typeof value === 'string' ? value.trim().toLowerCase() : value;
const trim = ({ value }: { value: unknown }) =>
  typeof value === 'string' ? value.trim() : value;

export class RegisterDto {
  @ApiProperty({ example: 'ana@example.com' })
  @Transform(toLowerCase)
  @IsEmail()
  @MaxLength(254)
  email: string;

  @ApiProperty({
    minLength: 8,
    maxLength: 72,
    description:
      'At least 8 characters, with at least one letter and one number.',
    example: 'Lumina2026',
  })
  @IsString()
  @Length(8, 72)
  @Matches(/[A-Za-z]/, { message: 'password must contain at least one letter' })
  @Matches(/\d/, { message: 'password must contain at least one number' })
  password: string;

  @ApiProperty({ minLength: 2, maxLength: 40, example: 'Ana' })
  @Transform(trim)
  @IsString()
  @Length(2, 40)
  displayName: string;

  @ApiPropertyOptional({ example: 'luna', description: 'One of AVATAR_IDS' })
  @IsOptional()
  @IsString()
  @Length(1, 40)
  avatarId?: string;
}

export class LoginDto {
  @ApiProperty({ example: 'ana@example.com' })
  @Transform(toLowerCase)
  @IsEmail()
  @MaxLength(254)
  email: string;

  @ApiProperty({ example: 'Lumina2026' })
  @IsString()
  @Length(1, 72)
  password: string;
}

export class RefreshDto {
  @ApiProperty({
    description: 'Refresh token received at login or on the last refresh.',
  })
  @IsString()
  @MaxLength(200)
  refreshToken: string;
}

export class SessionUserDto {
  @ApiProperty({ format: 'uuid' }) id: string;
  @ApiProperty({ example: 'ana@example.com' }) email: string;
  @ApiProperty({ enum: ['PLAYER', 'ADMIN'] }) role: string;
}

export class TokensResponseDto {
  @ApiProperty({ description: 'Short-lived HS256 JWT.' }) accessToken: string;
  @ApiProperty({
    description: 'Format <sessionId>.<secret>. Rotated on every refresh.',
  })
  refreshToken: string;
  @ApiProperty({ example: 'Bearer' }) tokenType: 'Bearer';
  @ApiProperty({
    example: 900,
    description: 'Seconds the accessToken is valid for.',
  })
  expiresIn: number;
  @ApiProperty({ type: SessionUserDto }) user: SessionUserDto;
}
