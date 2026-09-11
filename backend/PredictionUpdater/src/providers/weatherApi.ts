import { z } from 'zod';
import { DAILY_SLOT_HOURS, HOURLY_COUNT, getNext3DateDays, localDateKey, type LocalWallTime } from './timeSlots.js';
import type { CityInput, NormalizedForecast, WeatherProvider } from './types.js';

const DEFAULT_BASE_URL = 'https://api.weatherapi.com/v1/forecast.json';
// WeatherAPI's `days` counts today as day 1, so 4 days = today + the next 3 full local days.
// We need that 4th day so the DAILY selection below (which excludes today, like the other
// providers) has 3 whole days to draw from rather than just 2.
const FORECAST_DAYS = 4;

// forecast.json with alerts=yes. `hour[].time` and `location.localtime` are local wall-clock
// strings ("YYYY-MM-DD HH:mm"), so no offset math is needed to read a local hour from them -
// only to recover offsetMinutes itself, from the gap between `location.localtime` and the true
// UTC instant `location.localtime_epoch`.
const responseSchema = z.object({
  location: z.object({
    localtime_epoch: z.number(),
    localtime: z.string(),
  }),
  current: z.object({
    temp_c: z.number(),
    humidity: z.number(),
    wind_kph: z.number(),
  }),
  forecast: z.object({
    forecastday: z.array(
      z.object({
        date: z.string(),
        hour: z.array(
          z.object({
            time: z.string(),
            time_epoch: z.number(),
            temp_c: z.number(),
            humidity: z.number(),
            wind_kph: z.number(),
          }),
        ),
      }),
    ),
  }),
  alerts: z
    .object({
      alert: z.array(
        z.object({
          severity: z.string().optional(),
          effective: z.string(),
          expires: z.string(),
        }),
      ),
    })
    .optional(),
});

type WeatherApiResponse = z.infer<typeof responseSchema>;

interface Point {
  utc: Date;
  offsetMinutes: number;
  hourLocal: number;
  dateKey: string;
  temperatureC: number;
  humidityPct: number;
  windSpeedKmh: number;
}

export class WeatherApiProvider implements WeatherProvider {
  readonly serviceName = 'WeatherAPI';

  constructor(
    private readonly apiKey: string,
    private readonly dangerSeverities: ReadonlySet<string>,
    private readonly timeoutMs: number,
    private readonly baseUrl: string = DEFAULT_BASE_URL,
  ) {}

  // IO only: build the request, fetch, validate, then hand the parsed body to the pure
  // selection helpers below. Everything testable lives in those helpers.
  async fetchForCity(city: CityInput, signal?: AbortSignal): Promise<NormalizedForecast[]> {
    const url = new URL(this.baseUrl);
    url.search = new URLSearchParams({
      key: this.apiKey,
      q: `${city.latitude},${city.longitude}`,
      days: String(FORECAST_DAYS),
      alerts: 'yes',
      aqi: 'no',
    }).toString();

    const res = await fetch(url, { signal: signal ?? AbortSignal.timeout(this.timeoutMs) });
    if (!res.ok) {
      throw new Error(`WeatherAPI responded ${res.status} for city ${city.cityId}`);
    }

    const data = responseSchema.parse(await res.json());
    return toForecasts(data, Date.now(), this.dangerSeverities);
  }
}

// Pure: parsed WeatherAPI body -> our forecast rows, as of the instant `now`.
export function toForecasts(
  data: WeatherApiResponse,
  now: number,
  dangerSeverities: ReadonlySet<string>,
): NormalizedForecast[] {
  const offsetMinutes = offsetMinutesFrom(data.location);
  const points = data.forecast.forecastday.flatMap((day) => toPoints(day, offsetMinutes));
  const isDanger = dangerAt(data.alerts?.alert ?? [], dangerSeverities);
  const slotHours = DAILY_SLOT_HOURS as readonly number[];
  const todayLocal = parseSpaceLocal(data.location.localtime);
  const wantedDates = getNext3DateDays(todayLocal);

  const currentTimestamp = new Date(data.location.localtime_epoch * 1000);
  const current: NormalizedForecast = {
    type: 'CURRENT',
    timestamp: currentTimestamp,
    offsetMinutes,
    temperatureC: data.current.temp_c,
    humidityPct: data.current.humidity,
    windSpeedKmh: data.current.wind_kph,
    danger: isDanger(currentTimestamp),
  };

  const future = points.filter((p) => p.utc.getTime() > now);

  return [
    current,
    ...future
      .slice(0, HOURLY_COUNT)
      .map((p) => ({ type: 'HOURLY' as const, ...pointValues(p), danger: isDanger(p.utc) })),
    // Like Open-Meteo/Visual Crossing: exclude today, keep only the next 3 full local days -
    // `days=4` fetches one extra day so those 3 full days are actually available.
    ...future
      .filter((p) => slotHours.includes(p.hourLocal) && wantedDates.has(p.dateKey))
      .map((p) => ({ type: 'DAILY' as const, ...pointValues(p), danger: isDanger(p.utc) })),
  ];
}

function offsetMinutesFrom(location: WeatherApiResponse['location']): number {
  const local = parseSpaceLocal(location.localtime);
  const asIfUtc = Date.UTC(local.year, local.month - 1, local.day, local.hour, local.minute);
  return Math.round((asIfUtc - location.localtime_epoch * 1000) / 60_000);
}

function toPoints(day: WeatherApiResponse['forecast']['forecastday'][number], offsetMinutes: number): Point[] {
  const [year, month, dayOfMonth] = day.date.split('-').map(Number) as [number, number, number];
  const dateKey = localDateKey({ year, month, day: dayOfMonth, hour: 0 });
  return day.hour.map((h) => ({
    utc: new Date(h.time_epoch * 1000),
    offsetMinutes,
    hourLocal: parseSpaceLocal(h.time).hour,
    dateKey,
    temperatureC: h.temp_c,
    humidityPct: h.humidity,
    windSpeedKmh: h.wind_kph,
  }));
}

// WeatherAPI's local-time fields use a space, not "T" ("2026-09-04 15:00"), so they need their
// own parser rather than timeSlots' ISO-local one.
function parseSpaceLocal(spaceLocal: string): Required<LocalWallTime> {
  const [date = '', time = '00:00'] = spaceLocal.split(' ');
  const [year, month, day] = date.split('-').map(Number) as [number, number, number];
  const [hour, minute = 0] = time.split(':').map(Number) as [number, number];
  return { year, month, day, hour, minute };
}

// Danger = covered by at least one alert whose CAP severity is in the configured set.
function dangerAt(
  alerts: NonNullable<WeatherApiResponse['alerts']>['alert'],
  dangerSeverities: ReadonlySet<string>,
) {
  return (instant: Date): boolean =>
    alerts.some((a) => {
      if (!a.severity || !dangerSeverities.has(a.severity.toLowerCase())) return false;
      const effective = new Date(a.effective).getTime();
      const expires = new Date(a.expires).getTime();
      return instant.getTime() >= effective && instant.getTime() <= expires;
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
