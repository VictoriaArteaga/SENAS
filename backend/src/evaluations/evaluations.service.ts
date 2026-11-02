import { HttpStatus, Inject, Injectable } from '@nestjs/common';
import { InjectDataSource } from '@nestjs/typeorm';
import { randomUUID } from 'node:crypto';
import { DataSource, EntityManager } from 'typeorm';
import { ApiError, ErrorCode } from '../common/api-error';
import { sameSignCode } from '../common/sign-code';
import { APP_CONFIG, type AppConfig } from '../config/configuration';
import { CatalogLevel, LevelsService } from '../levels/levels.service';
import { SignStatus } from '../levels/unlocks';
import { Profile } from '../profiles/entities/profile.entity';
import { SignProgress } from '../progress/entities/sign-progress.entity';
import { CreateEvaluationDto } from './dto/create-evaluation.dto';
import { EvaluationResponseDto } from './dto/evaluation-response.dto';
import {
  HistoryFilterDto,
  HistoryItemDto,
  HistoryResponseDto,
} from './dto/history.dto';
import { Evaluation } from './entities/evaluation.entity';
import {
  applyAttempt,
  computeAwardedXp,
  maxXpPerSign,
  normalizePrecision,
  verifyScore,
} from './scoring';

export interface RegistrationResult {
  response: EvaluationResponseDto;
  /** true the first time (201); false for an identical retry (200). */
  created: boolean;
}

const UNIQUE_ATTEMPT = 'ux_evaluations_user_attempt';

@Injectable()
export class EvaluationsService {
  constructor(
    @InjectDataSource() private readonly dataSource: DataSource,
    private readonly levels: LevelsService,
    @Inject(APP_CONFIG) private readonly config: AppConfig,
  ) {}

  /**
   * Records an attempt in a single transaction: idempotent, verifies the score
   * against the official table, never duplicates XP and computes unlocks.
   */
  async register(
    userId: string,
    dto: CreateEvaluationDto,
  ): Promise<RegistrationResult> {
    try {
      return await this.dataSource.transaction((em) =>
        this.registerInTransaction(em, userId, dto),
      );
    } catch (e) {
      // Two simultaneous submissions of the same attempt: the unique index lets
      // only one through and the other receives the stored response.
      const driver = e as { code?: string; constraint?: string };
      if (driver.code === '23505' && driver.constraint === UNIQUE_ATTEMPT) {
        const existing = await this.findExisting(
          this.dataSource.manager,
          userId,
          dto.attemptId,
        );
        if (existing) return this.resolveDuplicate(existing, dto);
      }
      throw e;
    }
  }

  async history(
    userId: string,
    filter: HistoryFilterDto,
  ): Promise<HistoryResponseDto> {
    const qb = this.dataSource
      .getRepository(Evaluation)
      .createQueryBuilder('e')
      .innerJoinAndSelect('e.sign', 's')
      .innerJoinAndSelect('e.level', 'l')
      .where('e.userId = :userId', { userId });

    if (filter.sign)
      qb.andWhere('lower(s.code) = lower(:sign)', { sign: filter.sign });
    if (filter.level)
      qb.andWhere('l.code = :level', { level: filter.level.toUpperCase() });
    if (filter.passed !== undefined)
      qb.andWhere('e.passed = :passed', { passed: filter.passed });
    if (filter.from)
      qb.andWhere('e.createdAt >= :from', { from: new Date(filter.from) });
    if (filter.to)
      qb.andWhere('e.createdAt <= :to', { to: new Date(filter.to) });

    const [rows, total] = await qb
      .orderBy('e.createdAt', 'DESC')
      .addOrderBy('e.id', 'DESC')
      .skip((filter.page - 1) * filter.limit)
      .take(filter.limit)
      .getManyAndCount();

    const items: HistoryItemDto[] = rows.map((e) => ({
      id: e.id,
      attemptId: e.attemptId,
      signCode: e.sign?.code ?? '',
      levelCode: e.level?.code ?? '',
      precision: e.precision,
      reportedStars: e.reportedStars,
      reportedXp: e.reportedXp,
      passed: e.passed,
      awardedXp: e.awardedXp,
      durationMs: e.durationMs,
      feedback: e.feedback,
      createdAt: e.createdAt,
    }));
    return { items, total, page: filter.page, limit: filter.limit };
  }

  private async registerInTransaction(
    em: EntityManager,
    userId: string,
    dto: CreateEvaluationDto,
  ): Promise<RegistrationResult> {
    const precision = normalizePrecision(dto.precision);

    // 1. Idempotency.
    const previous = await this.findExisting(em, userId, dto.attemptId);
    if (previous) return this.resolveDuplicate(previous, dto);

    // 2–4. Sign, level and sign unlocked.
    const catalog = await this.levels.loadCatalog(em);
    const xpBeforeRead = await this.levels.loadTotalXp(userId, em);
    const previousProgress = await this.levels.loadProgress(userId, em);
    const passedIds = this.levels.passedIds(previousProgress);
    const { level, index, signId } = this.resolveLevel(
      catalog,
      dto.signCode,
      passedIds,
      xpBeforeRead,
    );

    // 5. Verification against the official table (not a recomputation).
    const rules = this.config.scoring;
    const v = verifyScore(precision, dto.stars, dto.xp, rules);
    if (!v.consistent) {
      throw new ApiError(
        HttpStatus.UNPROCESSABLE_ENTITY,
        ErrorCode.INCONSISTENT_RESULT,
        `Precision ${precision} corresponds to ${v.expectedStars} stars and ${v.expectedXp} XP; ${dto.stars} and ${dto.xp} were reported.`,
      );
    }

    // 6. Lock the progress row (created if missing).
    await em
      .createQueryBuilder()
      .insert()
      .into(SignProgress)
      .values({ userId, signId })
      .orIgnore()
      .execute();
    const progress = await em.findOneOrFail(SignProgress, {
      where: { userId, signId },
      lock: { mode: 'pessimistic_write' },
    });
    // Another submission of the same attempt may have committed while we waited for the lock.
    const race = await this.findExisting(em, userId, dto.attemptId);
    if (race) return this.resolveDuplicate(race, dto);

    // 7. Evaluation, progress and profile XP.
    const alreadyPassed = progress.passed;
    const now = new Date();
    const awardedXp = computeAwardedXp(
      v.passed,
      dto.xp,
      progress.earnedXp,
      maxXpPerSign(rules),
    );
    const next = applyAttempt(
      progress,
      { precision, passed: v.passed, stars: dto.stars, awardedXp },
      now,
    );
    await em.update(SignProgress, progress.id, next);

    let totalXp = xpBeforeRead;
    if (awardedXp > 0) {
      // Atomic increment: simultaneous attempts on different signs cannot overwrite each other.
      const r = await em
        .createQueryBuilder()
        .update(Profile)
        .set({ totalXp: () => 'total_xp + :xp' })
        .setParameter('xp', awardedXp)
        .where('user_id = :userId', { userId })
        .returning('total_xp')
        .execute();
      totalXp = (r.raw as { total_xp: number }[])[0].total_xp;
    }
    const xpBefore = totalXp - awardedXp;

    // 8. Unlocks caused by this attempt.
    const unlockedSign =
      v.passed && !alreadyPassed
        ? (level.signs[index + 1]?.code ?? null)
        : null;
    const unlockedLevels = catalog
      .filter((l) => l.requiredXp > xpBefore && l.requiredXp <= totalXp)
      .map((l) => l.code);

    const evaluationId = randomUUID();
    const response: EvaluationResponseDto = {
      evaluationId,
      attemptId: dto.attemptId,
      signCode: level.signs[index].code,
      passed: v.passed,
      precision,
      stars: v.passed ? dto.stars : 0,
      awardedXp,
      totalXp,
      bestPrecision: next.bestPrecision ?? precision,
      bestStars: next.bestStars,
      unlockedSign,
      unlockedLevels,
    };
    await em.insert(Evaluation, {
      id: evaluationId,
      userId,
      signId,
      levelId: level.id,
      attemptId: dto.attemptId,
      precision,
      reportedStars: dto.stars,
      reportedXp: dto.xp,
      passed: v.passed,
      awardedXp,
      durationMs: dto.durationMs ?? null,
      feedback: dto.feedback ?? null,
      response,
    });
    return { response, created: true };
  }

  /**
   * A sign can appear in several levels. The first unlocked level where the
   * sign is AVAILABLE or PASSED is used.
   */
  private resolveLevel(
    catalog: CatalogLevel[],
    signCode: string,
    passedIds: ReadonlySet<string>,
    totalXp: number,
  ): { level: CatalogLevel; index: number; signId: string } {
    const candidates = catalog
      .map((level) => ({
        level,
        index: level.signs.findIndex((s) => sameSignCode(s.code, signCode)),
      }))
      .filter((c) => c.index >= 0);
    if (candidates.length === 0) {
      throw new ApiError(
        HttpStatus.NOT_FOUND,
        ErrorCode.SIGN_NOT_FOUND,
        'The sign does not exist or is not active.',
      );
    }

    const open = candidates
      .map((c) => ({
        ...c,
        state: this.levels.levelState(c.level, passedIds, totalXp),
      }))
      .filter((c) => c.state.unlocked);
    if (open.length === 0) {
      throw new ApiError(
        HttpStatus.FORBIDDEN,
        ErrorCode.LEVEL_LOCKED,
        'The level of this sign is still locked.',
      );
    }

    const chosen = open.find(
      (c) => c.state.statuses[c.index] !== SignStatus.LOCKED,
    );
    if (!chosen) {
      throw new ApiError(
        HttpStatus.FORBIDDEN,
        ErrorCode.SIGN_LOCKED,
        'Pass the previous sign to unlock this one.',
      );
    }
    return {
      level: chosen.level,
      index: chosen.index,
      signId: chosen.level.signs[chosen.index].id,
    };
  }

  private findExisting(
    em: EntityManager,
    userId: string,
    attemptId: string,
  ): Promise<Evaluation | null> {
    return em.findOne(Evaluation, {
      where: { userId, attemptId },
      relations: { sign: true },
    });
  }

  /** Same content → same response (200); different → 409. */
  private resolveDuplicate(
    previous: Evaluation,
    dto: CreateEvaluationDto,
  ): RegistrationResult {
    const same =
      !!previous.sign &&
      sameSignCode(previous.sign.code, dto.signCode) &&
      previous.precision === normalizePrecision(dto.precision) &&
      previous.reportedStars === dto.stars &&
      previous.reportedXp === dto.xp &&
      previous.durationMs === (dto.durationMs ?? null) &&
      previous.feedback === (dto.feedback ?? null);
    if (!same || !previous.response) {
      throw new ApiError(
        HttpStatus.CONFLICT,
        ErrorCode.DUPLICATE_ATTEMPT,
        'An attempt with that attemptId already exists with different content.',
      );
    }
    return { response: previous.response, created: false };
  }
}
