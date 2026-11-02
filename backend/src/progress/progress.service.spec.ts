import { FakeDb, USER_ID } from '../../test/helpers/fake-db';
import { ApiError } from '../common/api-error';
import { LevelsService } from '../levels/levels.service';
import { SignStatus } from '../levels/unlocks';
import { ProgressService } from './progress.service';

const build = (db: FakeDb) =>
  new ProgressService(new LevelsService(db.dataSource as never));

describe('ProgressService.summary', () => {
  it('new player: everything at zero, level 1 unlocked and Numbers as next', async () => {
    const r = await build(new FakeDb()).summary(USER_ID);
    expect(r).toEqual({
      totalXp: 0,
      highestUnlockedLevel: {
        code: 'ALPHABET',
        name: 'Alphabet',
        requiredXp: 0,
      },
      nextLevel: {
        code: 'NUMBERS',
        name: 'Numbers',
        requiredXp: 300,
        missingXp: 300,
      },
      passedSigns: 0,
      totalSigns: 81,
      totalStars: 0,
      totalAttempts: 0,
      passRate: 0,
    });
  });

  it('adds up stars and attempts and computes the pass rate', async () => {
    const db = new FakeDb(310);
    db.pass(USER_ID, 'A', 'B');
    db.progressOf(USER_ID, 'A')!.totalAttempts = 3;
    db.progressOf(USER_ID, 'A')!.passedAttempts = 1;
    db.progressOf(USER_ID, 'B')!.bestStars = 3;
    const r = await build(db).summary(USER_ID);

    expect(r.highestUnlockedLevel?.code).toBe('NUMBERS');
    expect(r.nextLevel).toMatchObject({ code: 'MONTHS', missingXp: 390 });
    expect(r.passedSigns).toBe(2);
    expect(r.totalStars).toBe(4);
    expect(r.totalAttempts).toBe(4);
    expect(r.passRate).toBe(0.5);
  });

  it('with every level unlocked there is no next level', async () => {
    const r = await build(new FakeDb(1200)).summary(USER_ID);
    expect(r.highestUnlockedLevel?.code).toBe('GREETINGS_PHRASES');
    expect(r.nextLevel).toBeNull();
  });

  it('does not count progress on signs that are no longer in the active catalog', async () => {
    const db = new FakeDb();
    db.pass(USER_ID, 'A');
    db.signs.find((s) => s.code === 'A')!.active = false;
    db.levelSigns = db.levelSigns.filter((ls) => ls.signId !== 'sign-A');
    const r = await build(db).summary(USER_ID);
    expect(r.passedSigns).toBe(0);
    expect(r.totalSigns).toBe(80);
  });
});

describe('ProgressService.bySign', () => {
  it('sign with no attempts: the first AVAILABLE, the others LOCKED', async () => {
    const svc = build(new FakeDb());
    expect((await svc.bySign(USER_ID, 'A')).status).toBe(SignStatus.AVAILABLE);
    const b = await svc.bySign(USER_ID, 'b');
    expect(b).toMatchObject({
      signCode: 'B',
      status: SignStatus.LOCKED,
      totalAttempts: 0,
      bestPrecision: null,
      bestStars: 0,
      earnedXp: 0,
      passed: false,
    });
  });

  it('passed sign: returns the stored detail', async () => {
    const db = new FakeDb();
    db.pass(USER_ID, 'A');
    const r = await build(db).bySign(USER_ID, 'A');
    expect(r).toMatchObject({
      status: SignStatus.PASSED,
      passed: true,
      passedAttempts: 1,
      bestPrecision: 65,
      earnedXp: 10,
    });
  });

  it('sign in a level locked by XP → LOCKED', async () => {
    const r = await build(new FakeDb(299)).bySign(USER_ID, 'numeros.0');
    expect(r.status).toBe(SignStatus.LOCKED);
  });

  it('unknown sign → 404 SIGN_NOT_FOUND', async () => {
    const e = await build(new FakeDb())
      .bySign(USER_ID, 'DOES_NOT_EXIST')
      .catch((x: ApiError) => x);
    expect((e as ApiError).getStatus()).toBe(404);
    expect((e as ApiError).code).toBe('SIGN_NOT_FOUND');
  });
});
