// Las pruebas nunca deben leer .env ni conectarse a la base real.
process.env.NODE_ENV = 'test';
process.env.DATABASE_URL = process.env.TEST_DATABASE_URL ?? 'postgresql://invalid:invalid@127.0.0.1:1/invalid';
process.env.JWT_ACCESS_SECRET = 'test-secret-only-for-jest-0123456789abcdef0123456789';
process.env.JWT_ACCESS_EXPIRES_IN = '15m';
process.env.REFRESH_TOKEN_EXPIRES_IN = '30d';
delete process.env.MIGRATION_DATABASE_URL;
// Límites altos por defecto para que las pruebas no se bloqueen entre sí; la prueba de rate limiting
// los baja explícitamente.
process.env.RATE_LIMIT_TTL_SECONDS = '60';
process.env.RATE_LIMIT_LOGIN = '100000';
process.env.RATE_LIMIT_REGISTER = '100000';
process.env.RATE_LIMIT_REFRESH = '100000';
