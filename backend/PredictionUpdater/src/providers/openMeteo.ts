import { z } from 'zod';
import {
  DAILY_DAYS,
  DAILY_SLOT_HOURS,
  HOURLY_COUNT,
  localDateKey,
  localWallTimeToUtc,
  parseIsoLocal,
  getNext3DateDays,
  type LocalWallTime,
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

type OpenMeteoResponse = z.infer<typeof responseSchema>;

interface HourlyPoint {
  utc: Date;
  offsetMinutes: number;
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

  // IO only: build the request, fetch, validate, then hand the parsed body to the pure
  // selection helpers below. Everything testable lives in those helpers.
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
    return toForecasts(data, Date.now());
  }
}

// Pure: parsed Open-Meteo body -> our forecast rows, as of the instant `now`.
export function toForecasts(data: OpenMeteoResponse, now: number): NormalizedForecast[] {
  const offsetMinutes = data.utc_offset_seconds / 60;
  const todayLocal = parseIsoLocal(data.current.time);
  const points = fetchALLHourlyPoints(data.hourly, offsetMinutes);

  return [
    fetchCurrent(data.current, offsetMinutes),
    ...fetchNext3HourlyPoints(points, now),
    ...fetchDaily(points, todayLocal, now),
  ];
}

// 1. The single CURRENT reading.
function fetchCurrent(
  current: OpenMeteoResponse['current'],
  offsetMinutes: number,
): NormalizedForecast {
  return {
    type: 'CURRENT',
    timestamp: localWallTimeToUtc(parseIsoLocal(current.time), offsetMinutes),
    offsetMinutes,
    temperatureC: current.temperature_2m,
    humidityPct: current.relative_humidity_2m,
    windSpeedKmh: current.wind_speed_10m,
    danger: false,
  };
}

// 2. Every usable hourly entry, normalised. Rows with a null value are skipped. Both the
//    HOURLY and DAILY selections read from this list.
function fetchALLHourlyPoints(
  hourly: OpenMeteoResponse['hourly'],
  offsetMinutes: number,
): HourlyPoint[] {
  const points: HourlyPoint[] = [];
  for (let i = 0; i < hourly.time.length; i++) {
    const t = hourly.temperature_2m[i];
    const h = hourly.relative_humidity_2m[i];
    const w = hourly.wind_speed_10m[i];
    const timeStr = hourly.time[i];
    if (t == null || h == null || w == null || timeStr == null) continue;

    const local = parseIsoLocal(timeStr);
    points.push({
      utc: localWallTimeToUtc(local, offsetMinutes),
      offsetMinutes,
      hourLocal: local.hour,
      dateKey: localDateKey(local),
      temperatureC: t,
      humidityPct: h,
      windSpeedKmh: w,
    });
  }
  return points;
}

// 3. The next 3 hourly points still in the future.
function fetchNext3HourlyPoints(points: HourlyPoint[], now: number): NormalizedForecast[] {
  return points
    .filter((p) => p.utc.getTime() > now)
    .slice(0, HOURLY_COUNT)
    .map((p) => ({ type: 'HOURLY', ...pointValues(p), danger: false }));
}

// 4. Future hourly points that land on a DAILY slot (08/15/21 local) of the next DAILY_DAYS days.
function fetchDaily(
  points: HourlyPoint[],
  todayLocal: LocalWallTime,
  now: number,
): NormalizedForecast[] {
  const wantedDates = getNext3DateDays(todayLocal);
  const slotHours = DAILY_SLOT_HOURS as readonly number[];
  return points
    .filter(
      (p) =>
        slotHours.includes(p.hourLocal) &&
        wantedDates.has(p.dateKey) &&
        p.utc.getTime() > now,
    )
    .map((p) => ({ type: 'DAILY', ...pointValues(p), danger: false }));
}

function pointValues(p: HourlyPoint) {
  return {
    timestamp: p.utc,
    offsetMinutes: p.offsetMinutes,
    temperatureC: p.temperatureC,
    humidityPct: p.humidityPct,
    windSpeedKmh: p.windSpeedKmh,
  };
}
