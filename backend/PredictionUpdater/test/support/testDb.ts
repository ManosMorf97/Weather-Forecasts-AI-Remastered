import { spawnSync } from 'node:child_process';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { PrismaMssql } from '@prisma/adapter-mssql';
import { MSSQLServerContainer } from '@testcontainers/mssqlserver';
import { buildSqlServerUrl } from '../../src/db/connectionUrl.js';
import { PrismaClient } from '../../src/generated/prisma/client.js';

const packageRoot = resolve(dirname(fileURLToPath(import.meta.url)), '../..');

// Pinned rather than the container library's default so CI and dev pull the same (large) image
// and share the local cache.
const MSSQL_IMAGE = 'mcr.microsoft.com/mssql/server:2022-latest';

export interface TestDb {
  prisma: PrismaClient;
  /** Disconnect the client and stop the container. Call in afterAll. */
  stop: () => Promise<void>;
}

// Spins up a throwaway SQL Server, creates the schema in it with `prisma db push`, and hands
// back a client pointed at it. Requires Docker. The container's `master` database is used
// directly - it is discarded when the container stops, so there is nothing to isolate from.
export async function startTestDb(): Promise<TestDb> {
  const container = await new MSSQLServerContainer(MSSQL_IMAGE).acceptLicense().start();

  // The CLI still speaks Prisma's `sqlserver://` URL; prisma.config.ts picks DATABASE_URL up
  // ahead of the ambient YOUR_DATABASE_SERVER parts.
  const databaseUrl = buildSqlServerUrl({
    server: `${container.getHost()},${container.getPort()}`,
    user: container.getUsername(),
    password: container.getPassword(),
    database: 'master',
  });

  const push = spawnSync('npx', ['prisma', 'db', 'push', '--accept-data-loss'], {
    cwd: packageRoot,
    env: {
      ...process.env,
      DATABASE_URL: databaseUrl,
      // Prisma 7 blocks `db push` when it detects an AI-agent shell (CLAUDECODE / GEMINI_CLI /
      // ...). This push only ever targets the throwaway container created just above, so opt
      // out via Prisma's documented env var.
      PRISMA_USER_CONSENT_FOR_DANGEROUS_AI_ACTION:
        'Testcontainers ephemeral SQL Server for integration tests (test/support/testDb.ts)',
    },
    encoding: 'utf8',
    shell: true,
  });
  if (push.status !== 0) {
    await container.stop();
    throw new Error(`prisma db push failed:\n${push.stdout ?? ''}\n${push.stderr ?? ''}`);
  }

  // Prisma 7: the runtime client connects through the driver adapter, not a URL.
  const prisma = new PrismaClient({
    adapter: new PrismaMssql({
      server: container.getHost(),
      port: container.getPort(),
      database: 'master',
      user: container.getUsername(),
      password: container.getPassword(),
      options: { encrypt: true, trustServerCertificate: true },
    }),
  });
  return {
    prisma,
    stop: async () => {
      await prisma.$disconnect().catch(() => undefined);
      await container.stop();
    },
  };
}

// Delete every row, children before parents, so each test starts from an empty database.
export async function truncateAll(prisma: PrismaClient): Promise<void> {
  await prisma.notification.deleteMany();
  await prisma.forecast.deleteMany();
  await prisma.userCitySite.deleteMany();
  await prisma.userService.deleteMany();
  await prisma.citySite.deleteMany();
  await prisma.city.deleteMany();
  await prisma.forecastingService.deleteMany();
  await prisma.user.deleteMany();
}
