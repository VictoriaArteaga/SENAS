import {
  Column,
  CreateDateColumn,
  Entity,
  Index,
  PrimaryGeneratedColumn,
  UpdateDateColumn,
} from 'typeorm';

export enum Role {
  PLAYER = 'PLAYER',
  ADMIN = 'ADMIN',
}

@Entity({ name: 'users' })
@Index('ux_users_email', ['email'], { unique: true })
export class User {
  @PrimaryGeneratedColumn('uuid')
  id: string;

  /** Always lowercase (normalized in the application). */
  @Column({ name: 'email', type: 'varchar', length: 254 })
  email: string;

  @Column({ name: 'password_hash', type: 'varchar', length: 255 })
  passwordHash: string;

  @Column({
    name: 'role',
    type: 'enum',
    enum: Role,
    enumName: 'user_role',
    default: Role.PLAYER,
  })
  role: Role;

  @Column({ name: 'active', type: 'boolean', default: true })
  active: boolean;

  @Column({ name: 'last_access_at', type: 'timestamptz', nullable: true })
  lastAccessAt: Date | null;

  @CreateDateColumn({ name: 'created_at', type: 'timestamptz' })
  createdAt: Date;

  @UpdateDateColumn({ name: 'updated_at', type: 'timestamptz' })
  updatedAt: Date;
}
