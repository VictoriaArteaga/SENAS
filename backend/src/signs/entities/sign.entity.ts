import {
  Column,
  CreateDateColumn,
  Entity,
  PrimaryGeneratedColumn,
  UpdateDateColumn,
} from 'typeorm';

export enum SignType {
  LETTER = 'LETTER',
  NUMBER = 'NUMBER',
  MONTH = 'MONTH',
  WORD = 'WORD',
  PHRASE = 'PHRASE',
}

@Entity({ name: 'signs' })
export class Sign {
  @PrimaryGeneratedColumn('uuid')
  id: string;

  @Column({ name: 'code', type: 'varchar', length: 60, unique: true })
  code: string;

  @Column({ name: 'name', type: 'varchar', length: 80 })
  name: string;

  @Column({
    name: 'type',
    type: 'enum',
    enum: SignType,
    enumName: 'sign_type',
  })
  type: SignType;

  @Column({ name: 'description', type: 'text', nullable: true })
  description: string | null;

  @Column({ name: 'active', type: 'boolean', default: true })
  active: boolean;

  @CreateDateColumn({ name: 'created_at', type: 'timestamptz' })
  createdAt: Date;

  @UpdateDateColumn({ name: 'updated_at', type: 'timestamptz' })
  updatedAt: Date;
}
