import { HttpStatus, Inject, Injectable, OnModuleInit } from '@nestjs/common';
import { JwtService } from '@nestjs/jwt';
import * as argon2 from 'argon2';
import { createHash, randomBytes, timingSafeEqual } from 'node:crypto';
import { DataSource, EntityManager, IsNull } from 'typeorm';
import { ApiError, ErrorCode } from '../common/api-error';
import { APP_CONFIG, type AppConfig } from '../config/configuration';
import { ProfilesService } from '../profiles/profiles.service';
import { User } from '../users/entities/user.entity';
import { normalizeEmail } from '../users/users.service';
import { LoginDto, RegisterDto, TokensResponseDto } from './dto/auth.dto';
import { Session } from './entities/session.entity';

export interface ClientContext {
  ip?: string | null;
  userAgent?: string | null;
}

const UUID_RE =
  /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const ARGON_OPTIONS = { type: argon2.argon2id } as const;

export const sha256 = (value: string): string =>
  createHash('sha256').update(value).digest('hex');

function invalidSession(): ApiError {
  return new ApiError(
    HttpStatus.UNAUTHORIZED,
    ErrorCode.INVALID_SESSION,
    'The session is not valid. Please sign in again.',
  );
}

type RefreshResult = TokensResponseDto | 'REUSE' | 'INVALID';

@Injectable()
export class AuthService implements OnModuleInit {
  /** Fake hash so login costs the same whether or not the email exists. */
  private fakeHash = '';

  constructor(
    private readonly dataSource: DataSource,
    private readonly jwt: JwtService,
    private readonly profiles: ProfilesService,
    @Inject(APP_CONFIG) private readonly config: AppConfig,
  ) {}

  async onModuleInit(): Promise<void> {
    this.fakeHash = await argon2.hash(
      randomBytes(16).toString('hex'),
      ARGON_OPTIONS,
    );
  }

  async register(
    dto: RegisterDto,
    ctx: ClientContext,
  ): Promise<TokensResponseDto> {
    const email = normalizeEmail(dto.email);
    // Validate the avatar before spending time on the hash.
    this.profiles.resolveAvatar(dto.avatarId);
    const passwordHash = await argon2.hash(dto.password, ARGON_OPTIONS);

    try {
      return await this.dataSource.transaction(async (em) => {
        if (await em.existsBy(User, { email })) throw this.emailInUse();

        const user = await em.save(
          em.create(User, { email, passwordHash, lastAccessAt: new Date() }),
        );
        await this.profiles.create(em, user.id, dto.displayName, dto.avatarId);
        return this.issueSession(em, user, ctx);
      });
    } catch (e) {
      // Two simultaneous registrations with the same email: the unique index wins.
      if ((e as { code?: string }).code === '23505') throw this.emailInUse();
      throw e;
    }
  }

  async login(dto: LoginDto, ctx: ClientContext): Promise<TokensResponseDto> {
    const email = normalizeEmail(dto.email);
    const user = await this.dataSource.getRepository(User).findOneBy({ email });

    const matches = await argon2
      .verify(user?.passwordHash ?? this.fakeHash, dto.password)
      .catch(() => false);
    if (!user || !matches) {
      throw new ApiError(
        HttpStatus.UNAUTHORIZED,
        ErrorCode.INVALID_CREDENTIALS,
        'Incorrect email or password.',
      );
    }
    this.requireActive(user);

    return this.dataSource.transaction(async (em) => {
      await em.update(User, user.id, { lastAccessAt: new Date() });
      return this.issueSession(em, user, ctx);
    });
  }

  /**
   * Rotates the refresh token: revokes the session used and creates another.
   * If a token from an already revoked session arrives, theft/reuse is assumed
   * and all of the user's sessions are revoked.
   */
  async refresh(
    refreshToken: string,
    ctx: ClientContext,
  ): Promise<TokensResponseDto> {
    const [sessionId, secret] = refreshToken.split('.');
    if (!sessionId || !secret || !UUID_RE.test(sessionId)) {
      throw invalidSession();
    }

    const result = await this.dataSource.transaction(
      async (em): Promise<RefreshResult> => {
        // Row lock: two simultaneous refreshes of the same token are serialized.
        const session = await em.findOne(Session, {
          where: { id: sessionId },
          lock: { mode: 'pessimistic_write' },
        });
        if (!session || !this.secretMatches(secret, session.refreshTokenHash)) {
          return 'INVALID';
        }

        const now = new Date();
        if (session.revokedAt) {
          await em.update(
            Session,
            { userId: session.userId, revokedAt: IsNull() },
            { revokedAt: now },
          );
          return 'REUSE';
        }
        if (session.expiresAt <= now) return 'INVALID';

        const user = await em.findOneBy(User, { id: session.userId });
        if (!user) return 'INVALID';
        this.requireActive(user);

        const tokens = await this.issueSession(em, user, ctx);
        const newSessionId = tokens.refreshToken.split('.')[0];
        await em.update(Session, session.id, {
          revokedAt: now,
          replacedById: newSessionId,
          lastUsedAt: now,
        });
        return tokens;
      },
    );

    // Thrown outside the transaction so the bulk revocation is committed.
    if (result === 'REUSE' || result === 'INVALID') throw invalidSession();
    return result;
  }

  async logout(sessionId: string): Promise<void> {
    await this.dataSource
      .getRepository(Session)
      .update(
        { id: sessionId, revokedAt: IsNull() },
        { revokedAt: new Date() },
      );
  }

  async logoutAll(userId: string): Promise<void> {
    await this.dataSource
      .getRepository(Session)
      .update({ userId, revokedAt: IsNull() }, { revokedAt: new Date() });
  }

  /** Checks that the access token's session is still valid (immediate logout). */
  async isSessionActive(sessionId: string, userId: string): Promise<boolean> {
    const session = await this.dataSource.getRepository(Session).findOne({
      where: { id: sessionId },
      relations: { user: true },
    });
    return (
      !!session &&
      session.userId === userId &&
      session.revokedAt === null &&
      session.expiresAt > new Date() &&
      session.user?.active === true
    );
  }

  private async issueSession(
    em: EntityManager,
    user: User,
    ctx: ClientContext,
  ): Promise<TokensResponseDto> {
    const secret = randomBytes(32).toString('base64url');
    const now = new Date();
    const session = await em.save(
      em.create(Session, {
        userId: user.id,
        refreshTokenHash: sha256(secret),
        userAgent: ctx.userAgent?.slice(0, 300) ?? null,
        ip: ctx.ip?.slice(0, 64) ?? null,
        expiresAt: new Date(
          now.getTime() + this.config.auth.refreshTtlDays * 86_400_000,
        ),
        lastUsedAt: now,
      }),
    );
    const accessToken = await this.jwt.signAsync(
      { sub: user.id, sid: session.id, role: user.role },
      { expiresIn: this.config.auth.accessTtlSeconds },
    );
    return {
      accessToken,
      refreshToken: `${session.id}.${secret}`,
      tokenType: 'Bearer',
      expiresIn: this.config.auth.accessTtlSeconds,
      user: { id: user.id, email: user.email, role: user.role },
    };
  }

  private secretMatches(secret: string, storedHash: string): boolean {
    const a = Buffer.from(sha256(secret));
    const b = Buffer.from(storedHash);
    return a.length === b.length && timingSafeEqual(a, b);
  }

  private requireActive(user: User): void {
    if (!user.active) {
      throw new ApiError(
        HttpStatus.FORBIDDEN,
        ErrorCode.INACTIVE_USER,
        'The account is disabled.',
      );
    }
  }

  private emailInUse(): ApiError {
    return new ApiError(
      HttpStatus.CONFLICT,
      ErrorCode.EMAIL_IN_USE,
      'An account with that email already exists.',
    );
  }
}
