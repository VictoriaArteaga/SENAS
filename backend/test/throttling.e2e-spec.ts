import { INestApplication } from '@nestjs/common';
import request from 'supertest';
import { API, createApp } from './helpers/app';

// Low strict limit so it can be reached (setup-env.ts leaves it very high).
process.env['THROTTLE_AUTH_LIMIT'] = '3';

describe('Throttling (e2e)', () => {
  let app: INestApplication;

  beforeAll(async () => {
    app = await createApp();
  });
  afterAll(async () => {
    await app.close();
  });

  it('login has a strict limit → 429', async () => {
    const http = app.getHttpServer();
    const credentials = { email: 'nobody@example.com', password: 'Wrong1234' };

    // The first 3 reach the service (401); the 4th is cut by the throttler.
    for (let i = 0; i < 3; i++) {
      await request(http)
        .post(`${API}/auth/login`)
        .send(credentials)
        .expect(401);
    }
    const blocked = await request(http)
      .post(`${API}/auth/login`)
      .send(credentials)
      .expect(429);
    expect(blocked.body).toMatchObject({
      statusCode: 429,
      code: 'TOO_MANY_REQUESTS',
    });
  });

  it('the strict limit does not affect normal routes or health', async () => {
    const http = app.getHttpServer();
    for (let i = 0; i < 10; i++) {
      await request(http).get(`${API}/health`).expect(200);
    }
    const res = await request(http)
      .get(`${API}/levels`)
      .set('Authorization', 'Bearer x')
      .expect(401);
    expect(res.body.code).toBe('UNAUTHORIZED');
  });

  it('each strictly limited route keeps its own counter (register apart from login)', async () => {
    const http = app.getHttpServer();
    // Invalid bodies: they reach the throttler (guards run before validation) and return 400.
    for (let i = 0; i < 3; i++) {
      await request(http).post(`${API}/auth/register`).send({}).expect(400);
    }
    const blocked = await request(http)
      .post(`${API}/auth/register`)
      .send({})
      .expect(429);
    expect(blocked.body.code).toBe('TOO_MANY_REQUESTS');
  });
});
