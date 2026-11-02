import {
  Check,
  Column,
  CreateDateColumn,
  Entity,
  JoinColumn,
  OneToOne,
  PrimaryGeneratedColumn,
  UpdateDateColumn,
} from 'typeorm';
import { User } from '../../users/entities/user.entity';

export interface Preferences {
  language?: 'es' | 'en';
  theme?: 'light' | 'dark';
}

@Entity({ name: 'profiles' })
@Check('chk_profiles_total_xp', 'total_xp >= 0')
@Check(
  'chk_profiles_display_name',
  'char_length(display_name) between 2 and 40',
)
export class Profile {
  @PrimaryGeneratedColumn('uuid')
  id: string;

  @Column({ name: 'user_id', type: 'uuid', unique: true })
  userId: string;

  @OneToOne(() => User, { onDelete: 'CASCADE', nullable: false })
  @JoinColumn({ name: 'user_id' })
  user?: User;

  @Column({ name: 'display_name', type: 'varchar', length: 40 })
  displayName: string;

  @Column({ name: 'avatar_id', type: 'varchar', length: 40 })
  avatarId: string;

  @Column({ name: 'avatar_config', type: 'jsonb', default: () => "'{}'" })
  avatarConfig: Record<string, unknown>;

  @Column({ name: 'preferences', type: 'jsonb', default: () => "'{}'" })
  preferences: Preferences;

  @Column({ name: 'total_xp', type: 'int', default: 0 })
  totalXp: number;

  @CreateDateColumn({ name: 'created_at', type: 'timestamptz' })
  createdAt: Date;

  @UpdateDateColumn({ name: 'updated_at', type: 'timestamptz' })
  updatedAt: Date;
}
