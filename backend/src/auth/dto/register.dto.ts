import { Transform } from 'class-transformer';
import { IsEmail, IsNotEmpty, IsString, Matches, MaxLength, MinLength } from 'class-validator';
import { normalizeEmail, normalizeUsername } from '../../common/normalize';

const trimmed = ({ value }: { value: unknown }) => (typeof value === 'string' ? value.trim() : value);

export class RegisterDto {
  @Transform(({ value }) => (typeof value === 'string' ? normalizeUsername(value) : value))
  @IsString({ message: 'El nombre de usuario es obligatorio.' })
  @Matches(/^[a-z0-9_]{3,30}$/, {
    message: 'El nombre de usuario debe tener entre 3 y 30 caracteres: letras, números o guion bajo.',
  })
  username!: string;

  @Transform(({ value }) => (typeof value === 'string' ? normalizeEmail(value) : value))
  @IsEmail({}, { message: 'El correo electrónico no es válido.' })
  @MaxLength(254, { message: 'El correo electrónico es demasiado largo.' })
  email!: string;

  // La contraseña no se recorta ni se transforma. El máximo evita hashing de entradas gigantes (DoS).
  @IsString({ message: 'La contraseña es obligatoria.' })
  @MinLength(8, { message: 'La contraseña debe tener al menos 8 caracteres.' })
  @MaxLength(128, { message: 'La contraseña no puede superar 128 caracteres.' })
  password!: string;

  @Transform(trimmed)
  @IsString({ message: 'El nombre para mostrar es obligatorio.' })
  @IsNotEmpty({ message: 'El nombre para mostrar es obligatorio.' })
  @MaxLength(50, { message: 'El nombre para mostrar no puede superar 50 caracteres.' })
  displayName!: string;
}
