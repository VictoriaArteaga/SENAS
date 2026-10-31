import { Body, Controller, Get, HttpCode, HttpStatus, Post, UseGuards } from '@nestjs/common';
import { Throttle, ThrottlerGuard } from '@nestjs/throttler';
import { readRateLimitConfig } from '../config/rate-limit.config';
import { AuthService } from './auth.service';
import type { AuthenticatedUser } from './auth.types';
import {
  LoginResponse,
  LogoutAllResponse,
  MeResponse,
  RefreshResponse,
  RegisterResponse,
} from './auth.types';
import { CurrentUser } from './decorators/current-user.decorator';
import { LoginDto } from './dto/login.dto';
import { RefreshTokenDto } from './dto/refresh-token.dto';
import { RegisterDto } from './dto/register.dto';
import { JwtAuthGuard } from './guards/jwt-auth.guard';

// Límites por IP y por ruta, resueltos en cada petición (configurables por entorno).
const limit = (key: 'login' | 'register' | 'refresh') => () => readRateLimitConfig()[key];
const ttl = () => readRateLimitConfig().ttlMs;

@Controller('auth')
export class AuthController {
  constructor(private readonly auth: AuthService) {}

  @Post('register') // 201
  @UseGuards(ThrottlerGuard)
  @Throttle({ default: { limit: limit('register'), ttl } })
  register(@Body() dto: RegisterDto): Promise<RegisterResponse> {
    return this.auth.register(dto);
  }

  @Post('login')
  @HttpCode(HttpStatus.OK)
  @UseGuards(ThrottlerGuard)
  @Throttle({ default: { limit: limit('login'), ttl } })
  login(@Body() dto: LoginDto): Promise<LoginResponse> {
    return this.auth.login(dto);
  }

  @Post('refresh')
  @HttpCode(HttpStatus.OK)
  @UseGuards(ThrottlerGuard)
  @Throttle({ default: { limit: limit('refresh'), ttl } })
  refresh(@Body() dto: RefreshTokenDto): Promise<RefreshResponse> {
    return this.auth.refresh(dto);
  }

  /** Idempotente y sin cuerpo (204) tanto si la sesión se cerró como si ya no era válida. */
  @Post('logout')
  @HttpCode(HttpStatus.NO_CONTENT)
  async logout(@Body() dto: RefreshTokenDto): Promise<void> {
    await this.auth.logout(dto);
  }

  /** La identidad sale del JWT; el endpoint no acepta ningún id de usuario del cliente. */
  @Post('logout-all')
  @HttpCode(HttpStatus.OK)
  @UseGuards(JwtAuthGuard)
  logoutAll(@CurrentUser() user: AuthenticatedUser): Promise<LogoutAllResponse> {
    return this.auth.logoutAll(user.userId);
  }

  @Get('me')
  @UseGuards(JwtAuthGuard)
  me(@CurrentUser() user: AuthenticatedUser): Promise<MeResponse> {
    return this.auth.me(user.userId);
  }
}
