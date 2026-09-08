import { PrismaMssql } from '@prisma/adapter-mssql';
import { PrismaClient } from '../generated/prisma/client.js';
import { buildMssqlConfig, type SqlServerParts } from './connectionUrl.js';

export type Db = PrismaClient;

// Constructed lazily inside main() (not at import time) so a bad config surfaces as a clean
// error rather than an unhandled throw during module evaluation. Prisma 7 connects through a
// driver adapter, not a URL - the mssql config is assembled from YOUR_SERVER / YOUR_USER /
// YOUR_PASSWORD (see connectionUrl.ts). schema.prisma / prisma.config.ts are CLI-only.
export function createPrismaClient(parts: SqlServerParts): PrismaClient {
  return new PrismaClient({ adapter: new PrismaMssql(buildMssqlConfig(parts)) });
}
