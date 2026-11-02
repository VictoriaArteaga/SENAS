import { HttpStatus, Inject, Injectable } from '@nestjs/common';
import { InjectRepository } from '@nestjs/typeorm';
import { EntityManager, Repository } from 'typeorm';
import { ApiError, ErrorCode } from '../common/api-error';
import { APP_CONFIG, type AppConfig } from '../config/configuration';
import { User } from '../users/entities/user.entity';
import {
  ProfileResponseDto,
  UpdateAvatarDto,
  UpdateProfileDto,
} from './dto/profile.dto';
import { Profile } from './entities/profile.entity';

const MAX_AVATAR_CONFIG_BYTES = 4096;

@Injectable()
export class ProfilesService {
  constructor(
    @InjectRepository(Profile) private readonly profiles: Repository<Profile>,
    @Inject(APP_CONFIG) private readonly config: AppConfig,
  ) {}

  /** Validates the avatar against AVATAR_IDS; without one, the first in the list is used. */
  resolveAvatar(avatarId?: string): string {
    if (avatarId === undefined) return this.config.avatarIds[0];
    if (!this.config.avatarIds.includes(avatarId)) {
      throw new ApiError(
        HttpStatus.BAD_REQUEST,
        ErrorCode.INVALID_AVATAR,
        `The avatar does not exist. Options: ${this.config.avatarIds.join(', ')}.`,
      );
    }
    return avatarId;
  }

  create(
    em: EntityManager,
    userId: string,
    displayName: string,
    avatarId?: string,
  ): Promise<Profile> {
    return em.save(
      em.create(Profile, {
        userId,
        displayName: displayName.trim(),
        avatarId: this.resolveAvatar(avatarId),
      }),
    );
  }

  async get(userId: string): Promise<ProfileResponseDto> {
    return this.toDto(await this.load(userId));
  }

  async update(
    userId: string,
    dto: UpdateProfileDto,
  ): Promise<ProfileResponseDto> {
    const profile = await this.load(userId);
    if (dto.displayName !== undefined) profile.displayName = dto.displayName;
    if (dto.preferences !== undefined) {
      // Partial PATCH: merged with what is stored. The transformed DTO carries
      // unsent keys as `undefined`; they must not overwrite stored values.
      const sent = Object.fromEntries(
        Object.entries(dto.preferences).filter(([, v]) => v !== undefined),
      );
      profile.preferences = { ...profile.preferences, ...sent };
    }
    return this.toDto(await this.profiles.save(profile));
  }

  async updateAvatar(
    userId: string,
    dto: UpdateAvatarDto,
  ): Promise<ProfileResponseDto> {
    const profile = await this.load(userId);
    if (dto.avatarId !== undefined) {
      profile.avatarId = this.resolveAvatar(dto.avatarId);
    }
    if (dto.avatarConfig !== undefined) {
      if (
        Buffer.byteLength(JSON.stringify(dto.avatarConfig)) >
        MAX_AVATAR_CONFIG_BYTES
      ) {
        throw new ApiError(
          HttpStatus.BAD_REQUEST,
          ErrorCode.INVALID_AVATAR_CONFIG,
          `avatarConfig cannot exceed ${MAX_AVATAR_CONFIG_BYTES} bytes.`,
        );
      }
      profile.avatarConfig = dto.avatarConfig;
    }
    return this.toDto(await this.profiles.save(profile));
  }

  private async load(userId: string): Promise<Profile & { user: User }> {
    const profile = await this.profiles.findOne({
      where: { userId },
      relations: { user: true },
    });
    if (!profile?.user) {
      throw new ApiError(
        HttpStatus.NOT_FOUND,
        ErrorCode.NOT_FOUND,
        'The profile does not exist.',
      );
    }
    return profile as Profile & { user: User };
  }

  private toDto(profile: Profile & { user?: User }): ProfileResponseDto {
    return {
      userId: profile.userId,
      email: profile.user?.email ?? '',
      displayName: profile.displayName,
      avatarId: profile.avatarId,
      avatarConfig: profile.avatarConfig,
      preferences: profile.preferences,
      totalXp: profile.totalXp,
      createdAt: profile.createdAt,
    };
  }
}
