module.exports = {
  // Test environment
  testEnvironment: 'node',

  // Setup files - run before tests
  setupFilesAfterEnv: ['<rootDir>/Tests/setup.js'],

  // Test match patterns
  testMatch: [
    '**/Tests/**/*.test.js',
    '**/__tests__/**/*.js',
    '**/?(*.)+(spec|test).js'
  ],

  // Collection coverage from (only JS test files, not C# source)
  // Note: excludes test files themselves since we're testing C# production code
  collectCoverageFrom: [
    'Tests/**/*.js',
    '!**/node_modules/**',
    '!**/vendor/**',
    '!**/*.test.js',      // Exclude test files from coverage
    '!**/setup.js'        // Exclude setup file
  ],

  // Coverage thresholds - disabled for C# project with JavaScript tests
  // Re-enable this when testing JavaScript source code
  coverageThreshold: {
    global: {
      branches: 0,
      functions: 0,
      lines: 0,
      statements: 0
    }
  },

  // Timeout for tests (ms)
  testTimeout: 10000,

  // Verbose output
  verbose: true,

  // Clear mocks between tests
  clearMocks: true,

  // Restore mocks between tests
  restoreMocks: true,

  // Module name mapper (for path aliases)
  moduleNameMapper: {
    '^@/(.*)$': '<rootDir>/Server/ServerWeb/$1',
    '^@Controllers/(.*)$': '<rootDir>/Server/ServerWeb/Controllers/$1',
    '^@Models/(.*)$': '<rootDir>/Server/ServerWeb/Models/$1',
    '^@Data/(.*)$': '<rootDir>/Server/ServerWeb/Data/$1'
  }
};
