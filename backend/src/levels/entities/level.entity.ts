import {
  Check,
  Column,
  CreateDateColumn,
  Entity,
  OneToMany,
  PrimaryGeneratedColumn,
  UpdateDateColumn,
} from 'typeorm';
import type { LevelSign } from './level-sign.entity';

@Entity({ name: 'levels' })
@Check('chk_levels_position', 'position > 0')
@Check('chk_levels_required_xp', 'required_xp >= 0')
export class Level {
  @PrimaryGeneratedColumn('uuid')
  id: string;

  @Column({ name: 'code', type: 'varchar', length: 40, unique: true })
  code: string;

  @Column({ name: 'name', type: 'varchar', length: 80 })
  name: string;

  @Column({ name: 'description', type: 'text', nullable: true })
  description: string | null;

  @Column({ name: 'position', type: 'int', unique: true })
  position: number;

  @Column({ name: 'required_xp', type: 'int' })
  requiredXp: number;

  @Column({ name: 'active', type: 'boolean', default: true })
  active: boolean;

  @OneToMany('LevelSign', (ls: LevelSign) => ls.level)
  signs?: LevelSign[];

  @CreateDateColumn({ name: 'created_at', type: 'timestamptz' })
  createdAt: Date;

  @UpdateDateColumn({ name: 'updated_at', type: 'timestamptz' })
  updatedAt: Date;
}
