import { INestApplication } from '@nestjs/common';
import { Test } from '@nestjs/testing';
import { randomUUID } from 'node:crypto';
import request from 'supertest';
import { AppModule } from '../../src/app.module';
import { configureApp } from '../../src/app.setup';

export async function createApp(): Promise<INestApplication> {
  const moduleRef = await Test.createTestingModule({
    imports: [AppModule],
  }).compile();
  const app = moduleRef.createNestApplication();
  configureApp(app);
  await app.init();
  return app;
}

export const API = '/api/v1';

export interface TestSession {
  email: string;
  password: string;
  accessToken: string;
  refreshToken: string;
  userId: string;
}

export async function registerUser(
  app: INestApplication,
  name = 'Player',
): Promise<TestSession> {
  const email = `${name.toLowerCase()}-${randomUUID().slice(0, 8)}@example.com`;
  const password = 'Lumina2026';
  const res = await request(app.getHttpServer())
    .post(`${API}/auth/register`)
    .send({ email, password, displayName: name, avatarId: 'luna' })
    .expect(201);
  return {
    email,
    password,
    accessToken: res.body.accessToken,
    refreshToken: res.body.refreshToken,
    userId: res.body.user.id,
  };
}

export const auth = (token: string) => ({ Authorization: `Bearer ${token}` });

export function attemptBody(
  signCode: string,
  extra: Record<string, unknown> = {},
) {
  return {
    attemptId: randomUUID(),
    signCode,
    precision: 82,
    stars: 2,
    xp: 20,
    durationMs: 4200,
    feedback: 'Good job',
    ...extra,
  };
}

export const THREE_STARS = { precision: 95, stars: 3, xp: 30 };
