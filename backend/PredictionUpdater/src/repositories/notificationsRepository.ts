import type { Db } from '../db/client.js';

export interface DangerForecast {
  forecastId: number;
  citySiteId: number;
  timestamp: Date;
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
      type: r.type,
      temperatureC: r.temperature.toNumber(),
      windSpeedKmh: r.windSpeed.toNumber(),
      cityName: r.citySite.city.name,
      country: r.citySite.city.country,
      serviceName: r.citySite.service.name,
    }));
  }

  // Step 8: user ids that have any of these CitySites in their profile, keyed by citySiteId.
  async getSubscribersByCitySite(citySiteIds: number[]): Promise<Map<number, string[]>> {
    const byCitySite = new Map<number, string[]>();
    if (citySiteIds.length === 0) return byCitySite;

    const rows = await this.db.userCitySite.findMany({
      where: { citySiteId: { in: citySiteIds } },
      select: { userId: true, citySiteId: true },
    });

    for (const row of rows) {
      const list = byCitySite.get(row.citySiteId) ?? [];
      list.push(row.userId);
      byCitySite.set(row.citySiteId, list);
    }
    return byCitySite;
  }

  // Step 9: (userId, forecastId) pairs that already have a delivered notification, so they
  // are excluded from this cycle. A failed delivery is NOT counted as notified - it retries.
  async getDeliveredPairs(forecastIds: number[]): Promise<Set<string>> {
    const delivered = new Set<string>();
    if (forecastIds.length === 0) return delivered;

    const rows = await this.db.notification.findMany({
      where: { forecastId: { in: forecastIds }, status: 'Sent' },
      select: { userId: true, forecastId: true },
    });

    for (const row of rows) delivered.add(`${row.userId}|${row.forecastId}`);
    return delivered;
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
