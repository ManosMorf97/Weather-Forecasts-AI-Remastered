import { z } from 'zod';
import {
  DAILY_DAYS,
  DAILY_SLOT_HOURS,
  HOURLY_COUNT,
  localDateKey,
  localWallTimeToUtc,
  parseIsoLocal,
  wantedDailyDateKeys,
} from './timeSlots.js';
import type { CityInput, NormalizedForecast, WeatherProvider } from './types.js';

const DEFAULT_BASE_URL = 'https://api.open-meteo.com/v1/forecast';

// With timezone=auto every `time` string is local wall-clock (no offset suffix); we convert
// back to UTC with utc_offset_seconds. wind_speed_unit=kmh gives us our canonical unit directly.
const responseSchema = z.object({
  utc_offset_seconds: z.number(),
  current: z.object({
    time: z.string(),
    temperature_2m: z.number(),
    relative_humidity_2m: z.number(),
    wind_speed_10m: z.number(),
  }),
  hourly: z.object({
    time: z.array(z.string()),
    temperature_2m: z.array(z.number().nullable()),
    relative_humidity_2m: z.array(z.number().nullable()),
    wind_speed_10m: z.array(z.number().nullable()),
  }),
});

interface HourlyPoint {
  utc: Date;
  hourLocal: number;
  dateKey: string;
  temperatureC: number;
  humidityPct: number;
  windSpeedKmh: number;
}

export class OpenMeteoProvider implements WeatherProvider {
  readonly serviceName = 'Open-Meteo';

  constructor(
    private readonly timeoutMs: number,
    private readonly baseUrl: string = DEFAULT_BASE_URL,
  ) {}

  async fetchForCity(city: CityInput, signal?: AbortSignal): Promise<NormalizedForecast[]> {
    const url = new URL(this.baseUrl);
    url.search = new URLSearchParams({
      latitude: String(city.latitude),
      longitude: String(city.longitude),
      current: 'temperature_2m,relative_humidity_2m,wind_speed_10m',
      hourly: 'temperature_2m,relative_humidity_2m,wind_speed_10m',
      wind_speed_unit: 'kmh',
      timezone: 'auto',
      forecast_days: String(DAILY_DAYS + 1),
    }).toString();

    const res = await fetch(url, { signal: signal ?? AbortSignal.timeout(this.timeoutMs) });
    if (!res.ok) {
      throw new Error(`Open-Meteo responded ${res.status} for city ${city.cityId}`);
    }

    const data = responseSchema.parse(await res.json());
    const offsetMinutes = data.utc_offset_seconds / 60;
    const now = Date.now();

    const out: NormalizedForecast[] = [];

    const currentLocal = parseIsoLocal(data.current.time);
    out.push({
      type: 'CURRENT',
      timestamp: localWallTimeToUtc(currentLocal, offsetMinutes),
      temperatureC: data.current.temperature_2m,
      humidityPct: data.current.relative_humidity_2m,
      windSpeedKmh: data.current.wind_speed_10m,
      danger: false,
    });

    const hourly: HourlyPoint[] = [];
    for (let i = 0; i < data.hourly.time.length; i++) {
      const t = data.hourly.temperature_2m[i];
      const h = data.hourly.relative_humidity_2m[i];
      const w = data.hourly.wind_speed_10m[i];
      const timeStr = data.hourly.time[i];
      if (t == null || h == null || w == null || timeStr == null) continue;

      const local = parseIsoLocal(timeStr);
      hourly.push({
        utc: localWallTimeToUtc(local, offsetMinutes),
        hourLocal: local.hour,
        dateKey: localDateKey(local),
        temperatureC: t,
        humidityPct: h,
        windSpeedKmh: w,
      });
    }

    for (const point of hourly.filter((p) => p.utc.getTime() > now).slice(0, HOURLY_COUNT)) {
      out.push({ type: 'HOURLY', ...pointValues(point), danger: false });
    }

    const wantedDates = wantedDailyDateKeys(currentLocal);
    for (const point of hourly) {
      const isSlot = (DAILY_SLOT_HOURS as readonly number[]).includes(point.hourLocal);
      if (isSlot && wantedDates.has(point.dateKey) && point.utc.getTime() > now) {
        out.push({ type: 'DAILY', ...pointValues(point), danger: false });
      }
    }

    return out;
  }
}

function pointValues(p: HourlyPoint) {
  return {
    timestamp: p.utc,
    temperatureC: p.temperatureC,
    humidityPct: p.humidityPct,
    windSpeedKmh: p.windSpeedKmh,
  };
}
