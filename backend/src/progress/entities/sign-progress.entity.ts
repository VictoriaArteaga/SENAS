import {
  Check,
  Column,
  CreateDateColumn,
  Entity,
  JoinColumn,
  ManyToOne,
  PrimaryGeneratedColumn,
  Unique,
  UpdateDateColumn,
} from 'typeorm';
import { NumericTransformer } from '../../common/numeric.transformer';
import { Sign } from '../../signs/entities/sign.entity';
import { User } from '../../users/entities/user.entity';

@Entity({ name: 'sign_progress' })
@Unique('ux_sign_progress_user_sign', ['userId', 'signId'])
@Check(
  'chk_sign_progress_precision_0_100',
  'best_precision is null or best_precision between 0 and 100',
)
@Check('chk_sign_progress_stars', 'best_stars between 0 and 3')
@Check('chk_sign_progress_xp', 'earned_xp between 0 and 30')
@Check(
  'chk_sign_progress_attempts',
  'total_attempts >= 0 and passed_attempts >= 0',
)
export class SignProgress {
  @PrimaryGeneratedColumn('uuid')
  id: string;

  @Column({ name: 'user_id', type: 'uuid' })
  userId: string;

  @ManyToOne(() => User, { onDelete: 'CASCADE', nullable: false })
  @JoinColumn({ name: 'user_id' })
  user?: User;

  @Column({ name: 'sign_id', type: 'uuid' })
  signId: string;

  @ManyToOne(() => Sign, { nullable: false })
  @JoinColumn({ name: 'sign_id' })
  sign?: Sign;

  @Column({ name: 'total_attempts', type: 'int', default: 0 })
  totalAttempts: number;

  @Column({ name: 'passed_attempts', type: 'int', default: 0 })
  passedAttempts: number;

  @Column({
    name: 'best_precision',
    type: 'numeric',
    precision: 5,
    scale: 2,
    nullable: true,
    transformer: NumericTransformer,
  })
  bestPrecision: number | null;

  @Column({ name: 'best_stars', type: 'smallint', default: 0 })
  bestStars: number;

  @Column({ name: 'earned_xp', type: 'int', default: 0 })
  earnedXp: number;

  @Column({ name: 'passed', type: 'boolean', default: false })
  passed: boolean;

  @Column({ name: 'first_passed_at', type: 'timestamptz', nullable: true })
  firstPassedAt: Date | null;

  @Column({ name: 'last_attempt_at', type: 'timestamptz', nullable: true })
  lastAttemptAt: Date | null;

  @CreateDateColumn({ name: 'created_at', type: 'timestamptz' })
  createdAt: Date;

  @UpdateDateColumn({ name: 'updated_at', type: 'timestamptz' })
  updatedAt: Date;
}
