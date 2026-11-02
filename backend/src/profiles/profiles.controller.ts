import { Body, Controller, Get, Patch } from '@nestjs/common';
import {
  ApiBearerAuth,
  ApiOkResponse,
  ApiOperation,
  ApiTags,
} from '@nestjs/swagger';
import { ApiErrors } from '../common/decorators/api-errors.decorator';
import {
  CurrentUser,
  type AuthenticatedUser,
} from '../common/decorators/current-user.decorator';
import {
  ProfileResponseDto,
  UpdateAvatarDto,
  UpdateProfileDto,
} from './dto/profile.dto';
import { ProfilesService } from './profiles.service';

@ApiTags('Profile')
@ApiBearerAuth()
@Controller('profile')
export class ProfilesController {
  constructor(private readonly profiles: ProfilesService) {}

  @Get()
  @ApiOperation({ summary: 'Player profile, with email and total XP' })
  @ApiOkResponse({ type: ProfileResponseDto })
  @ApiErrors({ 401: 'UNAUTHORIZED | INVALID_SESSION', 404: 'NOT_FOUND' })
  get(@CurrentUser() u: AuthenticatedUser) {
    return this.profiles.get(u.userId);
  }

  @Patch()
  @ApiOperation({ summary: 'Updates displayName and preferences' })
  @ApiOkResponse({ type: ProfileResponseDto })
  @ApiErrors({
    400: 'VALIDATION_FAILED',
    401: 'UNAUTHORIZED | INVALID_SESSION',
  })
  update(@CurrentUser() u: AuthenticatedUser, @Body() dto: UpdateProfileDto) {
    return this.profiles.update(u.userId, dto);
  }

  @Patch('avatar')
  @ApiOperation({ summary: 'Updates avatarId and avatarConfig' })
  @ApiOkResponse({ type: ProfileResponseDto })
  @ApiErrors({
    400: 'VALIDATION_FAILED | INVALID_AVATAR | INVALID_AVATAR_CONFIG',
    401: 'UNAUTHORIZED | INVALID_SESSION',
  })
  updateAvatar(
    @CurrentUser() u: AuthenticatedUser,
    @Body() dto: UpdateAvatarDto,
  ) {
    return this.profiles.updateAvatar(u.userId, dto);
  }
}
