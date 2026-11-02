import { HttpStatus, Injectable } from '@nestjs/common';
import { InjectDataSource } from '@nestjs/typeorm';
import { DataSource, EntityManager } from 'typeorm';
import { ApiError, ErrorCode } from '../common/api-error';
import { Profile } from '../profiles/entities/profile.entity';
import { SignProgress } from '../progress/entities/sign-progress.entity';
import { SignType } from '../signs/entities/sign.entity';
import { LevelDetailDto, LevelSignDto, LevelSummaryDto } from './dto/level.dto';
import { LevelSign } from './entities/level-sign.entity';
import { isLevelUnlocked, levelSignStatuses, SignStatus } from './unlocks';

export const MAX_STARS_PER_SIGN = 3;

export interface CatalogSign {
  id: string;
  code: string;
  name: string;
  type: SignType;
  position: number;
}

export interface CatalogLevel {
  id: string;
  code: string;
  name: string;
  description: string | null;
  position: number;
  requiredXp: number;
  signs: CatalogSign[];
}

export interface LevelState {
  unlocked: boolean;
  /** Status of each sign, in the same order as `level.signs`. */
  statuses: SignStatus[];
}

@Injectable()
export class LevelsService {
  constructor(@InjectDataSource() private readonly dataSource: DataSource) {}

  /** Active levels with their active signs, both in order. */
  async loadCatalog(
    em: EntityManager = this.dataSource.manager,
  ): Promise<CatalogLevel[]> {
    const rows = await em.find(LevelSign, {
      where: { level: { active: true }, sign: { active: true } },
      relations: { level: true, sign: true },
      order: { level: { position: 'ASC' }, position: 'ASC' },
    });

    const byLevel = new Map<string, CatalogLevel>();
    for (const r of rows) {
      if (!r.level || !r.sign) continue;
      let level = byLevel.get(r.level.id);
      if (!level) {
        level = {
          id: r.level.id,
          code: r.level.code,
          name: r.level.name,
          description: r.level.description,
          position: r.level.position,
          requiredXp: r.level.requiredXp,
          signs: [],
        };
        byLevel.set(r.level.id, level);
      }
      level.signs.push({
        id: r.sign.id,
        code: r.sign.code,
        name: r.sign.name,
        type: r.sign.type,
        position: r.position,
      });
    }
    return [...byLevel.values()].sort((a, b) => a.position - b.position);
  }

  async loadProgress(
    userId: string,
    em: EntityManager = this.dataSource.manager,
  ): Promise<Map<string, SignProgress>> {
    const rows = await em.findBy(SignProgress, { userId });
    return new Map(rows.map((p) => [p.signId, p]));
  }

  async loadTotalXp(
    userId: string,
    em: EntityManager = this.dataSource.manager,
  ): Promise<number> {
    const profile = await em.findOne(Profile, {
      where: { userId },
      select: { id: true, totalXp: true },
    });
    return profile?.totalXp ?? 0;
  }

  /** Computed (never stored) state of a level for a player. */
  levelState(
    level: CatalogLevel,
    passed: ReadonlySet<string>,
    totalXp: number,
  ): LevelState {
    const unlocked = isLevelUnlocked(totalXp, level.requiredXp);
    return {
      unlocked,
      statuses: levelSignStatuses(
        level.signs.map((s) => s.id),
        passed,
        unlocked,
      ),
    };
  }

  async list(userId: string): Promise<LevelSummaryDto[]> {
    const { catalog, progress, totalXp } = await this.loadAll(userId);
    const passed = this.passedIds(progress);
    return catalog.map((l) => this.summary(l, progress, passed, totalXp));
  }

  async detail(userId: string, code: string): Promise<LevelDetailDto> {
    const { catalog, progress, totalXp } = await this.loadAll(userId);
    const level = catalog.find((l) => l.code === code.toUpperCase());
    if (!level) {
      throw new ApiError(
        HttpStatus.NOT_FOUND,
        ErrorCode.LEVEL_NOT_FOUND,
        'The level does not exist.',
      );
    }
    const passed = this.passedIds(progress);
    const { statuses } = this.levelState(level, passed, totalXp);
    const signs: LevelSignDto[] = level.signs.map((s, i) => ({
      code: s.code,
      name: s.name,
      type: s.type,
      position: s.position,
      status: statuses[i],
      bestPrecision: progress.get(s.id)?.bestPrecision ?? null,
      bestStars: progress.get(s.id)?.bestStars ?? 0,
    }));
    return { ...this.summary(level, progress, passed, totalXp), signs };
  }

  async loadAll(userId: string) {
    const [catalog, progress, totalXp] = await Promise.all([
      this.loadCatalog(),
      this.loadProgress(userId),
      this.loadTotalXp(userId),
    ]);
    return { catalog, progress, totalXp };
  }

  passedIds(progress: Map<string, SignProgress>): Set<string> {
    return new Set(
      [...progress.values()].filter((p) => p.passed).map((p) => p.signId),
    );
  }

  private summary(
    level: CatalogLevel,
    progress: Map<string, SignProgress>,
    passed: ReadonlySet<string>,
    totalXp: number,
  ): LevelSummaryDto {
    const { unlocked } = this.levelState(level, passed, totalXp);
    return {
      code: level.code,
      name: level.name,
      description: level.description,
      position: level.position,
      unlocked,
      requiredXp: level.requiredXp,
      missingXp: Math.max(0, level.requiredXp - totalXp),
      passedSigns: level.signs.filter((s) => passed.has(s.id)).length,
      totalSigns: level.signs.length,
      earnedStars: level.signs.reduce(
        (sum, s) => sum + (progress.get(s.id)?.bestStars ?? 0),
        0,
      ),
      possibleStars: level.signs.length * MAX_STARS_PER_SIGN,
    };
  }
}
