import { TEST_CONFIG } from '../../test/helpers/fake-db';
import {
  applyAttempt,
  computeAwardedXp,
  maxXpPerSign,
  normalizePrecision,
  ProgressState,
  verifyScore,
} from './scoring';

const rules = TEST_CONFIG.scoring;

describe('verifyScore', () => {
  it.each([
    // precision, expected stars, expected xp, passed
    [59.99, 0, 0, false],
    [60, 1, 10, true],
    [73.99, 1, 10, true],
    [74, 2, 20, true],
    [87.99, 2, 20, true],
    [88, 3, 30, true],
    [100, 3, 30, true],
    [0, 0, 0, false],
  ])(
    'precision %f → %i stars, %i XP, passed=%s',
    (precision, stars, xp, passed) => {
      const v = verifyScore(precision, stars, xp, rules);
      expect(v.passed).toBe(passed);
      expect(v.expectedStars).toBe(stars);
      expect(v.expectedXp).toBe(xp);
      expect(v.consistent).toBe(true);
    },
  );

  it('detects inconsistent stars on a passed attempt', () => {
    const v = verifyScore(82, 3, 20, rules);
    expect(v.passed).toBe(true);
    expect(v.consistent).toBe(false);
  });

  it('detects inconsistent XP on a passed attempt', () => {
    expect(verifyScore(82, 2, 30, rules).consistent).toBe(false);
    expect(verifyScore(60, 1, 0, rules).consistent).toBe(false);
  });

  it('does not validate what was reported when precision does not pass (the numbers recognizer gives 1 star)', () => {
    const v = verifyScore(40, 1, 10, rules);
    expect(v.passed).toBe(false);
    expect(v.consistent).toBe(true);
  });

  it('respects configurable thresholds and XP per star', () => {
    const other = {
      passThreshold: 50,
      twoStars: 70,
      threeStars: 90,
      xpPerStar: 5,
    };
    expect(verifyScore(70, 2, 10, other)).toMatchObject({
      passed: true,
      consistent: true,
    });
    expect(verifyScore(88, 3, 15, other).consistent).toBe(false);
  });
});

describe('normalizePrecision', () => {
  it('rounds to 2 decimals like numeric(5,2)', () => {
    expect(normalizePrecision(59.996)).toBe(60);
    expect(normalizePrecision(12.344)).toBe(12.34);
    expect(normalizePrecision(73.99)).toBe(73.99);
    expect(normalizePrecision(87.99)).toBe(87.99);
    expect(normalizePrecision(100)).toBe(100);
  });
});

describe('XP without duplication', () => {
  const max = maxXpPerSign(rules);

  it('the cap per sign is 3 × XP_PER_STAR', () => {
    expect(max).toBe(30);
  });

  it('first pass: awards all the reported XP', () => {
    expect(computeAwardedXp(true, 10, 0, max)).toBe(10);
    expect(computeAwardedXp(true, 30, 0, max)).toBe(30);
  });

  it('improving from 1 to 3 stars: awards only the difference (20)', () => {
    expect(computeAwardedXp(true, 30, 10, max)).toBe(20);
  });

  it('improving 1→2 and then 2→3 stars adds exactly 20 in total', () => {
    const a = computeAwardedXp(true, 20, 10, max);
    const b = computeAwardedXp(true, 30, 10 + a, max);
    expect(a + b).toBe(20);
  });

  it('repeating without improvement or worse: 0', () => {
    expect(computeAwardedXp(true, 20, 20, max)).toBe(0);
    expect(computeAwardedXp(true, 10, 30, max)).toBe(0);
  });

  it('a failed attempt: 0 even if XP is reported', () => {
    expect(computeAwardedXp(false, 30, 0, max)).toBe(0);
  });

  it('never exceeds the cap of 30', () => {
    expect(computeAwardedXp(true, 999, 0, max)).toBe(30);
    expect(computeAwardedXp(true, 999, 25, max)).toBe(5);
  });
});

describe('applyAttempt', () => {
  const empty: ProgressState = {
    totalAttempts: 0,
    passedAttempts: 0,
    bestPrecision: null,
    bestStars: 0,
    earnedXp: 0,
    passed: false,
    firstPassedAt: null,
    lastAttemptAt: null,
  };
  const t1 = new Date('2026-11-01T10:00:00Z');
  const t2 = new Date('2026-11-01T11:00:00Z');

  it('a failed attempt counts the attempt and best precision, but not stars or XP', () => {
    const r = applyAttempt(
      empty,
      { precision: 50, passed: false, stars: 1, awardedXp: 0 },
      t1,
    );
    expect(r).toMatchObject({
      totalAttempts: 1,
      passedAttempts: 0,
      bestPrecision: 50,
      bestStars: 0,
      earnedXp: 0,
      passed: false,
      firstPassedAt: null,
      lastAttemptAt: t1,
    });
  });

  it('the first pass sets firstPassedAt and does not move it afterwards', () => {
    const a = applyAttempt(
      empty,
      { precision: 80, passed: true, stars: 2, awardedXp: 20 },
      t1,
    );
    const b = applyAttempt(
      a,
      { precision: 90, passed: true, stars: 3, awardedXp: 10 },
      t2,
    );
    expect(a.firstPassedAt).toBe(t1);
    expect(b.firstPassedAt).toBe(t1);
    expect(b).toMatchObject({
      totalAttempts: 2,
      passedAttempts: 2,
      bestPrecision: 90,
      bestStars: 3,
      earnedXp: 30,
      passed: true,
      lastAttemptAt: t2,
    });
  });

  it('a worse later attempt does not lower best precision or stars', () => {
    const a = applyAttempt(
      empty,
      { precision: 90, passed: true, stars: 3, awardedXp: 30 },
      t1,
    );
    const b = applyAttempt(
      a,
      { precision: 30, passed: false, stars: 1, awardedXp: 0 },
      t2,
    );
    expect(b.bestPrecision).toBe(90);
    expect(b.bestStars).toBe(3);
    expect(b.passed).toBe(true);
  });

  it('does not mutate the original progress', () => {
    const copy = { ...empty };
    applyAttempt(
      empty,
      { precision: 90, passed: true, stars: 3, awardedXp: 30 },
      t1,
    );
    expect(empty).toEqual(copy);
  });
});
