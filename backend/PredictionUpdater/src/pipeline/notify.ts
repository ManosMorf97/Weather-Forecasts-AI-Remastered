import { logger } from '../logging/logger.js';
import type { EmailSender } from '../notifications/emailSender.js';
import type { UsersLookup } from '../notifications/appwriteUsers.js';
import type {
  DangerForecast,
  NotificationsRepository,
} from '../repositories/notificationsRepository.js';
import type { NotificationResult } from './types.js';

const CHANNEL = 'email';

// UC11 steps 8-13. Groups every unnotified danger forecast a user is eligible for into a
// single email, sends it, and logs one Notification row per covered forecast.
export async function notifyDangerForecasts(
  repo: NotificationsRepository,
  users: UsersLookup,
  email: EmailSender,
  now: Date = new Date(),
): Promise<NotificationResult> {
  const empty: NotificationResult = {
    dangerForecasts: 0,
    usersWithWarnings: 0,
    usersNotified: 0,
    deliveriesFailed: 0,
    emailsMissing: 0,
  };

  const dangerForecasts = await repo.getActiveDangerForecasts(now);
  if (dangerForecasts.length === 0) {
    logger.info('no active danger forecasts');
    return empty;
  }
  empty.dangerForecasts = dangerForecasts.length;

  const citySiteIds = [...new Set(dangerForecasts.map((d) => d.citySiteId))];
  const subscribersByCitySite = await repo.getSubscribersByCitySite(citySiteIds);
  const deliveredPairs = await repo.getDeliveredPairs(dangerForecasts.map((d) => d.forecastId));

  // Step 10: build one warning list per user, excluding already-delivered (user, forecast) pairs.
  const warningsByUser = new Map<string, DangerForecast[]>();
  for (const forecast of dangerForecasts) {
    const subscribers = subscribersByCitySite.get(forecast.citySiteId) ?? [];
    if (subscribers.length === 0) {
      logger.info(
        { forecastId: forecast.forecastId, citySiteId: forecast.citySiteId },
        'danger forecast has no subscribers', // A6
      );
      continue;
    }
    for (const userId of subscribers) {
      if (deliveredPairs.has(`${userId}|${forecast.forecastId}`)) continue;
      const list = warningsByUser.get(userId) ?? [];
      list.push(forecast);
      warningsByUser.set(userId, list);
    }
  }

  if (warningsByUser.size === 0) {
    logger.info({ dangerForecasts: dangerForecasts.length }, 'all danger forecasts already notified');
    return { ...empty };
  }

  // Step 11: one batched email lookup for every user with warnings.
  const emails = await users.getEmails([...warningsByUser.keys()]);

  let usersNotified = 0;
  let deliveriesFailed = 0;
  let emailsMissing = 0;

  for (const [userId, warnings] of warningsByUser) {
    const address = emails.get(userId);
    const forecastIds = warnings.map((w) => w.forecastId);

    if (!address) {
      // A8 fallout: no email this cycle -> stay unnotified, retried next cycle.
      logger.warn({ userId }, 'no email for user; deferring to next cycle');
      emailsMissing++;
      continue;
    }

    try {
      await email.send(address, buildSubject(warnings), buildBody(warnings));
      await repo.logDeliveries(userId, forecastIds, CHANNEL, 'Sent', now);
      usersNotified++;
    } catch (err) {
      logger.error({ err, userId }, 'danger notification delivery failed'); // E3
      await repo.logDeliveries(userId, forecastIds, CHANNEL, 'Failed', now);
      deliveriesFailed++;
    }
  }

  return {
    dangerForecasts: dangerForecasts.length,
    usersWithWarnings: warningsByUser.size,
    usersNotified,
    deliveriesFailed,
    emailsMissing,
  };
}

function buildSubject(warnings: DangerForecast[]): string {
  const cities = [...new Set(warnings.map((w) => w.cityName))];
  return cities.length === 1
    ? `Weather warning for ${cities[0]}`
    : `Weather warnings for ${cities.length} of your areas`;
}

function buildBody(warnings: DangerForecast[]): string {
  const lines = warnings
    .slice()
    .sort((a, b) => a.timestamp.getTime() - b.timestamp.getTime())
    .map((w) => {
      const when = w.timestamp.toISOString().replace('T', ' ').slice(0, 16) + ' UTC';
      return `- ${w.cityName}, ${w.country} (${w.serviceName}, ${w.type}) at ${when}: ` +
        `${w.temperatureC.toFixed(1)}°C, wind ${w.windSpeedKmh.toFixed(0)} km/h`;
    });

  return [
    'A life-threatening weather condition is forecast for areas in your profile:',
    '',
    ...lines,
    '',
    'Stay informed through your local authorities.',
  ].join('\n');
}
