import { JwtService } from '@nestjs/jwt';
import { randomUUID } from 'node:crypto';
import { FindOperator } from 'typeorm';
import { TEST_CONFIG } from '../../test/helpers/fake-db';
import { ApiError } from '../common/api-error';
import { User } from '../users/entities/user.entity';
import { AuthService, sha256 } from './auth.service';
import { Session } from './entities/session.entity';

type Row = Record<string, unknown>;

/** Users and sessions in memory with the part of the TypeORM API AuthService uses. */
class FakeAuthDb {
  users: Row[] = [];
  sessions: Row[] = [];

  private table(entity: unknown): Row[] {
    return entity === User ? this.users : this.sessions;
  }

  private matches(row: Row, where: Row): boolean {
    return Object.entries(where).every(([k, v]) => {
      if (v instanceof FindOperator)
        return v.type === 'isNull' ? row[k] === null : false;
      return row[k] === v;
    });
  }

  readonly manager = {
    create: (entity: unknown, data: Row) => ({
      ...(entity === Session
        ? { revokedAt: null, replacedById: null }
        : { active: true, role: 'PLAYER' }),
      ...data,
    }),
    save: async (row: Row) => {
      const isSession = 'refreshTokenHash' in row;
      row['id'] = row['id'] ?? randomUUID();
      (isSession ? this.sessions : this.users).push(row);
      return row;
    },
    existsBy: async (entity: unknown, where: Row) =>
      this.table(entity).some((r) => this.matches(r, where)),
    findOne: async (entity: unknown, { where }: { where: Row }) =>
      this.table(entity).find((r) => this.matches(r, where)) ?? null,
    findOneBy: async (entity: unknown, where: Row) =>
      this.table(entity).find((r) => this.matches(r, where)) ?? null,
    update: async (entity: unknown, where: string | Row, changes: Row) => {
      const criteria = typeof where === 'string' ? { id: where } : where;
      this.table(entity)
        .filter((r) => this.matches(r, criteria))
        .forEach((r) => Object.assign(r, changes));
    },
  };

  readonly dataSource = {
    transaction: <T>(cb: (em: unknown) => Promise<T>) => cb(this.manager),
    getRepository: (entity: unknown) => ({
      findOneBy: this.manager.findOneBy.bind(null, entity),
      findOne: async ({ where }: { where: Row }) => {
        const s = this.table(entity).find((r) => this.matches(r, where));
        return s
          ? {
              ...s,
              user: this.users.find((u) => u['id'] === s['userId']),
            }
          : null;
      },
      update: (where: Row, changes: Row) =>
        this.manager.update(entity, where, changes),
    }),
  };
}

const profiles = {
  resolveAvatar: jest.fn((a?: string) => a ?? 'luna'),
  create: jest.fn(async () => ({})),
};

async function build() {
  const db = new FakeAuthDb();
  const jwt = new JwtService({
    secret: TEST_CONFIG.auth.accessSecret,
    signOptions: { algorithm: 'HS256' },
  });
  const svc = new AuthService(
    db.dataSource as never,
    jwt,
    profiles as never,
    TEST_CONFIG,
  );
  await svc.onModuleInit();
  return { db, svc, jwt };
}

const registration = {
  email: 'Ana@Example.com ',
  password: 'Lumina2026',
  displayName: 'Ana',
};
const ctx = { ip: '1.2.3.4', userAgent: 'Unity' };

async function errorOf(p: Promise<unknown>): Promise<ApiError> {
  try {
    await p;
  } catch (e) {
    return e as ApiError;
  }
  throw new Error('an error was expected');
}

describe('AuthService', () => {
  beforeEach(() => jest.clearAllMocks());

  describe('register', () => {
    it('creates a user with a lowercase email, an argon2id hash, a profile and a session', async () => {
      const { db, svc, jwt } = await build();
      const t = await svc.register(registration, ctx);

      expect(db.users).toHaveLength(1);
      expect(db.users[0]['email']).toBe('ana@example.com');
      expect(db.users[0]['passwordHash']).toMatch(/^\$argon2id\$/);
      expect(profiles.create).toHaveBeenCalledTimes(1);
      expect(t).toMatchObject({
        tokenType: 'Bearer',
        expiresIn: 900,
        user: { email: 'ana@example.com', role: 'PLAYER' },
      });

      const [sid, secret] = t.refreshToken.split('.');
      expect(db.sessions).toHaveLength(1);
      expect(db.sessions[0]).toMatchObject({
        id: sid,
        refreshTokenHash: sha256(secret),
        ip: '1.2.3.4',
        userAgent: 'Unity',
      });
      // The secret is never stored in the clear.
      expect(JSON.stringify(db.sessions)).not.toContain(secret);

      const payload = await jwt.verifyAsync(t.accessToken);
      expect(payload).toMatchObject({ sub: t.user.id, sid, role: 'PLAYER' });
      expect(payload.exp - payload.iat).toBe(900);
    });

    it('repeated email (even with different case) → 409 EMAIL_IN_USE', async () => {
      const { svc } = await build();
      await svc.register(registration, ctx);
      const e = await errorOf(
        svc.register({ ...registration, email: 'ANA@example.com' }, ctx),
      );
      expect(e.getStatus()).toBe(409);
      expect(e.code).toBe('EMAIL_IN_USE');
    });

    it('a unique index violation in a race is also EMAIL_IN_USE', async () => {
      const { db, svc } = await build();
      db.dataSource.transaction = () =>
        Promise.reject(Object.assign(new Error('dup'), { code: '23505' }));
      expect((await errorOf(svc.register(registration, ctx))).code).toBe(
        'EMAIL_IN_USE',
      );
    });

    it('other errors are propagated', async () => {
      const { db, svc } = await build();
      db.dataSource.transaction = () => Promise.reject(new Error('boom'));
      await expect(svc.register(registration, ctx)).rejects.toThrow('boom');
    });

    it('an invalid avatar is rejected before creating anything', async () => {
      const { db, svc } = await build();
      profiles.resolveAvatar.mockImplementationOnce(() => {
        throw new Error('avatar');
      });
      await expect(
        svc.register({ ...registration, avatarId: 'x' }, ctx),
      ).rejects.toThrow('avatar');
      expect(db.users).toHaveLength(0);
    });
  });

  describe('login', () => {
    it('returns tokens with correct credentials and updates the last access', async () => {
      const { db, svc } = await build();
      await svc.register(registration, ctx);
      const t = await svc.login(
        { email: 'ANA@example.com', password: 'Lumina2026' },
        ctx,
      );
      expect(t.accessToken).toBeTruthy();
      expect(db.sessions).toHaveLength(2);
      expect(db.users[0]['lastAccessAt']).toBeInstanceOf(Date);
    });

    it('wrong password and unknown email give the same 401 error', async () => {
      const { svc } = await build();
      await svc.register(registration, ctx);
      const wrong = await errorOf(
        svc.login({ email: 'ana@example.com', password: 'other1234' }, ctx),
      );
      const unknown = await errorOf(
        svc.login({ email: 'nobody@example.com', password: 'other1234' }, ctx),
      );
      for (const e of [wrong, unknown]) {
        expect(e.getStatus()).toBe(401);
        expect(e.code).toBe('INVALID_CREDENTIALS');
      }
      expect(wrong.message).toBe(unknown.message);
    });

    it('inactive user with the correct password → 403', async () => {
      const { db, svc } = await build();
      await svc.register(registration, ctx);
      db.users[0]['active'] = false;
      const e = await errorOf(
        svc.login({ email: 'ana@example.com', password: 'Lumina2026' }, ctx),
      );
      expect(e.getStatus()).toBe(403);
      expect(e.code).toBe('INACTIVE_USER');
    });

    it('inactive user with a wrong password is still 401 (does not reveal the status)', async () => {
      const { db, svc } = await build();
      await svc.register(registration, ctx);
      db.users[0]['active'] = false;
      const e = await errorOf(
        svc.login({ email: 'ana@example.com', password: 'wrong12345' }, ctx),
      );
      expect(e.code).toBe('INVALID_CREDENTIALS');
    });
  });

  describe('refresh', () => {
    it('rotates: revokes the used session, creates another and links them', async () => {
      const { db, svc } = await build();
      const t1 = await svc.register(registration, ctx);
      const t2 = await svc.refresh(t1.refreshToken, ctx);

      expect(t2.refreshToken).not.toBe(t1.refreshToken);
      expect(t2.accessToken).not.toBe(t1.accessToken);
      expect(db.sessions).toHaveLength(2);
      const [old, fresh] = db.sessions;
      expect(old['revokedAt']).toBeInstanceOf(Date);
      expect(old['replacedById']).toBe(fresh['id']);
      expect(fresh['revokedAt']).toBeNull();
    });

    it('can chain several rotations', async () => {
      const { svc } = await build();
      let t = await svc.register(registration, ctx);
      for (let i = 0; i < 3; i++) t = await svc.refresh(t.refreshToken, ctx);
      expect(t.refreshToken).toBeTruthy();
    });

    it('reusing an already rotated refresh token revokes ALL sessions → 401 INVALID_SESSION', async () => {
      const { db, svc } = await build();
      const t1 = await svc.register(registration, ctx);
      const t2 = await svc.refresh(t1.refreshToken, ctx);
      const otherDevice = await svc.login(
        { email: 'ana@example.com', password: 'Lumina2026' },
        ctx,
      );

      const e = await errorOf(svc.refresh(t1.refreshToken, ctx));
      expect(e.getStatus()).toBe(401);
      expect(e.code).toBe('INVALID_SESSION');
      expect(db.sessions.every((s) => s['revokedAt'] instanceof Date)).toBe(
        true,
      );

      // Neither the latest legitimate token nor the other device's works any more.
      expect((await errorOf(svc.refresh(t2.refreshToken, ctx))).code).toBe(
        'INVALID_SESSION',
      );
      expect(
        (await errorOf(svc.refresh(otherDevice.refreshToken, ctx))).code,
      ).toBe('INVALID_SESSION');
    });

    it('revocation on reuse does not affect other users', async () => {
      const { db, svc } = await build();
      const a = await svc.register(registration, ctx);
      const b = await svc.register(
        { ...registration, email: 'beto@example.com' },
        ctx,
      );
      await svc.refresh(a.refreshToken, ctx);
      await errorOf(svc.refresh(a.refreshToken, ctx));
      const betoSession = db.sessions.find((s) => s['userId'] === b.user.id);
      expect(betoSession?.['revokedAt']).toBeNull();
    });

    it('a wrong secret is INVALID_SESSION and revokes nothing', async () => {
      const { db, svc } = await build();
      const t = await svc.register(registration, ctx);
      const [sid] = t.refreshToken.split('.');
      const e = await errorOf(svc.refresh(`${sid}.fake-secret`, ctx));
      expect(e.code).toBe('INVALID_SESSION');
      expect(db.sessions[0]['revokedAt']).toBeNull();
    });

    it.each(['', 'no-dot', '.', 'not-a-uuid.secret', `${randomUUID()}.`])(
      'invalid format %p → INVALID_SESSION',
      async (token) => {
        const { svc } = await build();
        expect((await errorOf(svc.refresh(token, ctx))).code).toBe(
          'INVALID_SESSION',
        );
      },
    );

    it('nonexistent session → INVALID_SESSION', async () => {
      const { svc } = await build();
      expect(
        (await errorOf(svc.refresh(`${randomUUID()}.abc`, ctx))).code,
      ).toBe('INVALID_SESSION');
    });

    it('expired session → INVALID_SESSION', async () => {
      const { db, svc } = await build();
      const t = await svc.register(registration, ctx);
      db.sessions[0]['expiresAt'] = new Date(Date.now() - 1000);
      expect((await errorOf(svc.refresh(t.refreshToken, ctx))).code).toBe(
        'INVALID_SESSION',
      );
    });

    it('disabled user → 403 INACTIVE_USER', async () => {
      const { db, svc } = await build();
      const t = await svc.register(registration, ctx);
      db.users[0]['active'] = false;
      const e = await errorOf(svc.refresh(t.refreshToken, ctx));
      expect(e.getStatus()).toBe(403);
      expect(e.code).toBe('INACTIVE_USER');
    });

    it('deleted user → INVALID_SESSION', async () => {
      const { db, svc } = await build();
      const t = await svc.register(registration, ctx);
      db.users.length = 0;
      expect((await errorOf(svc.refresh(t.refreshToken, ctx))).code).toBe(
        'INVALID_SESSION',
      );
    });
  });

  describe('logout and active session', () => {
    it('logout revokes only the current session', async () => {
      const { db, svc } = await build();
      const a = await svc.register(registration, ctx);
      const b = await svc.login(
        { email: 'ana@example.com', password: 'Lumina2026' },
        ctx,
      );
      const [sidA] = a.refreshToken.split('.');
      const [sidB] = b.refreshToken.split('.');

      await svc.logout(sidA);
      expect(await svc.isSessionActive(sidA, a.user.id)).toBe(false);
      expect(await svc.isSessionActive(sidB, a.user.id)).toBe(true);
      expect(db.sessions.filter((s) => s['revokedAt'] === null)).toHaveLength(
        1,
      );
    });

    it('logoutAll revokes every session of the user', async () => {
      const { svc } = await build();
      const a = await svc.register(registration, ctx);
      const b = await svc.login(
        { email: 'ana@example.com', password: 'Lumina2026' },
        ctx,
      );
      await svc.logoutAll(a.user.id);
      expect(
        await svc.isSessionActive(a.refreshToken.split('.')[0], a.user.id),
      ).toBe(false);
      expect(
        await svc.isSessionActive(b.refreshToken.split('.')[0], a.user.id),
      ).toBe(false);
    });

    it('isSessionActive fails if the session belongs to another user, expired, does not exist or the user is inactive', async () => {
      const { db, svc } = await build();
      const t = await svc.register(registration, ctx);
      const sid = t.refreshToken.split('.')[0];

      expect(await svc.isSessionActive(sid, t.user.id)).toBe(true);
      expect(await svc.isSessionActive(sid, randomUUID())).toBe(false);
      expect(await svc.isSessionActive(randomUUID(), t.user.id)).toBe(false);

      db.users[0]['active'] = false;
      expect(await svc.isSessionActive(sid, t.user.id)).toBe(false);
      db.users[0]['active'] = true;
      db.sessions[0]['expiresAt'] = new Date(Date.now() - 1);
      expect(await svc.isSessionActive(sid, t.user.id)).toBe(false);
    });
  });
});
