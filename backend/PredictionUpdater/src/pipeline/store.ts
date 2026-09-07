import type { ForecastsRepository, ForecastRow } from '../repositories/forecastsRepository.js';
import type { CitySiteForecasts } from './poll.js';

// DB column scales (Temperature decimal(3,1), Humidity/WindSpeed decimal(5,2)). We round to
// these before comparing and inserting so "same forecast, polled again" is a stable no-op.
const TEMP_DP = 1;
const HUMIDITY_DP = 2;
const WIND_DP = 2;

const round = (value: number, dp: number): number => Number(value.toFixed(dp));

export interface StoreSummary {
  inserted: number;
  updated: number;
  skipped: number;
}

// UC11 step 7 with A4 (skip exact duplicate) and A5 (update when values changed).
export async function storeForecasts(
  repo: ForecastsRepository,
  polled: CitySiteForecasts[],
  retrievedAt: Date,
): Promise<StoreSummary> {
  const summary: StoreSummary = { inserted: 0, updated: 0, skipped: 0 };

  for (const entry of polled) {
    const one = await storeForCitySite(repo, entry.citySiteId, entry.forecasts, retrievedAt);
    summary.inserted += one.inserted;
    summary.updated += one.updated;
    summary.skipped += one.skipped;
  }

  return summary;
}

async function storeForCitySite(
  repo: ForecastsRepository,
  citySiteId: number,
  forecasts: CitySiteForecasts['forecasts'],
  retrievedAt: Date,
): Promise<StoreSummary> {
  const rows: ForecastRow[] = forecasts.map((f) => ({
    citySiteId,
    timestamp: f.timestamp,
    type: f.type,
    offsetMinutes: f.offsetMinutes,
    temperatureC: round(f.temperatureC, TEMP_DP),
    humidityPct: round(f.humidityPct, HUMIDITY_DP),
    windSpeedKmh: round(f.windSpeedKmh, WIND_DP),
    danger: f.danger,
  }));

  const existing = await repo.getExisting(
    citySiteId,
    rows.map((r) => ({ timestamp: r.timestamp, type: r.type })),
  );
  const existingByKey = new Map(existing.map((e) => [key(e.timestamp, e.type), e]));

  const toInsert: ForecastRow[] = [];
  let updated = 0;
  let skipped = 0;

  for (const row of rows) {
    const match = existingByKey.get(key(row.timestamp, row.type));
    if (!match) {
      toInsert.push(row);
      continue;
    }

    const unchanged =
      match.offsetMinutes === row.offsetMinutes &&
      match.temperatureC === row.temperatureC &&
      match.humidityPct === row.humidityPct &&
      match.windSpeedKmh === row.windSpeedKmh &&
      match.danger === row.danger;

    if (unchanged) {
      skipped++;
      continue;
    }

    await repo.updateValues(match.forecastId, row, retrievedAt);
    updated++;
  }

  const inserted = await repo.insertMany(toInsert, retrievedAt);
  return { inserted, updated, skipped };
}

const key = (timestamp: Date, type: string): string => `${timestamp.getTime()}|${type}`;
