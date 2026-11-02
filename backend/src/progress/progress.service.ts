import { HttpStatus, Injectable } from '@nestjs/common';
import { ApiError, ErrorCode } from '../common/api-error';
import { sameSignCode } from '../common/sign-code';
import { LevelsService } from '../levels/levels.service';
import { SignStatus } from '../levels/unlocks';
import { ProgressSummaryDto, SignProgressDto } from './dto/progress.dto';

@Injectable()
export class ProgressService {
  constructor(private readonly levels: LevelsService) {}

  async summary(userId: string): Promise<ProgressSummaryDto> {
    const { catalog, progress, totalXp } = await this.levels.loadAll(userId);
    const catalogIds = new Set(
      catalog.flatMap((l) => l.signs.map((s) => s.id)),
    );
    const rows = [...progress.values()].filter((p) => catalogIds.has(p.signId));

    const unlocked = catalog.filter((l) => totalXp >= l.requiredXp);
    const highest = unlocked.at(-1);
    const next = catalog.find((l) => totalXp < l.requiredXp);

    const totalAttempts = rows.reduce((s, p) => s + p.totalAttempts, 0);
    const passedAttempts = rows.reduce((s, p) => s + p.passedAttempts, 0);
    return {
      totalXp,
      highestUnlockedLevel: highest
        ? {
            code: highest.code,
            name: highest.name,
            requiredXp: highest.requiredXp,
          }
        : null,
      nextLevel: next
        ? {
            code: next.code,
            name: next.name,
            requiredXp: next.requiredXp,
            missingXp: next.requiredXp - totalXp,
          }
        : null,
      passedSigns: rows.filter((p) => p.passed).length,
      totalSigns: catalogIds.size,
      totalStars: rows.reduce((s, p) => s + p.bestStars, 0),
      totalAttempts,
      passRate:
        totalAttempts === 0
          ? 0
          : Math.round((passedAttempts / totalAttempts) * 10000) / 10000,
    };
  }

  async bySign(userId: string, code: string): Promise<SignProgressDto> {
    const { catalog, progress, totalXp } = await this.levels.loadAll(userId);
    const appearances = catalog
      .map((level) => ({
        level,
        index: level.signs.findIndex((s) => sameSignCode(s.code, code)),
      }))
      .filter((a) => a.index >= 0);
    if (appearances.length === 0) {
      throw new ApiError(
        HttpStatus.NOT_FOUND,
        ErrorCode.SIGN_NOT_FOUND,
        'The sign does not exist.',
      );
    }

    const sign = appearances[0].level.signs[appearances[0].index];
    const p = progress.get(sign.id);
    const passedIds = this.levels.passedIds(progress);
    const statuses = appearances.map(
      (a) =>
        this.levels.levelState(a.level, passedIds, totalXp).statuses[a.index],
    );
    const status = statuses.includes(SignStatus.PASSED)
      ? SignStatus.PASSED
      : statuses.includes(SignStatus.AVAILABLE)
        ? SignStatus.AVAILABLE
        : SignStatus.LOCKED;

    return {
      signCode: sign.code,
      name: sign.name,
      status,
      totalAttempts: p?.totalAttempts ?? 0,
      passedAttempts: p?.passedAttempts ?? 0,
      bestPrecision: p?.bestPrecision ?? null,
      bestStars: p?.bestStars ?? 0,
      earnedXp: p?.earnedXp ?? 0,
      passed: p?.passed ?? false,
      firstPassedAt: p?.firstPassedAt ?? null,
      lastAttemptAt: p?.lastAttemptAt ?? null,
    };
  }
}
