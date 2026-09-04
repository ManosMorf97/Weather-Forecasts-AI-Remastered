import 'dotenv/config';
import { ZodError } from 'zod';
import { loadConfig } from './config.js';
import { createPrismaClient } from './db/client.js';
import { logger } from './logging/logger.js';
import { AppwriteUsersLookup } from './notifications/appwriteUsers.js';
import { buildEmailSender } from './notifications/emailSender.js';
import { buildProviderRegistry } from './providers/registry.js';
import { ForecastsRepository } from './repositories/forecastsRepository.js';
import { NotificationsRepository } from './repositories/notificationsRepository.js';
import { SelectionsRepository } from './repositories/selectionsRepository.js';
import { runCycle } from './pipeline/run.js';

// Run-once job. The external scheduler decides how often this process starts.
async function main(): Promise<number> {
  const config = loadConfig();
  const prisma = createPrismaClient();

  try {
    const summary = await runCycle({
      registry: buildProviderRegistry(config),
      selectionsRepo: new SelectionsRepository(prisma),
      forecastsRepo: new ForecastsRepository(prisma),
      notificationsRepo: new NotificationsRepository(prisma),
      users: new AppwriteUsersLookup(config),
      email: buildEmailSender(config),
    });

    logger.info(summary, 'poll cycle complete');
    return summary.allServicesFailed ? 1 : 0;
  } finally {
    await prisma.$disconnect().catch(() => undefined);
  }
}

main()
  .then((code) => process.exit(code))
  .catch((err) => {
    if (err instanceof ZodError) {
      const fields = err.issues.map((issue) => issue.path.join('.')).join(', ');
      logger.fatal(`invalid configuration - check these environment variables: ${fields}`);
    } else {
      logger.fatal({ err }, 'poll cycle crashed');
    }
    process.exit(1);
  });
