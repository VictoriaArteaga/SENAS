import { Controller, Get, Param } from '@nestjs/common';
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
import { LevelDetailDto, LevelSummaryDto } from './dto/level.dto';
import { LevelsService } from './levels.service';

@ApiTags('Levels')
@ApiBearerAuth()
@Controller('levels')
export class LevelsController {
  constructor(private readonly levels: LevelsService) {}

  @Get()
  @ApiOperation({ summary: "The 4 levels with the player's state" })
  @ApiOkResponse({ type: [LevelSummaryDto] })
  @ApiErrors({ 401: 'UNAUTHORIZED | INVALID_SESSION' })
  list(@CurrentUser() u: AuthenticatedUser) {
    return this.levels.list(u.userId);
  }

  @Get(':code')
  @ApiOperation({ summary: 'A level with its signs in order and their status' })
  @ApiOkResponse({ type: LevelDetailDto })
  @ApiErrors({
    401: 'UNAUTHORIZED | INVALID_SESSION',
    404: 'LEVEL_NOT_FOUND',
  })
  detail(@CurrentUser() u: AuthenticatedUser, @Param('code') code: string) {
    return this.levels.detail(u.userId, code);
  }
}
