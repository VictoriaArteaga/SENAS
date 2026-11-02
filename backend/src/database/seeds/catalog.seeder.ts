import { Injectable, Logger } from '@nestjs/common';
import { EntityManager, In, Not } from 'typeorm';
import {
  SIGN_CODE_MAX_LENGTH,
  SIGN_CODE_PATTERN,
} from '../../common/sign-code';
import { Evaluation } from '../../evaluations/entities/evaluation.entity';
import { LevelSign } from '../../levels/entities/level-sign.entity';
import { Level } from '../../levels/entities/level.entity';
import { SignProgress } from '../../progress/entities/sign-progress.entity';
import { Sign, SignType } from '../../signs/entities/sign.entity';
import catalogJson from './data/catalog.json';

export interface CatalogItem {
  code: string;
  name: string;
  type: string;
  position: number;
}
export interface CatalogLevel {
  code: string;
  name: string;
  description: string | null;
  position: number;
  requiredXp: number;
  items: CatalogItem[];
}

export const EXPECTED_LEVELS = 4;
export const EXPECTED_SIGNS_PER_LEVEL: Record<string, number> = {
  ALPHABET: 27,
  NUMBERS: 31,
  MONTHS: 12,
  GREETINGS_PHRASES: 11,
};
export const EXPECTED_TOTAL_SIGNS = 81;

export interface ReconciliationReport {
  /** Signs in the database that are not in the catalog and were deleted. */
  deleted: string[];
  /** Orphan signs still referenced by history/progress: deactivated, not deleted. */
  deactivated: string[];
}

/**
 * Idempotent load of the official catalog, in the caller's transaction:
 *  1. upsert levels and signs by `code`;
 *  2. replace the `level_signs` links of every catalog level (delete + insert),
 *     so reordering never trips the unique (level_id, position) constraint;
 *  3. reconcile signs that are in the database but not in the catalog: delete
 *     them if nothing references them, deactivate them otherwise.
 * It never touches player progress or history.
 */
@Injectable()
export class CatalogSeeder {
  private readonly logger = new Logger(CatalogSeeder.name);

  private readonly levels = (catalogJson as { levels: CatalogLevel[] }).levels;

  async seed(em: EntityManager): Promise<ReconciliationReport> {
    this.assertValidCodes();

    // 1. Levels and signs.
    for (const level of this.levels) {
      await em.upsert(
        Level,
        {
          code: level.code,
          name: level.name,
          description: level.description,
          position: level.position,
          requiredXp: level.requiredXp,
          active: true,
        },
        ['code'],
      );
      await em.upsert(
        Sign,
        level.items.map((e) => ({
          code: e.code,
          name: e.name,
          type: e.type as SignType,
          active: true,
        })),
        ['code'],
      );
    }

    // 2. Replace the links of every catalog level.
    for (const level of this.levels) {
      const dbLevel = await em.findOneByOrFail(Level, { code: level.code });
      const signs = await em.findBy(Sign, {
        code: In(level.items.map((e) => e.code)),
      });
      const idByCode = new Map(signs.map((s) => [s.code, s.id]));
      await em.delete(LevelSign, { levelId: dbLevel.id });
      await em.insert(
        LevelSign,
        level.items.map((e) => ({
          levelId: dbLevel.id,
          signId: idByCode.get(e.code) as string,
          position: e.position,
        })),
      );
    }

    // 3. Orphan signs.
    const report = await this.reconcileOrphans(em);

    const total = this.levels.reduce((n, l) => n + l.items.length, 0);
    this.logger.log(
      `Catalog synchronized: ${this.levels.length} levels, ${total} items` +
        (report.deleted.length || report.deactivated.length
          ? ` (orphan signs: ${report.deleted.length} deleted, ${report.deactivated.length} deactivated)`
          : '.'),
    );
    return report;
  }

  /** Checks 4 levels and 81 active signs (27 + 31 + 12 + 11). Logs the errors found. */
  async verify(em: EntityManager): Promise<boolean> {
    const levels = await em.countBy(Level, { active: true });
    const signs = await em.countBy(Sign, { active: true });
    const rows = await em
      .createQueryBuilder(LevelSign, 'ls')
      .innerJoin('ls.level', 'l')
      .innerJoin('ls.sign', 's')
      .where('s.active = true')
      .select('l.code', 'code')
      .addSelect('count(*)::int', 'total')
      .groupBy('l.code')
      .getRawMany<{ code: string; total: number }>();

    const perLevel = Object.fromEntries(rows.map((f) => [f.code, f.total]));
    const errors: string[] = [];
    if (levels !== EXPECTED_LEVELS)
      errors.push(`active levels: ${levels} (expected ${EXPECTED_LEVELS})`);
    if (signs !== EXPECTED_TOTAL_SIGNS)
      errors.push(`active signs: ${signs} (expected ${EXPECTED_TOTAL_SIGNS})`);
    for (const [code, expected] of Object.entries(EXPECTED_SIGNS_PER_LEVEL)) {
      if (perLevel[code] !== expected)
        errors.push(
          `${code}: ${perLevel[code] ?? 0} signs (expected ${expected})`,
        );
    }

    if (errors.length) {
      this.logger.error(
        `The catalog does not match the official one: ${errors.join('; ')}`,
      );
      return false;
    }
    this.logger.log('Catalog verified: 4 levels and 81 active signs.');
    return true;
  }

  private async reconcileOrphans(
    em: EntityManager,
  ): Promise<ReconciliationReport> {
    const catalogCodes = this.levels.flatMap((l) => l.items.map((i) => i.code));
    const orphans = await em.findBy(Sign, { code: Not(In(catalogCodes)) });
    const report: ReconciliationReport = { deleted: [], deactivated: [] };
    if (orphans.length === 0) return report;

    const ids = orphans.map((o) => o.id);
    // Orphans must not stay linked to any level.
    await em.delete(LevelSign, { signId: In(ids) });

    // Which orphans do history or progress still reference? (two queries, not one per sign)
    const [evaluated, progressed] = await Promise.all([
      em
        .createQueryBuilder(Evaluation, 'e')
        .select('DISTINCT e.signId', 'signId')
        .where('e.signId IN (:...ids)', { ids })
        .getRawMany<{ signId: string }>(),
      em
        .createQueryBuilder(SignProgress, 'p')
        .select('DISTINCT p.signId', 'signId')
        .where('p.signId IN (:...ids)', { ids })
        .getRawMany<{ signId: string }>(),
    ]);
    const referenced = new Set(
      [...evaluated, ...progressed].map((r) => r.signId),
    );

    const toDeactivate = orphans.filter((o) => referenced.has(o.id));
    const toDelete = orphans.filter((o) => !referenced.has(o.id));
    if (toDeactivate.length) {
      await em.update(
        Sign,
        { id: In(toDeactivate.map((o) => o.id)) },
        { active: false },
      );
    }
    if (toDelete.length) {
      await em.delete(Sign, { id: In(toDelete.map((o) => o.id)) });
    }
    report.deactivated.push(...toDeactivate.map((o) => o.code));
    report.deleted.push(...toDelete.map((o) => o.code));
    return report;
  }

  /** Fails fast if catalog.json contains a code the API would not accept. */
  private assertValidCodes(): void {
    const seen = new Set<string>();
    for (const level of this.levels) {
      for (const item of level.items) {
        if (
          item.code.length > SIGN_CODE_MAX_LENGTH ||
          !SIGN_CODE_PATTERN.test(item.code)
        ) {
          throw new Error(`Invalid sign code in catalog.json: ${item.code}`);
        }
        const key = item.code.toLowerCase();
        if (seen.has(key)) {
          throw new Error(`Duplicate sign code in catalog.json: ${item.code}`);
        }
        seen.add(key);
      }
    }
  }
}
