import type { Config } from 'jest';

const config: Config = {
  moduleFileExtensions: ['js', 'json', 'ts'],
  rootDir: '.',
  testRegex: 'src/.*\.spec\.ts$',
  transform: { '^.+\.(t|j)s$': 'ts-jest' },
  testEnvironment: 'node',
  collectCoverageFrom: [
    'src/**/*.ts',
    '!src/main.ts',
    '!src/**/*.module.ts',
    '!src/**/*.spec.ts',
    '!src/**/entities/*.ts',
    '!src/**/dto/*.ts',
  ],
  coverageDirectory: './coverage',
  // Minimum 80 % coverage on the evaluations, progress and auth services.
  coverageThreshold: {
    './src/evaluations/evaluations.service.ts': { statements: 80, branches: 80, functions: 80, lines: 80 },
    './src/progress/progress.service.ts': { statements: 80, branches: 80, functions: 80, lines: 80 },
    './src/auth/auth.service.ts': { statements: 80, branches: 80, functions: 80, lines: 80 },
  },
};

export default config;
