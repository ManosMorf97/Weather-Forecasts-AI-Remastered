import type { Db } from '../db/client.js';

export interface DangerForecast {
  forecastId: number;
  citySiteId: number;
  timestamp: Date;
  /** Minutes east of UTC for the city at `timestamp` - to render local wall-clock in the email. */
  offsetMinutes: number;
  type: string;
  temperatureC: number;
  windSpeedKmh: number;
  cityName: string;
  country: string;
  serviceName: string;
}

export class NotificationsRepository {
  constructor(private readonly db: Db) {}

  // UC11 step 8: stored forecasts flagged dangerous that still lie in the future (a past
  // "danger to life" forecast is moot). A7 - New Subscriber is handled naturally: eligibility
  // is recomputed from current UserCitySite rows every cycle.
  async getActiveDangerForecasts(now: Date = new Date()): Promise<DangerForecast[]> {
    const rows = await this.db.forecast.findMany({
      where: { dangerFlag: true, timestamp: { gte: now } },
      select: {
        forecastId: true,
        citySiteId: true,
        timestamp: true,
        offsetMinutes: true,
        type: true,
        temperature: true,
        windSpeed: true,
        citySite: {
          select: {
            city: { select: { name: true, country: true } },
            service: { select: { name: true } },
          },
        },
      },
    });

    return rows.map((r) => ({
      forecastId: r.forecastId,
      citySiteId: r.citySiteId,
      timestamp: r.timestamp,
      offsetMinutes: r.offsetMinutes,
      type: r.type,
      temperatureC: r.temperature.toNumber(),
      windSpeedKmh: r.windSpeed.toNumber(),
      cityName: r.citySite.city.name,
      country: r.citySite.city.country,
      serviceName: r.citySite.service.name,
    }));
  }

  // Step 8: for each of these CitySites, the set of user ids that have it in their profile.
  async getSubscribersByCitySite(citySiteIds: number[]): Promise<Map<number, Set<string>>> {
    const byCitySite = new Map<number, Set<string>>();
    if (citySiteIds.length === 0) return byCitySite;

    const rows = await this.db.userCitySite.findMany({
      where: { citySiteId: { in: citySiteIds } },
      select: { userId: true, citySiteId: true },
    });

    for (const row of rows) {
      const set = byCitySite.get(row.citySiteId) ?? new Set<string>();
      set.add(row.userId);
      byCitySite.set(row.citySiteId, set);
    }
    return byCitySite;
  }

  // Step 9: for each of these forecasts, the set of user ids already sent a notification for it,
  // so they are excluded this cycle. A failed delivery is NOT counted as notified - it retries.
  async getNotifiedUsersByForecast(forecastIds: number[]): Promise<Map<number, Set<string>>> {
    const byForecast = new Map<number, Set<string>>();
    if (forecastIds.length === 0) return byForecast;

    const rows = await this.db.notification.findMany({
      where: { forecastId: { in: forecastIds }, status: 'Sent' },
      select: { userId: true, forecastId: true },
    });

    for (const row of rows) {
      const set = byForecast.get(row.forecastId) ?? new Set<string>();
      set.add(row.userId);
      byForecast.set(row.forecastId, set);
    }
    return byForecast;
  }

  // Step 13: one Notification row per (user, forecast) covered by a delivery attempt.
  async logDeliveries(
    userId: string,
    forecastIds: number[],
    channel: string,
    status: 'Sent' | 'Failed',
    sentAt: Date = new Date(),
  ): Promise<void> {
    if (forecastIds.length === 0) return;

    await this.db.notification.createMany({
      data: forecastIds.map((forecastId) => ({ userId, forecastId, channel, status, sentAt })),
    });
  }
}
