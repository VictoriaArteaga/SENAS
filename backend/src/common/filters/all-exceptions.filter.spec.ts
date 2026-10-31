import { ArgumentsHost, ConflictException } from '@nestjs/common';
import { HttpAdapterHost } from '@nestjs/core';
import { AllExceptionsFilter } from './all-exceptions.filter';

describe('AllExceptionsFilter', () => {
  const reply = jest.fn();
  const filter = new AllExceptionsFilter({ httpAdapter: { reply } } as unknown as HttpAdapterHost);
  const host = { switchToHttp: () => ({ getResponse: () => 'res' }) } as unknown as ArgumentsHost;
  let output: string[];

  beforeEach(() => {
    reply.mockClear();
    output = [];
    const capture = (chunk: unknown) => (output.push(String(chunk)), true);
    jest.spyOn(process.stdout, 'write').mockImplementation(capture as never);
    jest.spyOn(process.stderr, 'write').mockImplementation(capture as never);
  });
  afterEach(() => jest.restoreAllMocks());

  it('las excepciones HTTP se devuelven con su estado', () => {
    filter.catch(new ConflictException('duplicado'), host);
    expect(reply).toHaveBeenCalledWith('res', expect.objectContaining({ statusCode: 409, message: 'duplicado' }), 409);
  });

  it('un error interno devuelve 500 genérico y no registra el mensaje original', () => {
    const err = Object.assign(new Error('Invalid prisma.usuario.create() invocation: passwordHash: "$argon2id$secreto" email: "a@b.co"'), { code: 'P2010' });
    err.name = 'PrismaClientKnownRequestError';
    filter.catch(err, host);

    expect(reply).toHaveBeenCalledWith('res', { statusCode: 500, message: 'Error interno del servidor.' }, 500);
    const logs = output.join('');
    expect(logs).toContain('PrismaClientKnownRequestError (P2010)');
    expect(logs).not.toMatch(/argon2id|secreto|a@b\.co|passwordHash/);
    expect(JSON.stringify(reply.mock.calls)).not.toMatch(/argon2id|secreto|a@b\.co/);
  });
});
