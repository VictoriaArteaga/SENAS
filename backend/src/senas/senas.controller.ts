import { Controller, Get, Param, UseGuards } from '@nestjs/common';
import type { AuthenticatedUser } from '../auth/auth.types';
import { CurrentUser } from '../auth/decorators/current-user.decorator';
import { JwtAuthGuard } from '../auth/guards/jwt-auth.guard';
import { parseUuid } from '../common/pipes/parse-uuid.pipe';
import { DataResponse } from '../common/types/api-response';
import { LevelSenaResponse, SenaResponse, SenasService } from './senas.service';

/**
 * GET /levels/:id/senas — señas de un nivel, en el orden definido por NivelSena. Solo si el jugador (por
 * su JWT) tiene el nivel desbloqueado: 403 `LEVEL_LOCKED` si no, 404 si el nivel no existe.
 */
@Controller('levels')
@UseGuards(JwtAuthGuard)
export class LevelSenasController {
  constructor(private readonly senas: SenasService) {}

  @Get(':id/senas')
  async findByLevel(
    @Param('id', parseUuid) id: string,
    @CurrentUser() user: AuthenticatedUser,
  ): Promise<DataResponse<LevelSenaResponse[]>> {
    return { data: await this.senas.findByLevelForPlayer(user.userId, id) };
  }
}

/**
 * GET /senas/:id — detalle de una seña. Accesible si al menos uno de sus niveles está desbloqueado para el
 * jugador del JWT; 403 `LEVEL_LOCKED` si todos están bloqueados; 404 si no existe o no tiene niveles.
 */
@Controller('senas')
@UseGuards(JwtAuthGuard)
export class SenasController {
  constructor(private readonly senas: SenasService) {}

  @Get(':id')
  async findOne(
    @Param('id', parseUuid) id: string,
    @CurrentUser() user: AuthenticatedUser,
  ): Promise<DataResponse<SenaResponse>> {
    return { data: await this.senas.findOneForPlayer(user.userId, id) };
  }
}
