import { PrismaPg } from '@prisma/adapter-pg';
import { PrismaClient } from '../../src/generated/prisma/client.js';

/**
 * Cliente con el rol propietario `senas_migrator`, SOLO para preparar y limpiar datos de prueba que
 * `senas_app` no puede escribir (niveles, señas, progreso de jugadores...). Deriva el host y la base de
 * TEST_DATABASE_URL y rechaza cualquier host que no sea local: nunca puede apuntar a una base remota.
 * La aplicación bajo prueba jamás usa este cliente.
 */
export function fixturesClient(): PrismaClient {
  const appUrl = process.env.TEST_DATABASE_URL;
  if (!appUrl) throw new Error('TEST_DATABASE_URL no está definida.');
  const url = new URL(appUrl);
  if (!['127.0.0.1', 'localhost', '::1', '[::1]'].includes(url.hostname)) {
    throw new Error('Los fixtures solo pueden ejecutarse contra una base de datos local.');
  }
  url.username = 'senas_migrator';
  url.password = '';
  return new PrismaClient({ adapter: new PrismaPg({ connectionString: url.toString() }) });
}
