import { BadRequestException, Injectable, PipeTransform } from '@nestjs/common';

// 8-4-4-4-12 dígitos hexadecimales. No se exige versión ni variante RFC: PostgreSQL acepta cualquier
// UUID de este formato como clave y un seed con ids fijos escritos a mano es habitual en un catálogo.
const CANONICAL_UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

/**
 * Valida que un parámetro de ruta sea un UUID de formato canónico y lo devuelve en minúsculas.
 * Un identificador inválido responde 400 con un mensaje en español y nunca llega a la base de datos,
 * donde provocaría un error de conversión de tipos.
 */
@Injectable()
export class ParseUuidPipe implements PipeTransform<unknown, string> {
  transform(value: unknown): string {
    if (typeof value !== 'string' || !CANONICAL_UUID.test(value)) {
      throw new BadRequestException('El identificador no es un UUID válido.');
    }
    return value.toLowerCase();
  }
}

export const parseUuid = new ParseUuidPipe();
