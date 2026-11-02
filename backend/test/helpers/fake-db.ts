import catalogJson from '../../src/database/seeds/data/catalog.json';
import { Evaluation } from '../../src/evaluations/entities/evaluation.entity';
import { LevelSign } from '../../src/levels/entities/level-sign.entity';
import { Profile } from '../../src/profiles/entities/profile.entity';
import { SignProgress } from '../../src/progress/entities/sign-progress.entity';
import type { AppConfig } from '../../src/config/configuration';

/** Test configuration with the official rules. */
export const TEST_CONFIG: AppConfig = {
  nodeEnv: 'test',
  port: 3000,
  apiPrefix: 'api/v1',
  swaggerEnabled: false,
  corsOrigins: [],
  db: {
    host: 'localhost',
    port: 5432,
    database: 'test',
    user: 'test',
    password: 'test',
    ssl: false,
    poolMax: 5,
    synchronize: true,
    seedOnStart: true,
  },
  auth: {
    accessSecret: 'test-secret-with-more-than-32-characters!!',
    accessTtl: '15m',
    accessTtlSeconds: 900,
    refreshTtlDays: 30,
  },
  scoring: {
    passThreshold: 60,
    twoStars: 74,
    threeStars: 88,
    xpPerStar: 10,
  },
  avatarIds: ['luna', 'nube', 'rio', 'sol'],
  throttle: { ttlMs: 60000, limit: 100, authLimit: 5 },
};

interface LevelRow {
  id: string;
  code: string;
  name: string;
  description: string | null;
  position: number;
  requiredXp: number;
  active: boolean;
}
interface SignRow {
  id: string;
  code: string;
  name: string;
  type: string;
  active: boolean;
}

export const USER_ID = 'user-1';

/**
 * In-memory database with the official catalog and the subset of the
 * EntityManager API used by the levels, evaluations and progress services.
 */
export class FakeDb {
  levels: LevelRow[] = [];
  signs: SignRow[] = [];
  levelSigns: { levelId: string; signId: string; position: number }[] = [];
  progress: SignProgress[] = [];
  evaluations: Evaluation[] = [];
  xp = new Map<string, number>();
  private serial = 0;

  constructor(initialXp = 0, userId = USER_ID) {
    for (const l of catalogJson.levels) {
      this.levels.push({
        id: `level-${l.code}`,
        code: l.code,
        name: l.name,
        description: l.description,
        position: l.position,
        requiredXp: l.requiredXp,
        active: true,
      });
      for (const e of l.items) {
        this.signs.push({
          id: `sign-${e.code}`,
          code: e.code,
          name: e.name,
          type: e.type,
          active: true,
        });
        this.levelSigns.push({
          levelId: `level-${l.code}`,
          signId: `sign-${e.code}`,
          position: e.position,
        });
      }
    }
    this.xp.set(userId, initialXp);
  }

  /** Marks signs as passed (1 star) without going through the API. */
  pass(userId: string, ...codes: string[]): void {
    for (const code of codes) {
      this.progress.push({
        id: `p-${++this.serial}`,
        userId,
        signId: `sign-${code}`,
        totalAttempts: 1,
        passedAttempts: 1,
        bestPrecision: 65,
        bestStars: 1,
        earnedXp: 10,
        passed: true,
        firstPassedAt: new Date(),
        lastAttemptAt: new Date(),
      } as SignProgress);
    }
  }

  progressOf(userId: string, code: string): SignProgress | undefined {
    return this.progress.find(
      (p) => p.userId === userId && p.signId === `sign-${code}`,
    );
  }

  readonly manager = FakeDb.createManager(this);
  readonly dataSource = {
    manager: this.manager,
    transaction: <T>(cb: (em: unknown) => Promise<T>) => cb(this.manager),
  };

  private static createManager(db: FakeDb) {
    const manager = {
      find: async (entity: unknown) => {
        if (entity !== LevelSign) throw new Error('find not supported');
        return [...db.levelSigns]
          .filter(
            (ls) =>
              db.levels.find((l) => l.id === ls.levelId)?.active &&
              db.signs.find((s) => s.id === ls.signId)?.active,
          )
          .map((ls) => ({
            ...ls,
            level: db.levels.find((l) => l.id === ls.levelId),
            sign: db.signs.find((s) => s.id === ls.signId),
          }))
          .sort(
            (a, b) =>
              (a.level?.position ?? 0) - (b.level?.position ?? 0) ||
              a.position - b.position,
          );
      },
      findBy: async (entity: unknown, where: { userId: string }) => {
        if (entity !== SignProgress) throw new Error('findBy not supported');
        return db.progress.filter((p) => p.userId === where.userId);
      },
      findOne: async (
        entity: unknown,
        { where }: { where: { userId: string; attemptId?: string } },
      ) => {
        if (entity === Profile) {
          return db.xp.has(where.userId)
            ? { id: 'profile', totalXp: db.xp.get(where.userId) }
            : null;
        }
        if (entity === Evaluation) {
          const ev = db.evaluations.find(
            (e) => e.userId === where.userId && e.attemptId === where.attemptId,
          );
          return ev
            ? { ...ev, sign: db.signs.find((s) => s.id === ev.signId) }
            : null;
        }
        throw new Error('findOne not supported');
      },
      findOneOrFail: async (
        _entity: unknown,
        { where }: { where: { userId: string; signId: string } },
      ) => {
        const p = db.progress.find(
          (x) => x.userId === where.userId && x.signId === where.signId,
        );
        if (!p) throw new Error('does not exist');
        return p;
      },
      update: async (
        _entity: unknown,
        id: string,
        changes: Partial<SignProgress>,
      ) => {
        Object.assign(
          db.progress.find((p) => p.id === id) as SignProgress,
          changes,
        );
      },
      insert: async (_entity: unknown, row: Evaluation) => {
        if (
          db.evaluations.some(
            (e) => e.userId === row.userId && e.attemptId === row.attemptId,
          )
        ) {
          throw Object.assign(new Error('duplicate'), {
            code: '23505',
            constraint: 'ux_evaluations_user_attempt',
          });
        }
        db.evaluations.push({ ...row, createdAt: new Date() } as Evaluation);
      },
      createQueryBuilder: () => {
        let operation: 'insert' | 'update' = 'insert';
        let values: Record<string, unknown> = {};
        let params: Record<string, unknown> = {};
        const qb = {
          insert: () => ((operation = 'insert'), qb),
          update: () => ((operation = 'update'), qb),
          into: () => qb,
          values: (v: Record<string, unknown>) => ((values = v), qb),
          orIgnore: () => qb,
          set: () => qb,
          setParameter: (k: string, v: unknown) => ((params[k] = v), qb),
          where: (_s: string, p?: Record<string, unknown>) => (
            (params = { ...params, ...p }),
            qb
          ),
          returning: () => qb,
          execute: async () => {
            if (operation === 'insert') {
              const { userId, signId } = values as {
                userId: string;
                signId: string;
              };
              if (!db.progressOf(userId, signId.replace('sign-', ''))) {
                db.progress.push({
                  id: `p-${++db.serial}`,
                  userId,
                  signId,
                  totalAttempts: 0,
                  passedAttempts: 0,
                  bestPrecision: null,
                  bestStars: 0,
                  earnedXp: 0,
                  passed: false,
                  firstPassedAt: null,
                  lastAttemptAt: null,
                } as SignProgress);
              }
              return { raw: [] };
            }
            const userId = params['userId'] as string;
            const next = (db.xp.get(userId) ?? 0) + (params['xp'] as number);
            db.xp.set(userId, next);
            return { raw: [{ total_xp: next }] };
          },
        };
        return qb;
      },
    };
    return manager;
  }
}
