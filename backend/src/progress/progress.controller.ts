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
import { ProgressSummaryDto, SignProgressDto } from './dto/progress.dto';
import { ProgressService } from './progress.service';

@ApiTags('Progress')
@ApiBearerAuth()
@Controller('progress')
export class ProgressController {
  constructor(private readonly progress: ProgressService) {}

  @Get()
  @ApiOperation({ summary: "Summary of the player's progress" })
  @ApiOkResponse({ type: ProgressSummaryDto })
  @ApiErrors({ 401: 'UNAUTHORIZED | INVALID_SESSION' })
  summary(@CurrentUser() u: AuthenticatedUser) {
    return this.progress.summary(u.userId);
  }

  @Get('signs/:code')
  @ApiOperation({ summary: "The player's progress on one sign" })
  @ApiOkResponse({ type: SignProgressDto })
  @ApiErrors({
    401: 'UNAUTHORIZED | INVALID_SESSION',
    404: 'SIGN_NOT_FOUND',
  })
  bySign(@CurrentUser() u: AuthenticatedUser, @Param('code') code: string) {
    return this.progress.bySign(u.userId, code);
  }
}
