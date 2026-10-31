import { Controller, Get, Param, UseGuards } from '@nestjs/common';
import type { AuthenticatedUser } from '../auth/auth.types';
import { CurrentUser } from '../auth/decorators/current-user.decorator';
import { JwtAuthGuard } from '../auth/guards/jwt-auth.guard';
import { parseUuid } from '../common/pipes/parse-uuid.pipe';
import { DataResponse } from '../common/types/api-response';
import { LevelAccessService, PlayerLevelResponse } from './level-access.service';
import { LevelResponse, LevelsService } from './levels.service';

/** Catálogo de niveles: solo consulta y solo para usuarios autenticados. */
@Controller('levels')
@UseGuards(JwtAuthGuard)
export class LevelsController {
  constructor(
    private readonly levels: LevelsService,
    private readonly levelAccess: LevelAccessService,
  ) {}

  /**
   * Niveles con la disponibilidad del jugador autenticado. La identidad sale SOLO del JWT: la ruta no
   * acepta usuarioId por parámetros, consulta, cuerpo ni cabeceras.
   */
  @Get()
  async findAll(@CurrentUser() user: AuthenticatedUser): Promise<DataResponse<PlayerLevelResponse[]>> {
    return { data: await this.levelAccess.listForPlayer(user.userId) };
  }

  @Get(':id')
  async findOne(@Param('id', parseUuid) id: string): Promise<DataResponse<LevelResponse>> {
    return { data: await this.levels.findOne(id) };
  }
}
