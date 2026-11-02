import {
  Body,
  Controller,
  HttpCode,
  HttpStatus,
  Post,
  Req,
} from '@nestjs/common';
import {
  ApiBearerAuth,
  ApiCreatedResponse,
  ApiNoContentResponse,
  ApiOkResponse,
  ApiOperation,
  ApiTags,
} from '@nestjs/swagger';
import type { Request } from 'express';
import { ApiErrors } from '../common/decorators/api-errors.decorator';
import {
  CurrentUser,
  type AuthenticatedUser,
} from '../common/decorators/current-user.decorator';
import { Public } from '../common/decorators/public.decorator';
import { StrictThrottle } from '../common/decorators/strict-throttle.decorator';
import { AuthService, type ClientContext } from './auth.service';
import {
  LoginDto,
  RefreshDto,
  RegisterDto,
  TokensResponseDto,
} from './dto/auth.dto';

const clientContext = (req: Request): ClientContext => ({
  ip: req.ip ?? null,
  userAgent: req.get('user-agent') ?? null,
});

@ApiTags('Auth')
@Controller('auth')
export class AuthController {
  constructor(private readonly auth: AuthService) {}

  @Public()
  @StrictThrottle()
  @Post('register')
  @ApiOperation({ summary: 'Player registration + tokens' })
  @ApiCreatedResponse({ type: TokensResponseDto })
  @ApiErrors({
    400: 'VALIDATION_FAILED | INVALID_AVATAR',
    409: 'EMAIL_IN_USE',
    429: 'TOO_MANY_REQUESTS',
  })
  register(@Body() dto: RegisterDto, @Req() req: Request) {
    return this.auth.register(dto, clientContext(req));
  }

  @Public()
  @StrictThrottle()
  @Post('login')
  @HttpCode(HttpStatus.OK)
  @ApiOperation({ summary: 'Sign in + tokens' })
  @ApiOkResponse({ type: TokensResponseDto })
  @ApiErrors({
    400: 'VALIDATION_FAILED',
    401: 'INVALID_CREDENTIALS',
    403: 'INACTIVE_USER',
    429: 'TOO_MANY_REQUESTS',
  })
  login(@Body() dto: LoginDto, @Req() req: Request) {
    return this.auth.login(dto, clientContext(req));
  }

  @Public()
  @StrictThrottle()
  @Post('refresh')
  @HttpCode(HttpStatus.OK)
  @ApiOperation({
    summary: 'Rotates the tokens',
    description:
      "Revokes the session used and returns a new pair. Reusing an already rotated refresh token revokes all of the user's sessions.",
  })
  @ApiOkResponse({ type: TokensResponseDto })
  @ApiErrors({
    400: 'VALIDATION_FAILED',
    401: 'INVALID_SESSION',
    403: 'INACTIVE_USER',
    429: 'TOO_MANY_REQUESTS',
  })
  refresh(@Body() dto: RefreshDto, @Req() req: Request) {
    return this.auth.refresh(dto.refreshToken, clientContext(req));
  }

  @Post('logout')
  @HttpCode(HttpStatus.NO_CONTENT)
  @ApiBearerAuth()
  @ApiOperation({ summary: 'Closes the current session' })
  @ApiNoContentResponse()
  @ApiErrors({ 401: 'UNAUTHORIZED | INVALID_SESSION' })
  async logout(@CurrentUser() u: AuthenticatedUser): Promise<void> {
    await this.auth.logout(u.sessionId);
  }

  @Post('logout-all')
  @HttpCode(HttpStatus.NO_CONTENT)
  @ApiBearerAuth()
  @ApiOperation({ summary: "Closes all of the user's sessions" })
  @ApiNoContentResponse()
  @ApiErrors({ 401: 'UNAUTHORIZED | INVALID_SESSION' })
  async logoutAll(@CurrentUser() u: AuthenticatedUser): Promise<void> {
    await this.auth.logoutAll(u.userId);
  }
}
