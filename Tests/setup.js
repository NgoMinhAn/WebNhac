/**
 * Jest Setup File - Runs before all tests
 * Configure global test settings, mocks, and utilities
 */

// Set test environment
process.env.NODE_ENV = 'test';
process.env.DB_CONNECTION = 'test';

// Increase Jest timeout for integration tests
jest.setTimeout(10000);

// Mock console methods if needed (reduce noise in tests)
// Uncomment to suppress console output during tests
// global.console = {
//   ...console,
//   log: jest.fn(),
//   debug: jest.fn(),
//   info: jest.fn(),
//   warn: jest.fn(),
//   error: jest.fn(),
// };

// Global test utilities
global.testUtils = {
  // Generate test email
  generateTestEmail: () => `test-${Date.now()}@example.com`,

  // Generate test username
  generateTestUsername: () => `user-${Date.now()}`,

  // Generate test song ID
  generateTestSongId: () => Math.floor(Math.random() * 10000),

  // Generate test user ID
  generateTestUserId: () => Math.floor(Math.random() * 10000),

  // Sample test data
  sampleUser: {
    email: 'test@example.com',
    username: 'testuser',
    password: 'SecurePass123',
    confirmPassword: 'SecurePass123',
    role: 'User'
  },

  sampleSong: {
    name: 'Test Song',
    author: 'Test Artist',
    album: 'Test Album',
    genre: 'Rock',
    duration: '3:45'
  },

  samplePlaylist: {
    name: 'Test Playlist',
    description: 'A test playlist',
    isPrivate: false
  }
};

// Setup global error handlers for unhandled rejections
process.on('unhandledRejection', (reason, promise) => {
  console.error('Unhandled Rejection at:', promise, 'reason:', reason);
});

// Export for use in tests
module.exports = {};
