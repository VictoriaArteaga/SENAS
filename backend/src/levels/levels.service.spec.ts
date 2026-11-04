import { FakeDb, USER_ID } from '../../test/helpers/fake-db';
import { ApiError } from '../common/api-error';
import { LevelsService } from './levels.service';
import { SignStatus } from './unlocks';

const build = (db: FakeDb) => new LevelsService(db.dataSource as never);

describe('LevelsService', () => {
  it('loadCatalog returns the 4 levels in order with 27, 31, 12 and 11 signs', async () => {
    const catalog = await build(new FakeDb()).loadCatalog();
    expect(catalog.map((l) => [l.code, l.signs.length])).toEqual([
      ['ALPHABET', 27],
      ['NUMBERS', 31],
      ['MONTHS', 12],
      ['GREETINGS_PHRASES', 11],
    ]);
    expect(catalog[0].signs[14].code).toBe('Ñ');
    expect(catalog[3].signs.map((s) => s.code)).toEqual([
      'PHRASE_GOOD_MORNING',
      'PHRASE_GOOD_AFTERNOON',
      'PHRASE_GOOD_EVENING',
      'WORD_HELLO',
      'WORD_GOODBYE',
      'WORD_YES',
      'WORD_NO',
      'PHRASE_HOW_ARE_YOU',
      'WORD_THANK_YOU',
      'PHRASE_FINE_THANK_YOU',
      'PHRASE_YOU_ARE_WELCOME',
    ]);
  });

  it('list: state of each level for a player with 120 XP', async () => {
    const db = new FakeDb(120);
    db.pass(USER_ID, 'A', 'B');
    db.progressOf(USER_ID, 'B')!.bestStars = 3;
    const levels = await build(db).list(USER_ID);

    expect(levels).toHaveLength(4);
    expect(levels[0]).toMatchObject({
      code: 'ALPHABET',
      unlocked: true,
      missingXp: 0,
      passedSigns: 2,
      totalSigns: 27,
      earnedStars: 4,
      possibleStars: 81,
    });
    expect(levels[1]).toMatchObject({
      code: 'NUMBERS',
      unlocked: false,
      requiredXp: 270,
      missingXp: 150,
      passedSigns: 0,
      totalSigns: 31,
      possibleStars: 93,
    });
  });

  it('detail: signs in order with status, best precision and stars', async () => {
    const db = new FakeDb();
    db.pass(USER_ID, 'A');
    const level = await build(db).detail(USER_ID, 'alphabet');

    expect(level.signs).toHaveLength(27);
    expect(level.signs[0]).toMatchObject({
      code: 'A',
      position: 1,
      status: SignStatus.PASSED,
      bestPrecision: 65,
      bestStars: 1,
    });
    expect(level.signs[1]).toMatchObject({
      code: 'B',
      status: SignStatus.AVAILABLE,
      bestPrecision: null,
    });
    expect(level.signs[2].status).toBe(SignStatus.LOCKED);
  });

  it('detail of a locked level: every sign LOCKED', async () => {
    const level = await build(new FakeDb(0)).detail(USER_ID, 'MONTHS');
    expect(level.unlocked).toBe(false);
    expect(level.signs.every((s) => s.status === SignStatus.LOCKED)).toBe(true);
  });

  it('detail of an unknown level → 404 LEVEL_NOT_FOUND', async () => {
    const e = await build(new FakeDb())
      .detail(USER_ID, 'DOES_NOT_EXIST')
      .catch((x: ApiError) => x);
    expect((e as ApiError).getStatus()).toBe(404);
    expect((e as ApiError).code).toBe('LEVEL_NOT_FOUND');
  });

  it('a player without a profile is treated as 0 XP', async () => {
    const db = new FakeDb();
    db.xp.clear();
    expect(await build(db).loadTotalXp('nobody')).toBe(0);
  });
});
