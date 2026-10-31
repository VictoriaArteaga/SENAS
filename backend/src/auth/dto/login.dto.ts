import { Transform, Type } from 'class-transformer';
import { IsEmail, IsNotEmpty, IsObject, IsOptional, IsString, MaxLength, ValidateNested } from 'class-validator';
import { normalizeEmail } from '../../common/normalize';
import { DeviceInfoDto } from './device-info.dto';

export class LoginDto {
  @Transform(({ value }) => (typeof value === 'string' ? normalizeEmail(value) : value))
  @IsEmail({}, { message: 'El correo electrónico no es válido.' })
  @MaxLength(254)
  email!: string;

  // Sin longitud mínima en login: no se revela la política de contraseñas.
  @IsString({ message: 'La contraseña es obligatoria.' })
  @IsNotEmpty({ message: 'La contraseña es obligatoria.' })
  @MaxLength(128)
  password!: string;

  @IsOptional()
  @IsObject({ message: 'informacionDispositivo debe ser un objeto.' })
  @ValidateNested()
  @Type(() => DeviceInfoDto)
  informacionDispositivo?: DeviceInfoDto;
}
