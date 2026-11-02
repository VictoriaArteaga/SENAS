import { randomUUID } from 'node:crypto';
import { FakeDb, TEST_CONFIG, USER_ID } from '../../test/helpers/fake-db';
import { ApiError } from '../common/api-error';
import { LevelsService } from '../levels/levels.service';
import { CreateEvaluationDto } from './dto/create-evaluation.dto';
import { HistoryFilterDto } from './dto/history.dto';
import { EvaluationsService } from './evaluations.service';

function build(db: FakeDb) {
  const levels = new LevelsService(db.dataSource as never);
  return new EvaluationsService(db.dataSource as never, levels, TEST_CONFIG);
}

function attempt(
  extra: Partial<CreateEvaluationDto> = {},
): CreateEvaluationDto {
  return {
    attemptId: randomUUID(),
    signCode: 'A',
    precision: 82,
    stars: 2,
    xp: 20,
    durationMs: 4200,
    feedback: 'good',
    ...extra,
  };
}

async function errorCodeOf(p: Promise<unknown>): Promise<string> {
  try {
    await p;
  } catch (e) {
    return (e as ApiError).code;
  }
  return 'NO_ERROR';
}

describe('EvaluationsService.register', () => {
  describe('passed attempt', () => {
    it('records, awards XP and unlocks the next sign', async () => {
      const db = new FakeDb();
      const { response, created } = await build(db).register(
        USER_ID,
        attempt(),
      );

      expect(created).toBe(true);
      expect(response).toMatchObject({
        signCode: 'A',
        passed: true,
        precision: 82,
        stars: 2,
        awardedXp: 20,
        totalXp: 20,
        bestPrecision: 82,
        bestStars: 2,
        unlockedSign: 'B',
        unlockedLevels: [],
      });
      expect(db.evaluations).toHaveLength(1);
      expect(db.evaluations[0]).toMatchObject({
        levelId: 'level-ALPHABET',
        awardedXp: 20,
      });
      expect(db.progressOf(USER_ID, 'A')).toMatchObject({
        totalAttempts: 1,
        passedAttempts: 1,
        passed: true,
        earnedXp: 20,
      });
    });

    it('a passed retry of an already passed sign announces no unlocks', async () => {
      const db = new FakeDb();
      const svc = build(db);
      await svc.register(USER_ID, attempt());
      const { response } = await svc.register(USER_ID, attempt());
      expect(response.unlockedSign).toBeNull();
      expect(response.awardedXp).toBe(0);
    });

    it('improving from 1 to 3 stars awards only the difference (20)', async () => {
      const db = new FakeDb();
      const svc = build(db);
      const first = await svc.register(
        USER_ID,
        attempt({ precision: 65, stars: 1, xp: 10 }),
      );
      const second = await svc.register(
        USER_ID,
        attempt({ precision: 95, stars: 3, xp: 30 }),
      );

      expect(first.response.awardedXp).toBe(10);
      expect(second.response).toMatchObject({
        awardedXp: 20,
        totalXp: 30,
        bestStars: 3,
        bestPrecision: 95,
      });
      expect(db.progressOf(USER_ID, 'A')?.earnedXp).toBe(30);
    });

    it('repeating without improvement adds no XP and keeps the best precision', async () => {
      const db = new FakeDb();
      const svc = build(db);
      await svc.register(USER_ID, attempt({ precision: 95, stars: 3, xp: 30 }));
      const worse = await svc.register(
        USER_ID,
        attempt({ precision: 70, stars: 1, xp: 10 }),
      );
      expect(worse.response).toMatchObject({
        awardedXp: 0,
        totalXp: 30,
        bestPrecision: 95,
        bestStars: 3,
      });
    });

    it('unlocks a level on crossing the XP threshold (290 + 10 = 300)', async () => {
      const db = new FakeDb(290);
      const { response } = await build(db).register(
        USER_ID,
        attempt({ precision: 65, stars: 1, xp: 10 }),
      );
      expect(response.totalXp).toBe(300);
      expect(response.unlockedLevels).toEqual(['NUMBERS']);
    });

    it('does not announce a level if it stays at 299', async () => {
      const db = new FakeDb(289);
      const { response } = await build(db).register(
        USER_ID,
        attempt({ precision: 65, stars: 1, xp: 10 }),
      );
      expect(response.totalXp).toBe(299);
      expect(response.unlockedLevels).toEqual([]);
    });

    it('only announces the levels crossed by this attempt, not those already open', async () => {
      const db = new FakeDb(690);
      const { response } = await build(db).register(
        USER_ID,
        attempt({ precision: 95, stars: 3, xp: 30 }),
      );
      expect(response.totalXp).toBe(720);
      expect(response.unlockedLevels).toEqual(['MONTHS']);
    });

    it('the last sign of a level unlocks no sign', async () => {
      const db = new FakeDb();
      const previous = 'ABCDEFGHIJKLMN'
        .split('')
        .concat(['Ñ'])
        .concat('OPQRSTUVWXY'.split(''));
      db.pass(USER_ID, ...previous);
      const { response } = await build(db).register(
        USER_ID,
        attempt({ signCode: 'Z' }),
      );
      expect(response.unlockedSign).toBeNull();
    });
  });

  describe('failed attempt', () => {
    it('gives no XP or stars even if the AI reports values, and stores what was reported', async () => {
      const db = new FakeDb();
      const { response } = await build(db).register(
        USER_ID,
        attempt({ precision: 40, stars: 1, xp: 10 }),
      );
      expect(response).toMatchObject({
        passed: false,
        stars: 0,
        awardedXp: 0,
        totalXp: 0,
        unlockedSign: null,
        bestPrecision: 40,
        bestStars: 0,
      });
      expect(db.evaluations[0]).toMatchObject({
        reportedStars: 1,
        reportedXp: 10,
        passed: false,
        awardedXp: 0,
      });
      expect(db.progressOf(USER_ID, 'A')).toMatchObject({
        totalAttempts: 1,
        passedAttempts: 0,
        passed: false,
      });
    });

    it('does not unlock the next sign', async () => {
      const db = new FakeDb();
      const svc = build(db);
      await svc.register(USER_ID, attempt({ precision: 30, stars: 0, xp: 0 }));
      expect(
        await errorCodeOf(svc.register(USER_ID, attempt({ signCode: 'B' }))),
      ).toBe('SIGN_LOCKED');
    });
  });

  describe('rejections', () => {
    it('unknown sign → 404 SIGN_NOT_FOUND', async () => {
      const db = new FakeDb();
      const e = await build(db)
        .register(USER_ID, attempt({ signCode: 'MADE_UP_SIGN' }))
        .catch((x: ApiError) => x);
      expect(e).toBeInstanceOf(ApiError);
      expect((e as ApiError).getStatus()).toBe(404);
      expect((e as ApiError).code).toBe('SIGN_NOT_FOUND');
    });

    it('deactivated sign → 404', async () => {
      const db = new FakeDb();
      db.signs.find((s) => s.code === 'A')!.active = false;
      expect(await errorCodeOf(build(db).register(USER_ID, attempt()))).toBe(
        'SIGN_NOT_FOUND',
      );
    });

    it('level locked by XP → 403 LEVEL_LOCKED', async () => {
      const db = new FakeDb(299);
      const e = await build(db)
        .register(USER_ID, attempt({ signCode: 'numeros.0' }))
        .catch((x: ApiError) => x);
      expect((e as ApiError).getStatus()).toBe(403);
      expect((e as ApiError).code).toBe('LEVEL_LOCKED');
    });

    it('with 300 XP the Numbers level opens (the first sign is available)', async () => {
      const db = new FakeDb(300);
      const { response } = await build(db).register(
        USER_ID,
        attempt({ signCode: 'numeros.0', precision: 65, stars: 1, xp: 10 }),
      );
      expect(response.unlockedSign).toBe('numeros.1');
    });

    it('locked sign inside an open level → 403 SIGN_LOCKED', async () => {
      const db = new FakeDb();
      const e = await build(db)
        .register(USER_ID, attempt({ signCode: 'C' }))
        .catch((x: ApiError) => x);
      expect((e as ApiError).getStatus()).toBe(403);
      expect((e as ApiError).code).toBe('SIGN_LOCKED');
    });

    it('inconsistent result → 422 and nothing is saved', async () => {
      const db = new FakeDb();
      const e = await build(db)
        .register(USER_ID, attempt({ precision: 82, stars: 3, xp: 30 }))
        .catch((x: ApiError) => x);
      expect((e as ApiError).getStatus()).toBe(422);
      expect((e as ApiError).code).toBe('INCONSISTENT_RESULT');
      expect(db.evaluations).toHaveLength(0);
      expect(db.progress).toHaveLength(0);
      expect(db.xp.get(USER_ID)).toBe(0);
    });

    it('inconsistent XP with correct stars → 422', async () => {
      const db = new FakeDb();
      expect(
        await errorCodeOf(
          build(db).register(USER_ID, attempt({ stars: 2, xp: 30 })),
        ),
      ).toBe('INCONSISTENT_RESULT');
    });
  });

  describe('idempotency', () => {
    it('the same attempt and content returns the original response without changes (200)', async () => {
      const db = new FakeDb();
      const svc = build(db);
      const dto = attempt();
      const first = await svc.register(USER_ID, dto);
      const xpAfter = db.xp.get(USER_ID);

      const repeated = await svc.register(USER_ID, { ...dto });
      expect(first.created).toBe(true);
      expect(repeated.created).toBe(false);
      expect(repeated.response).toEqual(first.response);
      expect(db.evaluations).toHaveLength(1);
      expect(db.xp.get(USER_ID)).toBe(xpAfter);
      expect(db.progressOf(USER_ID, 'A')?.totalAttempts).toBe(1);
    });

    it('the repeated response does not change even if progress has advanced', async () => {
      const db = new FakeDb();
      const svc = build(db);
      const dto = attempt({ precision: 65, stars: 1, xp: 10 });
      const first = await svc.register(USER_ID, dto);
      await svc.register(USER_ID, attempt({ precision: 95, stars: 3, xp: 30 }));
      const repeated = await svc.register(USER_ID, { ...dto });
      expect(repeated.response).toEqual(first.response);
    });

    it('the same attemptId with different content → 409 DUPLICATE_ATTEMPT', async () => {
      const db = new FakeDb();
      const svc = build(db);
      const dto = attempt();
      await svc.register(USER_ID, dto);
      const e = await svc
        .register(USER_ID, { ...dto, precision: 90, stars: 3, xp: 30 })
        .catch((x: ApiError) => x);
      expect((e as ApiError).getStatus()).toBe(409);
      expect((e as ApiError).code).toBe('DUPLICATE_ATTEMPT');
      expect(db.evaluations).toHaveLength(1);
    });

    it('changing only the duration, feedback or sign is also different content', async () => {
      const db = new FakeDb();
      const svc = build(db);
      const dto = attempt();
      await svc.register(USER_ID, dto);
      expect(
        await errorCodeOf(svc.register(USER_ID, { ...dto, durationMs: 1 })),
      ).toBe('DUPLICATE_ATTEMPT');
      expect(
        await errorCodeOf(svc.register(USER_ID, { ...dto, feedback: 'other' })),
      ).toBe('DUPLICATE_ATTEMPT');
      expect(
        await errorCodeOf(svc.register(USER_ID, { ...dto, signCode: 'B' })),
      ).toBe('DUPLICATE_ATTEMPT');
    });

    it('the same attemptId from another user is independent', async () => {
      const db = new FakeDb();
      db.xp.set('user-2', 0);
      const svc = build(db);
      const dto = attempt();
      const a = await svc.register(USER_ID, dto);
      const b = await svc.register('user-2', { ...dto });
      expect(a.created).toBe(true);
      expect(b.created).toBe(true);
      expect(db.evaluations).toHaveLength(2);
    });

    it('if two simultaneous submissions collide on the unique index, the loser gets the stored response', async () => {
      const db = new FakeDb();
      const svc = build(db);
      const dto = attempt();
      const winner = await svc.register(USER_ID, dto);

      // Simulates the race: the initial check does not see the attempt, the INSERT fails.
      let calls = 0;
      const original = db.manager.findOne;
      db.manager.findOne = (async (...args: Parameters<typeof original>) => {
        const [entity] = args;
        if (
          entity &&
          (entity as { name: string }).name === 'Evaluation' &&
          calls++ < 2
        )
          return null;
        return original(...args);
      }) as typeof original;

      const loser = await svc.register(USER_ID, { ...dto });
      expect(loser.created).toBe(false);
      expect(loser.response).toEqual(winner.response);
    });

    it('an error other than uniqueness is propagated', async () => {
      const db = new FakeDb();
      db.dataSource.transaction = () => Promise.reject(new Error('boom'));
      await expect(build(db).register(USER_ID, attempt())).rejects.toThrow(
        'boom',
      );
    });
  });

  it('normalizes precision to 2 decimals before evaluating and saving', async () => {
    const db = new FakeDb();
    const { response } = await build(db).register(
      USER_ID,
      attempt({ precision: 59.996, stars: 1, xp: 10 }),
    );
    // 59.996 → 60.00, which already meets the pass threshold.
    expect(response).toMatchObject({ passed: true, precision: 60 });
  });
});

describe('EvaluationsService sign codes', () => {
  it('matches codes case-insensitively and answers with the canonical code', async () => {
    const db = new FakeDb(300);
    const svc = build(db);
    const letter = await svc.register(USER_ID, attempt({ signCode: 'a' }));
    expect(letter.response.signCode).toBe('A');
    const number = await svc.register(
      USER_ID,
      attempt({ signCode: 'NUMEROS.0', precision: 65, stars: 1, xp: 10 }),
    );
    expect(number.response.signCode).toBe('numeros.0');
    expect(number.response.unlockedSign).toBe('numeros.1');
  });

  it('supports the Ñ code in either case', async () => {
    const db = new FakeDb();
    db.pass(USER_ID, ...'ABCDEFGHIJKLMN'.split(''));
    const { response } = await build(db).register(
      USER_ID,
      attempt({ signCode: 'ñ' }),
    );
    expect(response.signCode).toBe('Ñ');
    expect(response.unlockedSign).toBe('O');
  });

  it('an idempotent retry with different letter case is the same attempt', async () => {
    const db = new FakeDb();
    const svc = build(db);
    const dto = attempt({ signCode: 'A' });
    const first = await svc.register(USER_ID, dto);
    const again = await svc.register(USER_ID, { ...dto, signCode: 'a' });
    expect(again.created).toBe(false);
    expect(again.response).toEqual(first.response);
  });
});

describe('EvaluationsService.history', () => {
  function withRepo(rows: unknown[], total: number) {
    const qb = {
      innerJoinAndSelect: jest.fn().mockReturnThis(),
      where: jest.fn().mockReturnThis(),
      andWhere: jest.fn().mockReturnThis(),
      orderBy: jest.fn().mockReturnThis(),
      addOrderBy: jest.fn().mockReturnThis(),
      skip: jest.fn().mockReturnThis(),
      take: jest.fn().mockReturnThis(),
      getManyAndCount: jest.fn().mockResolvedValue([rows, total]),
    };
    const ds = { getRepository: () => ({ createQueryBuilder: () => qb }) };
    const levels = new LevelsService(new FakeDb().dataSource as never);
    return {
      qb,
      svc: new EvaluationsService(ds as never, levels, TEST_CONFIG),
    };
  }

  it('paginates and maps to DTO', async () => {
    const row = {
      id: 'e1',
      attemptId: 'i1',
      sign: { code: 'A' },
      level: { code: 'ALPHABET' },
      precision: 80,
      reportedStars: 2,
      reportedXp: 20,
      passed: true,
      awardedXp: 20,
      durationMs: null,
      feedback: null,
      createdAt: new Date(),
    };
    const { svc, qb } = withRepo([row], 41);
    const filter = Object.assign(new HistoryFilterDto(), {
      page: 3,
      limit: 10,
    });
    const r = await svc.history(USER_ID, filter);

    expect(qb.skip).toHaveBeenCalledWith(20);
    expect(qb.take).toHaveBeenCalledWith(10);
    expect(r).toMatchObject({ total: 41, page: 3, limit: 10 });
    expect(r.items[0]).toMatchObject({
      signCode: 'A',
      levelCode: 'ALPHABET',
      passed: true,
    });
  });

  it('applies each optional filter', async () => {
    const { svc, qb } = withRepo([], 0);
    const filter = Object.assign(new HistoryFilterDto(), {
      sign: 'a',
      level: 'alphabet',
      passed: false,
      from: '2026-11-01T00:00:00Z',
      to: '2026-11-30T00:00:00Z',
    });
    await svc.history(USER_ID, filter);
    // 1 base where + 5 filters.
    expect(qb.andWhere).toHaveBeenCalledTimes(5);
    expect(qb.andWhere).toHaveBeenCalledWith('lower(s.code) = lower(:sign)', {
      sign: 'a',
    });
    expect(qb.andWhere).toHaveBeenCalledWith('e.passed = :passed', {
      passed: false,
    });
  });
});
