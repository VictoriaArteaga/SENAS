import {
  Body,
  Controller,
  Get,
  HttpStatus,
  Post,
  Query,
  Res,
} from '@nestjs/common';
import {
  ApiBearerAuth,
  ApiCreatedResponse,
  ApiOkResponse,
  ApiOperation,
  ApiTags,
} from '@nestjs/swagger';
import type { Response } from 'express';
import { ApiErrors } from '../common/decorators/api-errors.decorator';
import {
  CurrentUser,
  type AuthenticatedUser,
} from '../common/decorators/current-user.decorator';
import { CreateEvaluationDto } from './dto/create-evaluation.dto';
import { EvaluationResponseDto } from './dto/evaluation-response.dto';
import { HistoryFilterDto, HistoryResponseDto } from './dto/history.dto';
import { EvaluationsService } from './evaluations.service';

@ApiTags('Evaluations')
@ApiBearerAuth()
@Controller('evaluations')
export class EvaluationsController {
  constructor(private readonly evaluations: EvaluationsService) {}

  @Post()
  @ApiOperation({
    summary: 'Records the result of an attempt',
    description:
      'Idempotent per (user, attemptId): resubmitting the same attempt with the same content returns 200 with the original response; with different content, 409.',
  })
  @ApiCreatedResponse({
    type: EvaluationResponseDto,
    description: 'First time the attempt is received.',
  })
  @ApiOkResponse({
    type: EvaluationResponseDto,
    description: 'Identical retry: same response, no changes.',
  })
  @ApiErrors({
    400: 'VALIDATION_FAILED',
    401: 'UNAUTHORIZED | INVALID_SESSION',
    403: 'LEVEL_LOCKED | SIGN_LOCKED',
    404: 'SIGN_NOT_FOUND',
    409: 'DUPLICATE_ATTEMPT',
    422: 'INCONSISTENT_RESULT',
  })
  async register(
    @CurrentUser() u: AuthenticatedUser,
    @Body() dto: CreateEvaluationDto,
    @Res({ passthrough: true }) res: Response,
  ): Promise<EvaluationResponseDto> {
    const { response, created } = await this.evaluations.register(
      u.userId,
      dto,
    );
    res.status(created ? HttpStatus.CREATED : HttpStatus.OK);
    return response;
  }

  @Get()
  @ApiOperation({ summary: "Paginated history of the player's attempts" })
  @ApiOkResponse({ type: HistoryResponseDto })
  @ApiErrors({
    400: 'VALIDATION_FAILED',
    401: 'UNAUTHORIZED | INVALID_SESSION',
  })
  history(
    @CurrentUser() u: AuthenticatedUser,
    @Query() filter: HistoryFilterDto,
  ) {
    return this.evaluations.history(u.userId, filter);
  }
}
