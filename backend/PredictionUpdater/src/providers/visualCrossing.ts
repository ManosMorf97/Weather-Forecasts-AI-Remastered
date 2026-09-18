import { z } from 'zod';
import {
  DAILY_SLOT_HOURS,
  HOURLY_COUNT,
  MS_PER_DAY,
  getNext3DateDays,
  localDateKey,
  utcToLocalWallTime,
} from './timeSlots.js';
import type { CityInput, NormalizedForecast, WeatherProvider } from './types.js';

const DEFAULT_BASE_URL = 'https://weather.visualcrossing.com/VisualCrossingWebServices/rest/services/timeline';

// Timeline API with include=current,hours,alerts. `tzoffset` is hours from UTC and may be
// fractional (e.g. +5.5) - converted to minutes once and stamped on every row. The request's
// date range (see fetchForCity) already narrows the response down close to what we need, but
// we still self-filter DAILY down to the exact next 3 local days rather than trusting it, since
// the requested range is a safety margin, not an exact match to the city's local calendar.
const responseSchema = z.object({
  tzoffset: z.number(),
  currentConditions: z.object({
    datetimeEpoch: z.number(),
    temp: z.number(),
    humidity: z.number(),
    windspeed: z.number(),
  }),
  days: z.array(
    z.object({
      datetime: z.string(),
      hours: z.array(
        z.object({
          datetimeEpoch: z.number(),
          temp: z.number(),
          humidity: z.number(),
          windspeed: z.number(),
        }),
      ),
    }),
  ),
  alerts: z
    .array(
      z.object({
        severity: z.string().optional(),
        event: z.string(),
        onset: z.string(),
        ends: z.string().optional(),
      }),
    )
    .optional(),
});

type VisualCrossingResponse = z.infer<typeof responseSchema>;

interface Point {
  utc: Date;
  offsetMinutes: number;
  hourLocal: number;
  dateKey: string;
  temperatureC: number;
  humidityPct: number;
  windSpeedKmh: number;
}

export class VisualCrossingProvider implements WeatherProvider {
  readonly serviceName = 'Visual Crossing';

  constructor(
    private readonly apiKey: string,
    private readonly dangerSeverities: ReadonlySet<string>,
    private readonly timeoutMs: number,
    private readonly baseUrl: string = DEFAULT_BASE_URL,
  ) {}

  // IO only: build the request, fetch, validate, then hand the parsed body to the pure
  // selection helpers below. Everything testable lives in those helpers.
  async fetchForCity(city: CityInput, signal?: AbortSignal): Promise<NormalizedForecast[]> {
    const now = Date.now();
    // We don't know the city's local "today" until tzoffset comes back in the response, so the
    // range must be wide enough to bracket "next 3 local days" for any real-world UTC offset
    // (-12..+14): from yesterday UTC through 4 days ahead UTC. Still far less than the Timeline
    // API's default (~15 days) when no range is given.
    const rangeStart = isoDateUtc(now - MS_PER_DAY);
    const rangeEnd = isoDateUtc(now + 4 * MS_PER_DAY);

    const url = new URL(`${this.baseUrl}/${city.latitude},${city.longitude}/${rangeStart}/${rangeEnd}`);
    url.search = new URLSearchParams({
      unitGroup: 'metric',
      include: 'current,hours,alerts',
      key: this.apiKey,
    }).toString();

    const res = await fetch(url, { signal: signal ?? AbortSignal.timeout(this.timeoutMs) });
    if (!res.ok) {
      console.log(url);
      throw new Error(`Visual Crossing responded ${res.status} for city ${city.cityId}`);
    }

    const data = responseSchema.parse(await res.json());
    return toForecasts(data, now, this.dangerSeverities);
  }
}

// Pure: parsed Visual Crossing body -> our forecast rows, as of the instant `now`.
export function toForecasts(
  data: VisualCrossingResponse,
  now: number,
  dangerSeverities: ReadonlySet<string>,
): NormalizedForecast[] {
  const offsetMinutes = data.tzoffset * 60;
  const points = toPoints(data.days, offsetMinutes);
  const isDanger = dangerAt(data.alerts ?? [], dangerSeverities);

  const currentTimestamp = new Date(data.currentConditions.datetimeEpoch * 1000);
  const current: NormalizedForecast = {
    type: 'CURRENT',
    timestamp: currentTimestamp,
    offsetMinutes,
    temperatureC: data.currentConditions.temp,
    humidityPct: data.currentConditions.humidity,
    windSpeedKmh: data.currentConditions.windspeed,
    danger: isDanger(currentTimestamp),
  };

  const future = points.filter((p) => p.utc.getTime() > now);
  const todayLocal = utcToLocalWallTime(currentTimestamp.getTime(), offsetMinutes);
  const wantedDates = getNext3DateDays(todayLocal);
  const slotHours = DAILY_SLOT_HOURS as readonly number[];

  return [
    current,
    ...future
      .slice(0, HOURLY_COUNT)
      .map((p) => ({ type: 'HOURLY' as const, ...pointValues(p), danger: isDanger(p.utc) })),
    ...future
      .filter((p) => slotHours.includes(p.hourLocal) && wantedDates.has(p.dateKey))
      .map((p) => ({ type: 'DAILY' as const, ...pointValues(p), danger: isDanger(p.utc) })),
  ];
}

function toPoints(days: VisualCrossingResponse['days'], offsetMinutes: number): Point[] {
  return days.flatMap((day) => {
    const [year, month, dayOfMonth] = day.datetime.split('-').map(Number) as [number, number, number];
    const dateKey = localDateKey({ year, month, day: dayOfMonth, hour: 0 });
    return day.hours.map((h) => {
      const utc = new Date(h.datetimeEpoch * 1000);
      return {
        utc,
        offsetMinutes,
        hourLocal: utcToLocalWallTime(utc.getTime(), offsetMinutes).hour,
        dateKey,
        temperatureC: h.temp,
        humidityPct: h.humidity,
        windSpeedKmh: h.windspeed,
      };
    });
  });
}

// severity is sometimes omitted by Visual Crossing; with no CAP value to compare against
// config.dangerSeverities, such an alert is treated as non-danger (safe default).
function dangerAt(alerts: NonNullable<VisualCrossingResponse['alerts']>, dangerSeverities: ReadonlySet<string>) {
  return (instant: Date): boolean =>
    alerts.some((a) => {
      if (!a.severity || !dangerSeverities.has(a.severity.toLowerCase())) return false;
      const onset = new Date(a.onset).getTime();
      const ends = a.ends ? new Date(a.ends).getTime() : Infinity;
      return instant.getTime() >= onset && instant.getTime() <= ends;
    });
}

// UTC calendar date as YYYY-MM-DD, the format Visual Crossing's Timeline API date-range path
// segments expect.
function isoDateUtc(ms: number): string {
  return new Date(ms).toISOString().slice(0, 10);
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
