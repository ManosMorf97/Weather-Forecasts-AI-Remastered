import { PrismaClient } from '@prisma/client';
import { loadConfig } from '../config.js';

export type Db = PrismaClient;

// Constructed lazily inside main() (not at import time) so a bad config surfaces as a clean
// error rather than an unhandled throw during module evaluation. The datasource URL is
// assembled from YOUR_SERVER / YOUR_USER / YOUR_PASSWORD (see config.ts), so schema.prisma's
// env("DATABASE_URL") is only used by the Prisma CLI.
export function createPrismaClient(): PrismaClient {
  return new PrismaClient({ datasourceUrl: loadConfig().databaseUrl });
}
