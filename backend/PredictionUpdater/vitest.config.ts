import { defineConfig } from 'vitest/config';

export default defineConfig({
  test: {
    include: ['test/**/*.test.ts'],
    // DB-backed pipeline tests spin up a SQL Server container - give them room.
    testTimeout: 120_000,
    hookTimeout: 120_000,
  },
});
