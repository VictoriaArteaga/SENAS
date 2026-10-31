import { IsNotEmpty, IsString, MaxLength } from 'class-validator';
import { REFRESH_TOKEN_MAX_LENGTH } from '../token.util';

/** Cuerpo de /auth/refresh y /auth/logout. El formato exacto lo valida AuthService. */
export class RefreshTokenDto {
  @IsString({ message: 'refreshToken es obligatorio.' })
  @IsNotEmpty({ message: 'refreshToken es obligatorio.' })
  @MaxLength(REFRESH_TOKEN_MAX_LENGTH, { message: 'refreshToken no es válido.' })
  refreshToken!: string;
}
