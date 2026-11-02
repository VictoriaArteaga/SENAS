// Environment for the e2e tests: the local PostgreSQL from docker-compose.test.yml.
// NODE_ENV=test makes the app NOT read backend/.env, so these tests can never
// touch the real Supabase database.
const e = process.env;
e['NODE_ENV'] = 'test';
e['PORT'] = '0';
e['SWAGGER_ENABLED'] = 'true';
e['DB_URL'] =
  `postgresql://${e['E2E_DB_HOST'] ?? 'localhost'}:${e['E2E_DB_PORT'] ?? '54329'}/senas_test`;
e['DB_USER'] = 'senas';
e['DB_PASSWORD'] = 'senas_test';
e['DB_SSL'] = 'false';
e['DB_POOL_MAX'] = '10';
e['DB_SYNCHRONIZE'] = 'true';
e['DB_SEED_ON_START'] = 'true';
e['JWT_ACCESS_SECRET'] = 'e2e-secret-with-more-than-thirty-two-characters';
e['JWT_ACCESS_TTL'] = '15m';
e['REFRESH_TOKEN_TTL_DAYS'] = '30';
e['THROTTLE_LIMIT'] = '100000';
e['THROTTLE_AUTH_LIMIT'] = '100000';
