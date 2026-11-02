import { HttpStatus, Injectable } from '@nestjs/common';
import { InjectRepository } from '@nestjs/typeorm';
import { Raw, Repository } from 'typeorm';
import { ApiError, ErrorCode } from '../common/api-error';
import { LevelSign } from '../levels/entities/level-sign.entity';
import { SignFilterDto, SignResponseDto } from './dto/sign.dto';

@Injectable()
export class SignsService {
  constructor(
    @InjectRepository(LevelSign)
    private readonly levelSigns: Repository<LevelSign>,
  ) {}

  async list(filter: SignFilterDto): Promise<SignResponseDto[]> {
    const rows = await this.levelSigns.find({
      where: {
        sign: { active: true, ...(filter.type ? { type: filter.type } : {}) },
        level: {
          active: true,
          ...(filter.level ? { code: filter.level.toUpperCase() } : {}),
        },
      },
      relations: { level: true, sign: true },
      order: { level: { position: 'ASC' }, position: 'ASC' },
    });
    return this.group(rows);
  }

  async detail(code: string): Promise<SignResponseDto> {
    const rows = await this.levelSigns.find({
      where: {
        sign: {
          code: Raw((alias) => `lower(${alias}) = lower(:code)`, { code }),
          active: true,
        },
        level: { active: true },
      },
      relations: { level: true, sign: true },
      order: { level: { position: 'ASC' } },
    });
    const [sign] = this.group(rows);
    if (!sign) {
      throw new ApiError(
        HttpStatus.NOT_FOUND,
        ErrorCode.SIGN_NOT_FOUND,
        'The sign does not exist.',
      );
    }
    return sign;
  }

  /** A sign can be in several levels: merged into a single element. */
  private group(rows: LevelSign[]): SignResponseDto[] {
    const byCode = new Map<string, SignResponseDto>();
    for (const r of rows) {
      if (!r.sign || !r.level) continue;
      let dto = byCode.get(r.sign.code);
      if (!dto) {
        dto = {
          code: r.sign.code,
          name: r.sign.name,
          type: r.sign.type,
          description: r.sign.description,
          levels: [],
        };
        byCode.set(r.sign.code, dto);
      }
      dto.levels.push({ level: r.level.code, position: r.position });
    }
    return [...byCode.values()];
  }
}
