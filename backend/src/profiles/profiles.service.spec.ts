import { TEST_CONFIG } from '../../test/helpers/fake-db';
import { ApiError } from '../common/api-error';
import { PreferencesDto } from './dto/profile.dto';
import { Profile } from './entities/profile.entity';
import { ProfilesService } from './profiles.service';

function build(profile: Partial<Profile> | null) {
  const repo = {
    findOne: jest.fn().mockResolvedValue(profile),
    save: jest.fn(async (p: Profile) => p),
  };
  return { repo, svc: new ProfilesService(repo as never, TEST_CONFIG) };
}

const baseProfile = (): Partial<Profile> => ({
  userId: 'u1',
  displayName: 'Ana',
  avatarId: 'luna',
  avatarConfig: {},
  preferences: { language: 'es' },
  totalXp: 50,
  createdAt: new Date(),
  user: { email: 'ana@example.com' } as never,
});

const codeOf = async (p: Promise<unknown>) =>
  p.then(
    () => 'NO_ERROR',
    (e: ApiError) => e.code,
  );

describe('ProfilesService', () => {
  it('resolveAvatar uses the first avatar if none is given and rejects unknown ones', () => {
    const { svc } = build(null);
    expect(svc.resolveAvatar()).toBe('luna');
    expect(svc.resolveAvatar('rio')).toBe('rio');
    expect(() => svc.resolveAvatar('dragon')).toThrow(ApiError);
  });

  it('get returns the profile with email and XP', async () => {
    const { svc } = build(baseProfile());
    expect(await svc.get('u1')).toMatchObject({
      email: 'ana@example.com',
      displayName: 'Ana',
      totalXp: 50,
    });
  });

  it('get for a user without a profile → 404', async () => {
    const { svc } = build(null);
    expect(await codeOf(svc.get('u1'))).toBe('NOT_FOUND');
  });

  it('update merges preferences without losing the previous ones', async () => {
    const { svc, repo } = build(baseProfile());
    const r = await svc.update('u1', {
      displayName: 'Ana Maria',
      preferences: { theme: 'dark' },
    });
    expect(r.displayName).toBe('Ana Maria');
    expect(r.preferences).toEqual({ language: 'es', theme: 'dark' });
    expect(repo.save).toHaveBeenCalledTimes(1);
  });

  it('unsent keys (undefined in the transformed DTO) do not erase the stored preferences', async () => {
    const { svc } = build(baseProfile());
    // With class-transformer, declared fields arrive as "undefined" properties.
    const dto = Object.assign(new PreferencesDto(), {
      language: undefined,
      theme: 'dark',
    });
    const r = await svc.update('u1', { preferences: dto });
    expect(r.preferences).toEqual({ language: 'es', theme: 'dark' });
  });

  it('update with no fields changes nothing', async () => {
    const { svc } = build(baseProfile());
    expect(await svc.update('u1', {})).toMatchObject({
      displayName: 'Ana',
      preferences: { language: 'es' },
    });
  });

  it('updateAvatar changes the avatar and the configuration', async () => {
    const { svc } = build(baseProfile());
    const r = await svc.updateAvatar('u1', {
      avatarId: 'sol',
      avatarConfig: { color: 'red' },
    });
    expect(r).toMatchObject({
      avatarId: 'sol',
      avatarConfig: { color: 'red' },
    });
  });

  it('updateAvatar rejects an invalid avatar and an oversized configuration', async () => {
    const { svc } = build(baseProfile());
    expect(await codeOf(svc.updateAvatar('u1', { avatarId: 'dragon' }))).toBe(
      'INVALID_AVATAR',
    );
    expect(
      await codeOf(
        svc.updateAvatar('u1', { avatarConfig: { x: 'a'.repeat(5000) } }),
      ),
    ).toBe('INVALID_AVATAR_CONFIG');
  });

  it('create stores the trimmed name and the validated avatar', async () => {
    const { svc } = build(null);
    const em = {
      create: jest.fn((_c: unknown, d: unknown) => d),
      save: jest.fn(async (d: unknown) => d),
    };
    const p = await svc.create(em as never, 'u1', '  Ana  ', 'nube');
    expect(p).toMatchObject({
      userId: 'u1',
      displayName: 'Ana',
      avatarId: 'nube',
    });
  });
});
