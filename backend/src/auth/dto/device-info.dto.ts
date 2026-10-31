import { IsOptional, IsString, MaxLength } from 'class-validator';

/**
 * Información no sensible del dispositivo, guardada en sesiones.informacion_dispositivo.
 * Lista cerrada de campos (forbidNonWhitelisted rechaza el resto) para que el cliente
 * no pueda almacenar datos arbitrarios ni de gran tamaño.
 */
export class DeviceInfoDto {
  @IsOptional() @IsString() @MaxLength(64)
  plataforma?: string;

  @IsOptional() @IsString() @MaxLength(64)
  modelo?: string;

  @IsOptional() @IsString() @MaxLength(64)
  sistemaOperativo?: string;

  @IsOptional() @IsString() @MaxLength(32)
  versionApp?: string;
}
