import { logger } from '../logging/logger.js';
import type { EmailSender } from '../notifications/emailSender.js';
import type { UsersLookup } from '../notifications/appwriteUsers.js';
import type { ForecastsRepository } from '../repositories/forecastsRepository.js';
import type { NotificationsRepository } from '../repositories/notificationsRepository.js';
import type { SelectionsRepository } from '../repositories/selectionsRepository.js';
import type { WeatherProvider } from '../providers/types.js';
import { pollForecasts, type PollSummary } from './poll.js';
import { storeForecasts, type StoreSummary } from './store.js';
import { notifyDangerForecasts, type NotifySummary } from './notify.js';

export interface RunDeps {
  registry: Map<string, WeatherProvider>;
  selectionsRepo: SelectionsRepository;
  forecastsRepo: ForecastsRepository;
  notificationsRepo: NotificationsRepository;
  users: UsersLookup;
  email: EmailSender;
}

export interface RunSummary {
  selections: number;
  poll?: PollSummary;
  store?: StoreSummary;
  notify?: NotifySummary;
  // E1: every attempted service failed this cycle.
  allServicesFailed: boolean;
}

// One full UC11 cycle: read selections -> poll -> store -> notify. Intended to be invoked as
// a run-once job by an external scheduler (cron / k8s CronJob / cloud scheduler).
export async function runCycle(deps: RunDeps): Promise<RunSummary> {
  const selections = await deps.selectionsRepo.getServiceSelections();

  if (selections.length === 0) {
    // A3: nobody has a (city, service) selection - nothing to poll.
    logger.info('no user (city, service) selections; cycle is a no-op');
    return { selections: 0, allServicesFailed: false };
  }

  const poll = await pollForecasts(selections, deps.registry);

  const retrievedAt = new Date();
  const store = await storeForecasts(deps.forecastsRepo, poll.results, retrievedAt);

  const notify = await notifyDangerForecasts(deps.notificationsRepo, deps.users, deps.email);

  const allServicesFailed = poll.servicesAttempted > 0 && poll.servicesSucceeded === 0;
  if (allServicesFailed) {
    logger.error({ failedServices: poll.failedServices }, 'all services failed this cycle');
  }

  return { selections: selections.length, poll, store, notify, allServicesFailed };
}
