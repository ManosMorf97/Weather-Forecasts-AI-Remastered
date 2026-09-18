import { z } from 'zod';
import {
  DAILY_SLOT_HOURS,
  getNext3DateDays,
  localDateKey,
  utcToLocalWallTime,
  type LocalWallTime,
} from './timeSlots.js';
import type { CityInput, NormalizedForecast, WeatherProvider } from './types.js';

const DEFAULT_BASE_URL = 'https://api.openweathermap.org/data/2.5/forecast';

// Free 5-day/3-hour endpoint: `list[]` steps are 3h apart, each stamped with a UTC epoch second
// plus one shared `city.timezone` offset. This endpoint carries no alert data, so every row this
// adapter emits has danger: false. It emits no HOURLY row: the closest available granularity is
// 3-hourly, not hourly, so a fabricated "next 3 hours" would misrepresent the data - CURRENT and
// DAILY only.
const responseSchema = z.object({
  city: z.object({ timezone: z.number() }),
  list: z
    .array(
      z.object({
        dt: z.number(),
        main: z.object({ temp: z.number(), humidity: z.number() }),
        wind: z.object({ speed: z.number() }),
      }),
    )
    .min(1),
});

type OpenWeatherMapResponse = z.infer<typeof responseSchema>;

interface Point {
  utc: Date;
  offsetMinutes: number;
  hourLocal: number;
  dateKey: string;
  temperatureC: number;
  humidityPct: number;
  windSpeedKmh: number;
}

export class OpenWeatherMapProvider implements WeatherProvider {
  readonly serviceName = 'OpenWeatherMap';

  constructor(
    private readonly apiKey: string,
    private readonly timeoutMs: number,
    private readonly baseUrl: string = DEFAULT_BASE_URL,
  ) {}

  // IO only: build the request, fetch, validate, then hand the parsed body to the pure
  // selection helpers below. Everything testable lives in those helpers.
  async fetchForCity(city: CityInput, signal?: AbortSignal): Promise<NormalizedForecast[]> {
    const url = new URL(this.baseUrl);
    url.search = new URLSearchParams({
      lat: String(city.latitude),
      lon: String(city.longitude),
      units: 'metric',
      appid: this.apiKey,
    }).toString();

    const res = await fetch(url, { signal: signal ?? AbortSignal.timeout(this.timeoutMs) });
    if (!res.ok) {
      throw new Error(`OpenWeatherMap responded ${res.status} for city ${city.cityId}`);
    }

    const data = responseSchema.parse(await res.json());
    return toForecasts(data, Date.now());
  }
}

// Pure: parsed OpenWeatherMap body -> our forecast rows, as of the instant `now`.
export function toForecasts(data: OpenWeatherMapResponse, now: number): NormalizedForecast[] {
  const offsetMinutes = data.city.timezone / 60;
  const points = toPoints(data.list, offsetMinutes);

  // CURRENT is the first step at/after now; DAILY is drawn from the steps strictly after it, so
  // the same instant is never emitted twice.
  const future = points.filter((p) => p.utc.getTime() >= now);
  const [current, ...rest] = future;
  if (!current) return [];

  const todayLocal = utcToLocalWallTime(now, offsetMinutes);
  const wantedDates = getNext3DateDays(todayLocal);
  const slotHours = DAILY_SLOT_HOURS as readonly number[];

  return [
    { type: 'CURRENT', ...pointValues(current), danger: false },
    ...rest
      .filter((p) => slotHours.includes(p.hourLocal) && wantedDates.has(p.dateKey))
      .map((p) => ({ type: 'DAILY' as const, ...pointValues(p), danger: false })),
  ];
}

function toPoints(list: OpenWeatherMapResponse['list'], offsetMinutes: number): Point[] {
  return list.map((item) => {
    const utc = new Date(item.dt * 1000);//to find the milliseconds
    const local: LocalWallTime = utcToLocalWallTime(utc.getTime(), offsetMinutes);
    return {
      utc,
      offsetMinutes,
      hourLocal: local.hour,
      dateKey: localDateKey(local),
      temperatureC: item.main.temp,
      humidityPct: item.main.humidity,
      windSpeedKmh: item.wind.speed * 3.6,
    };
  });
}

function pointValues(p: Point) {
  return {
    timestamp: p.utc,
    offsetMinutes: p.offsetMinutes,
    temperatureC: p.temperatureC,
    humidityPct: p.humidityPct,
    windSpeedKmh: p.windSpeedKmh,
  };
}
