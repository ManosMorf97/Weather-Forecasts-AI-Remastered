import type { Db } from '../db/client.js';

// Values already rounded to the DB column scale (see store.ts). Celsius / percent / km/h.
export interface ForecastRow {
  citySiteId: number;
  timestamp: Date;
  type: string;
  offsetMinutes: number;
  temperatureC: number;
  humidityPct: number;
  windSpeedKmh: number;
  danger: boolean;
}

export interface ExistingForecast {
  forecastId: number;
  timestamp: Date;
  type: string;
  offsetMinutes: number;
  temperatureC: number;
  humidityPct: number;
  windSpeedKmh: number;
  danger: boolean;
}

export class ForecastsRepository {
  constructor(private readonly db: Db) {}

  // Rows already stored for the (timestamp, type) keys just polled for one CitySite.
  async getExisting(
    citySiteId: number,
    keys: { timestamp: Date; type: string }[],
  ): Promise<ExistingForecast[]> {
    if (keys.length === 0) return [];

    const rows = await this.db.forecast.findMany({
      where: { citySiteId, OR: keys.map((k) => ({ timestamp: k.timestamp, type: k.type })) },
      select: {
        forecastId: true,
        timestamp: true,
        type: true,
        offsetMinutes: true,
        temperature: true,
        humidity: true,
        windSpeed: true,
        dangerFlag: true,
      },
    });

    return rows.map((r) => ({
      forecastId: r.forecastId,
      timestamp: r.timestamp,
      type: r.type,
      offsetMinutes: r.offsetMinutes,
      temperatureC: r.temperature.toNumber(),
      humidityPct: r.humidity.toNumber(),
      windSpeedKmh: r.windSpeed.toNumber(),
      danger: r.dangerFlag,
    }));
  }

  async insertMany(rows: ForecastRow[], retrievedAt: Date): Promise<number> {
    if (rows.length === 0) return 0;

    const result = await this.db.forecast.createMany({
      data: rows.map((r) => ({
        citySiteId: r.citySiteId,
        timestamp: r.timestamp,
        type: r.type,
        offsetMinutes: r.offsetMinutes,
        temperature: r.temperatureC,
        humidity: r.humidityPct,
        windSpeed: r.windSpeedKmh,
        dangerFlag: r.danger,
        retrievedAt,
      })),
    });
    return result.count;
  }

  async updateValues(forecastId: number, row: ForecastRow, retrievedAt: Date): Promise<void> {
    await this.db.forecast.update({
      where: { forecastId },
      data: {
        offsetMinutes: row.offsetMinutes,
        temperature: row.temperatureC,
        humidity: row.humidityPct,
        windSpeed: row.windSpeedKmh,
        dangerFlag: row.danger,
        retrievedAt,
      },
    });
  }
}
