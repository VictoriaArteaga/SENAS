import {
  Check,
  Column,
  CreateDateColumn,
  Entity,
  JoinColumn,
  ManyToOne,
  PrimaryGeneratedColumn,
  Unique,
} from 'typeorm';
import { Sign } from '../../signs/entities/sign.entity';
import { Level } from './level.entity';

@Entity({ name: 'level_signs' })
@Unique('ux_level_signs_level_sign', ['levelId', 'signId'])
@Unique('ux_level_signs_level_position', ['levelId', 'position'])
@Check('chk_level_signs_position', 'position > 0')
export class LevelSign {
  @PrimaryGeneratedColumn('uuid')
  id: string;

  @Column({ name: 'level_id', type: 'uuid' })
  levelId: string;

  @ManyToOne(() => Level, (l) => l.signs, { nullable: false })
  @JoinColumn({ name: 'level_id' })
  level?: Level;

  @Column({ name: 'sign_id', type: 'uuid' })
  signId: string;

  @ManyToOne(() => Sign, { nullable: false })
  @JoinColumn({ name: 'sign_id' })
  sign?: Sign;

  @Column({ name: 'position', type: 'int' })
  position: number;

  @CreateDateColumn({ name: 'created_at', type: 'timestamptz' })
  createdAt: Date;
}
