import { PasswordService } from './password.service';

describe('PasswordService (Argon2id)', () => {
  const service = new PasswordService();

  it('genera un hash PHC argon2id con sal propia y sin la contraseña en claro', async () => {
    const plain = 'MiContraseña-Segura-123';
    const [a, b] = [await service.hash(plain), await service.hash(plain)];
    expect(a).toMatch(/^\$argon2id\$v=19\$m=19456,p=1,t=2\$/);
    expect(a).not.toBe(b); // sal distinta por hash
    expect(a).not.toContain(plain);
  });

  it('verifica la contraseña correcta y rechaza la incorrecta', async () => {
    const hash = await service.hash('correcta-123');
    await expect(service.verify(hash, 'correcta-123')).resolves.toBe(true);
    await expect(service.verify(hash, 'incorrecta-123')).resolves.toBe(false);
    await expect(service.verify(hash, '')).resolves.toBe(false);
  });

  it('un hash corrupto nunca autentica (no lanza)', async () => {
    await expect(service.verify('esto-no-es-un-hash', 'x')).resolves.toBe(false);
    await expect(service.verify('', 'x')).resolves.toBe(false);
  });

  it('verifyAgainstDummy siempre devuelve false', async () => {
    await expect(service.verifyAgainstDummy('lo-que-sea')).resolves.toBe(false);
  });
});
