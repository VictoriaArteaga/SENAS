import { BadRequestException, ValidationPipe } from '@nestjs/common';
import { LoginDto } from './login.dto';
import { RegisterDto } from './register.dto';

// Misma configuración que AppModule.
const pipe = new ValidationPipe({ whitelist: true, forbidNonWhitelisted: true, transform: true });

async function run<T>(metatype: new () => T, body: unknown): Promise<T> {
  return pipe.transform(body, { type: 'body', metatype });
}
async function messages(metatype: new () => unknown, body: unknown): Promise<string[]> {
  try {
    await run(metatype, body);
  } catch (e) {
    expect(e).toBeInstanceOf(BadRequestException);
    const msg = (e as BadRequestException).getResponse() as { message: string[] };
    return msg.message;
  }
  throw new Error('Se esperaba un 400 y la validación pasó');
}

const validRegister = {
  username: 'Maria_01',
  email: '  Maria@Example.COM ',
  password: 'contraseña-larga',
  displayName: '  María  ',
};

describe('RegisterDto', () => {
  it('normaliza username y email (minúsculas, sin espacios) y recorta displayName', async () => {
    const dto = await run(RegisterDto, validRegister);
    expect(dto).toMatchObject({ username: 'maria_01', email: 'maria@example.com', displayName: 'María' });
    expect(dto.password).toBe('contraseña-larga'); // la contraseña no se modifica
  });

  it('rechaza email inválido', async () => {
    expect(await messages(RegisterDto, { ...validRegister, email: 'no-es-correo' })).toContain('El correo electrónico no es válido.');
  });

  it('rechaza contraseña corta (menos de 8) y demasiado larga', async () => {
    expect(await messages(RegisterDto, { ...validRegister, password: '1234567' })).toContain('La contraseña debe tener al menos 8 caracteres.');
    expect((await run(RegisterDto, { ...validRegister, password: '12345678' })).password).toBe('12345678');
    expect(await messages(RegisterDto, { ...validRegister, password: 'x'.repeat(129) })).toContain('La contraseña no puede superar 128 caracteres.');
  });

  it.each([
    ['username', ''],
    ['username', 'ab'],
    ['username', 'con espacio'],
    ['username', 'a'.repeat(31)],
    ['username', 'raro;--'],
    ['email', ''],
    ['password', ''],
    ['displayName', ''],
    ['displayName', '    '],
    ['displayName', 'x'.repeat(51)],
  ])('rechaza %s inválido (%j)', async (field, value) => {
    expect((await messages(RegisterDto, { ...validRegister, [field]: value })).length).toBeGreaterThan(0);
  });

  it.each(['username', 'email', 'password', 'displayName'])('exige %s', async (field) => {
    const { [field]: _omit, ...rest } = validRegister as Record<string, string>;
    expect((await messages(RegisterDto, rest)).length).toBeGreaterThan(0);
  });

  it.each([123, null, {}, ['a']])('rechaza tipos no string en password (%j)', async (value) => {
    expect((await messages(RegisterDto, { ...validRegister, password: value })).length).toBeGreaterThan(0);
  });

  it('rechaza campos adicionales (asignación masiva)', async () => {
    for (const extra of [{ isActive: false }, { id: 'x' }, { passwordHash: 'x' }, { totalXp: 9999 }, { role: 'admin' }]) {
      const msgs = await messages(RegisterDto, { ...validRegister, ...extra });
      expect(msgs.join(' ')).toMatch(/should not exist/);
    }
  });
});

describe('LoginDto', () => {
  const validLogin = { email: ' Maria@Example.com', password: 'x' };

  it('normaliza el email y acepta informacionDispositivo opcional', async () => {
    const dto = await run(LoginDto, { ...validLogin, informacionDispositivo: { plataforma: 'Windows', versionApp: '1.0.0' } });
    expect(dto.email).toBe('maria@example.com');
    expect(dto.informacionDispositivo).toMatchObject({ plataforma: 'Windows', versionApp: '1.0.0' });
    expect((await run(LoginDto, validLogin)).informacionDispositivo).toBeUndefined();
  });

  it('rechaza campos adicionales, también dentro de informacionDispositivo', async () => {
    expect((await messages(LoginDto, { ...validLogin, isActive: true })).join(' ')).toMatch(/should not exist/);
    const nested = await messages(LoginDto, { ...validLogin, informacionDispositivo: { plataforma: 'x', ip: '1.2.3.4' } });
    expect(nested.join(' ')).toMatch(/should not exist/);
  });

  it('rechaza informacionDispositivo que no sea un objeto o con valores enormes', async () => {
    for (const bad of ['texto', 5, ['a']]) {
      expect((await messages(LoginDto, { ...validLogin, informacionDispositivo: bad })).length).toBeGreaterThan(0);
    }
    expect((await messages(LoginDto, { ...validLogin, informacionDispositivo: { modelo: 'x'.repeat(65) } })).length).toBeGreaterThan(0);
  });

  it('rechaza email inválido y contraseña vacía', async () => {
    expect((await messages(LoginDto, { email: 'x', password: 'y' })).length).toBeGreaterThan(0);
    expect((await messages(LoginDto, { email: 'a@b.co', password: '' })).length).toBeGreaterThan(0);
  });

  it('los mensajes de validación no repiten la contraseña enviada', async () => {
    const secreto = 'mi-clave-que-no-debe-aparecer';
    const msgs = await messages(RegisterDto, { ...validRegister, email: 'malo', password: secreto, extra: secreto });
    expect(JSON.stringify(msgs)).not.toContain(secreto);
  });
});
