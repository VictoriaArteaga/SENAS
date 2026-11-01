import { Injectable } from '@nestjs/common';
import { argon2id, hash, verify } from 'argon2';
import { randomBytes } from 'node:crypto';

// Parámetros mínimos recomendados por OWASP para Argon2id: 19 MiB, 2 iteraciones, 1 hilo.
const OPTIONS = { type: argon2id, memoryCost: 19456, timeCost: 2, parallelism: 1 } as const;

@Injectable()
export class PasswordService {
  /** Hash de una contraseña aleatoria: iguala el tiempo de login y no delata qué correos existen. */
  private dummyHash?: Promise<string>;

  /** Devuelve un hash PHC (formato $argon2id$...) con sal aleatoria propia. */
  hash(plain: string): Promise<string> {
    return hash(plain, OPTIONS);
  }

  async verify(passwordHash: string, plain: string): Promise<boolean> {
    try {
      return await verify(passwordHash, plain);
    } catch {
      return false; // hash corrupto o formato desconocido: nunca autentica
    }
  }

  /** Gasta el mismo trabajo que una verificación real. Siempre devuelve false. */
  async verifyAgainstDummy(plain: string): Promise<false> {
    this.dummyHash ??= this.hash(randomBytes(16).toString('hex'));
    await this.verify(await this.dummyHash, plain);
    return false;
  }
}
