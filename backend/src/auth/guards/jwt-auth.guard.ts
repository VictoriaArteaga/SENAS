import { Injectable } from '@nestjs/common';
import { AuthGuard } from '@nestjs/passport';

/** Uso futuro: @UseGuards(JwtAuthGuard) en los endpoints protegidos. */
@Injectable()
export class JwtAuthGuard extends AuthGuard('jwt') {}
