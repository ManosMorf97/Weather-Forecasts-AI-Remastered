import { logger } from '../logging/logger.js';
import { pollForecasts } from './poll.js';
import { storeForecasts } from './store.js';
import { notifyDangerForecasts } from './notify.js';
import type { CycleDeps, CycleResult } from './types.js';

// One full UC11 cycle: read selections -> poll -> store -> notify. Intended to be invoked as
// a run-once job by an external scheduler (cron / k8s CronJob / cloud scheduler).
export async function runCycle(deps: CycleDeps): Promise<CycleResult> {
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
