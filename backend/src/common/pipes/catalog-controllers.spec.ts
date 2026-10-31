import { BadRequestException, RequestMethod } from '@nestjs/common';
import { GUARDS_METADATA, METHOD_METADATA, PATH_METADATA, ROUTE_ARGS_METADATA } from '@nestjs/common/constants';
import { randomUUID } from 'node:crypto';
import { JwtAuthGuard } from '../../auth/guards/jwt-auth.guard';
import { LevelsController } from '../../levels/levels.controller';
import { LevelSenasController, SenasController } from '../../senas/senas.controller';
import type { SenasService } from '../../senas/senas.service';
import { parseUuid } from './parse-uuid.pipe';

describe('parseUuid', () => {
  it('acepta UUID v4 y v7 (el que genera Prisma) y devuelve el valor en minúsculas', () => {
    const v7 = '01a19052-a49e-71f5-82bb-6f5da232c9f5';
    for (const id of [randomUUID(), v7]) expect(parseUuid.transform(id)).toBe(id);
    expect(parseUuid.transform(v7.toUpperCase())).toBe(v7);
  });

  it.each([
    '00000000-0000-0000-0000-000000000000', // UUID nulo
    '00000000-0000-0000-0000-0000000000b1', // ids fijos de un seed escrito a mano
    '11111111-1111-1111-1111-111111111111',
    'ffffffff-ffff-ffff-ffff-ffffffffffff',
  ])('acepta el UUID de formato canónico %s aunque no sea RFC (PostgreSQL lo admite)', (id) => {
    expect(parseUuid.transform(id)).toBe(id);
  });

  it.each([
    '', 'abc', 'no-es-un-uuid', "1' OR '1'='1", '123', '../../etc/passwd', 'null', 'undefined',
    `${randomUUID()}x`, ` ${randomUUID()}`, `${randomUUID()}\n`,
    randomUUID().replace(/-/g, ''), // sin guiones
    `{${randomUUID()}}`, `urn:uuid:${randomUUID()}`, // otras notaciones que PostgreSQL aceptaría
    'gggggggg-gggg-gggg-gggg-gggggggggggg',
  ])('rechaza %j con 400 y mensaje en español', (bad) => {
    let err: unknown;
    try {
      parseUuid.transform(bad);
    } catch (e) {
      err = e;
    }
    expect(err).toBeInstanceOf(BadRequestException);
    expect((err as BadRequestException).getResponse()).toMatchObject({ statusCode: 400, message: 'El identificador no es un UUID válido.' });
  });

  it('rechaza valores que no son texto', () => {
    for (const bad of [undefined, null, 5, {}, [randomUUID()]]) {
      expect(() => parseUuid.transform(bad)).toThrow(BadRequestException);
    }
  });
});

describe('controladores del catálogo', () => {
  const controllers = [LevelsController, LevelSenasController, SenasController];

  it.each(controllers)('%p está protegido con JwtAuthGuard a nivel de controlador', (controller) => {
    expect(Reflect.getMetadata(GUARDS_METADATA, controller)).toContain(JwtAuthGuard);
  });

  it('todas las rutas son de solo lectura (GET): no hay endpoints de escritura', () => {
    const handlers: Array<[string, unknown]> = [];
    for (const controller of controllers) {
      for (const name of Object.getOwnPropertyNames(controller.prototype).filter((n) => n !== 'constructor')) {
        handlers.push([`${controller.name}.${name}`, (controller.prototype as never)[name]]);
      }
    }
    expect(handlers.map(([n]) => n).sort()).toEqual(['LevelSenasController.findByLevel', 'LevelsController.findAll', 'LevelsController.findOne', 'SenasController.findOne']);
    for (const [, handler] of handlers) {
      expect(Reflect.getMetadata(METHOD_METADATA, handler as object)).toBe(RequestMethod.GET);
    }
  });

  it('rutas expuestas', () => {
    const routes = controllers.flatMap((c) =>
      Object.getOwnPropertyNames(c.prototype)
        .filter((n) => n !== 'constructor')
        .map((n) => `${Reflect.getMetadata(PATH_METADATA, c)}/${Reflect.getMetadata(PATH_METADATA, (c.prototype as never)[n]) || ''}`.replace(/\/+$/, '')),
    );
    expect(routes.sort()).toEqual(['levels', 'levels/:id', 'levels/:id/senas', 'senas/:id']);
  });

  it('cada handler recibe exactamente los argumentos previstos: @Param id y/o el usuario del JWT (nada de @Body, @Query ni @Headers)', () => {
    const PARAM = 5; // RouteParamtypes.PARAM
    const kindsOf = (controller: object, name: string) => {
      const args = Reflect.getMetadata(ROUTE_ARGS_METADATA, controller, name) ?? {};
      return Object.keys(args)
        .map((key) => {
          if (key.includes('__customRouteArgs__')) return 'usuario-del-jwt';
          expect(Number(key.split(':')[0])).toBe(PARAM); // cualquier otro tipo (BODY, QUERY, HEADERS...) es un fallo
          expect(args[key].data).toBe('id');
          return 'param:id';
        })
        .sort();
    };
    expect({
      'LevelsController.findAll': kindsOf(LevelsController, 'findAll'),
      'LevelsController.findOne': kindsOf(LevelsController, 'findOne'),
      'LevelSenasController.findByLevel': kindsOf(LevelSenasController, 'findByLevel'),
      'SenasController.findOne': kindsOf(SenasController, 'findOne'),
    }).toEqual({
      'LevelsController.findAll': ['usuario-del-jwt'],
      'LevelsController.findOne': ['param:id'],
      'LevelSenasController.findByLevel': ['param:id', 'usuario-del-jwt'],
      'SenasController.findOne': ['param:id', 'usuario-del-jwt'],
    });
  });
});

describe('controladores de señas: delegan en el servicio con la identidad del JWT', () => {
  const user = { userId: '018f0000-0000-7000-8000-0000000000aa', email: 'x@example.com' };

  it('GET /levels/:id/senas usa user.userId (del JWT) y el id de la ruta', async () => {
    const senas = { findByLevelForPlayer: jest.fn().mockResolvedValue([{ id: 's' }]) };
    const controller = new LevelSenasController(senas as unknown as SenasService);
    await expect(controller.findByLevel('nivel-id', user)).resolves.toEqual({ data: [{ id: 's' }] });
    expect(senas.findByLevelForPlayer).toHaveBeenCalledWith(user.userId, 'nivel-id');
  });

  it('GET /senas/:id usa user.userId (del JWT) y el id de la ruta', async () => {
    const senas = { findOneForPlayer: jest.fn().mockResolvedValue({ id: 's' }) };
    const controller = new SenasController(senas as unknown as SenasService);
    await expect(controller.findOne('sena-id', user)).resolves.toEqual({ data: { id: 's' } });
    expect(senas.findOneForPlayer).toHaveBeenCalledWith(user.userId, 'sena-id');
  });

  it('los errores del servicio (403/404) se propagan sin transformarse', async () => {
    const boom = new Error('403');
    const controller = new LevelSenasController({ findByLevelForPlayer: jest.fn().mockRejectedValue(boom) } as unknown as SenasService);
    await expect(controller.findByLevel('x', user)).rejects.toBe(boom);
  });
});
