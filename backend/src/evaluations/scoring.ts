import { ScoringRules } from '../config/configuration';

export interface ScoreVerification {
  /** Decided by the backend by comparing precision with the pass threshold. */
  passed: boolean;
  /** Stars per the official table (0 if not passed). */
  expectedStars: number;
  /** XP per the official table (0 if not passed). */
  expectedXp: number;
  /** false if the attempt passed but what was reported does not match the table. */
  consistent: boolean;
}

/** Precision is on a 0–100 scale and stored with 2 decimals (numeric(5,2)); it is rounded before evaluating. */
export function normalizePrecision(precision: number): number {
  return Math.round(precision * 100) / 100;
}

/**
 * Verifies (does not recompute) what the AI reports against the official
 * table. Below the pass threshold the reported stars/XP are not validated: they
 * are kept as received for auditing.
 */
export function verifyScore(
  precision: number,
  reportedStars: number,
  reportedXp: number,
  rules: ScoringRules,
): ScoreVerification {
  if (precision < rules.passThreshold) {
    return { passed: false, expectedStars: 0, expectedXp: 0, consistent: true };
  }
  const expectedStars =
    precision >= rules.threeStars ? 3 : precision >= rules.twoStars ? 2 : 1;
  const expectedXp = expectedStars * rules.xpPerStar;
  return {
    passed: true,
    expectedStars,
    expectedXp,
    consistent: reportedStars === expectedStars && reportedXp === expectedXp,
  };
}

/** Maximum XP obtainable on one sign (3 stars). */
export function maxXpPerSign(rules: ScoringRules): number {
  return 3 * rules.xpPerStar;
}

/** Only the improvement over what was already earned is added; a failed attempt gives no XP. */
export function computeAwardedXp(
  passed: boolean,
  reportedXp: number,
  alreadyEarnedXp: number,
  maxXp: number,
): number {
  if (!passed) return 0;
  return Math.max(0, Math.min(reportedXp, maxXp) - alreadyEarnedXp);
}

export interface ProgressState {
  totalAttempts: number;
  passedAttempts: number;
  bestPrecision: number | null;
  bestStars: number;
  earnedXp: number;
  passed: boolean;
  firstPassedAt: Date | null;
  lastAttemptAt: Date | null;
}

export interface VerifiedAttempt {
  precision: number;
  passed: boolean;
  stars: number;
  awardedXp: number;
}

/** Applies a verified attempt to the sign's progress (without mutating the original). */
export function applyAttempt(
  progress: ProgressState,
  attempt: VerifiedAttempt,
  now: Date,
): ProgressState {
  return {
    totalAttempts: progress.totalAttempts + 1,
    passedAttempts: progress.passedAttempts + (attempt.passed ? 1 : 0),
    bestPrecision: Math.max(progress.bestPrecision ?? 0, attempt.precision),
    bestStars: Math.max(progress.bestStars, attempt.passed ? attempt.stars : 0),
    earnedXp: progress.earnedXp + attempt.awardedXp,
    passed: progress.passed || attempt.passed,
    firstPassedAt: progress.firstPassedAt ?? (attempt.passed ? now : null),
    lastAttemptAt: now,
  };
}
