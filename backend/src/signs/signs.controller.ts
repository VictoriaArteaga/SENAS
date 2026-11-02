import { Controller, Get, Param, Query } from '@nestjs/common';
import {
  ApiBearerAuth,
  ApiOkResponse,
  ApiOperation,
  ApiTags,
} from '@nestjs/swagger';
import { ApiErrors } from '../common/decorators/api-errors.decorator';
import { SignFilterDto, SignResponseDto } from './dto/sign.dto';
import { SignsService } from './signs.service';

@ApiTags('Signs')
@ApiBearerAuth()
@Controller('signs')
export class SignsController {
  constructor(private readonly signs: SignsService) {}

  @Get()
  @ApiOperation({
    summary: 'Sign catalog, with optional type and level filters',
  })
  @ApiOkResponse({ type: [SignResponseDto] })
  @ApiErrors({
    400: 'VALIDATION_FAILED',
    401: 'UNAUTHORIZED | INVALID_SESSION',
  })
  list(@Query() filter: SignFilterDto) {
    return this.signs.list(filter);
  }

  @Get(':code')
  @ApiOperation({ summary: 'Details of a sign' })
  @ApiOkResponse({ type: SignResponseDto })
  @ApiErrors({
    401: 'UNAUTHORIZED | INVALID_SESSION',
    404: 'SIGN_NOT_FOUND',
  })
  detail(@Param('code') code: string) {
    return this.signs.detail(code);
  }
}
