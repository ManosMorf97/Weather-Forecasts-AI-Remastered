import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { WeatherApiProvider } from '../../src/providers/weatherApi.js';
import type { CityInput } from '../../src/providers/types.js';

// Athens: UTC+3 in summer. "Now" is pinned so the CURRENT / HOURLY / DAILY selection is
// deterministic.
const NOW = new Date('2026-09-04T12:00:00Z'); // 15:00 Athens local

const ATHENS: CityInput = {
  cityId: 1,
  citySiteId: 10,
  name: 'Athens',
  country: 'Greece',
  latitude: 37.9838,
  longitude: 23.7275,
};

function pad(n: number): string {
  return String(n).padStart(2, '0');
}

// Hourly grid, every local hour, for local days Sep 4-7 - `days=4` (today + the next 3 full
// days), mirroring how forecast.json's forecastday[].hour[] is laid out (24 local-time entries
// per day).
function buildResponse(alerts: Array<{ severity?: string; effective: string; expires: string }> = []) {
  const forecastday = [4, 5, 6, 7].map((day) => ({
    date: `2026-09-${pad(day)}`,
    hour: Array.from({ length: 24 }, (_, hour) => {
      const utcMs = Date.UTC(2026, 8, day, hour, 0, 0) - 180 * 60_000; // local -3h = UTC
      return {
        time: `2026-09-${pad(day)} ${pad(hour)}:00`,
        time_epoch: utcMs / 1000,
        temp_c: 20 + hour * 0.1,
        humidity: 50,
        wind_kph: 10,
      };
    }),
  }));

  return {
    location: {
      localtime_epoch: NOW.getTime() / 1000,
      localtime: '2026-09-04 15:00',
    },
    current: { temp_c: 30.4, humidity: 45, wind_kph: 12 },
    forecast: { forecastday },
    alerts: { alert: alerts },
  };
}

function stubFetch(body: unknown) {
  vi.stubGlobal(
    'fetch',
    vi.fn(async () => new Response(JSON.stringify(body), { status: 200 })),
  );
}

describe('WeatherApiProvider', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(NOW);
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.unstubAllGlobals();
  });

  it('returns one CURRENT reading with offsetMinutes derived from localtime vs localtime_epoch', async () => {
    stubFetch(buildResponse());
    const provider = new WeatherApiProvider('key', new Set(['extreme']), 15_000);

    const forecasts = await provider.fetchForCity(ATHENS);

    const current = forecasts.filter((f) => f.type === 'CURRENT');
    expect(current).toHaveLength(1);
    expect(current[0]).toMatchObject({
      offsetMinutes: 180,
      temperatureC: 30.4,
      humidityPct: 45,
      windSpeedKmh: 12,
      danger: false,
    });
    expect(current[0]?.timestamp.toISOString()).toBe('2026-09-04T12:00:00.000Z');
  });

  it('returns the next 3 hourly readings after now', async () => {
    stubFetch(buildResponse());
    const provider = new WeatherApiProvider('key', new Set(['extreme']), 15_000);

    const hourly = (await provider.fetchForCity(ATHENS)).filter((f) => f.type === 'HOURLY');

    expect(hourly.map((f) => f.timestamp.toISOString())).toEqual([
      '2026-09-04T13:00:00.000Z', // 16:00 Athens
      '2026-09-04T14:00:00.000Z',
      '2026-09-04T15:00:00.000Z',
    ]);
    expect(hourly.every((f) => f.offsetMinutes === 180 && f.danger === false)).toBe(true);
  });

  it('returns 08/15/21-local slots for the next 3 full local days, excluding today', async () => {
    stubFetch(buildResponse());
    const provider = new WeatherApiProvider('key', new Set(['extreme']), 15_000);

    const daily = (await provider.fetchForCity(ATHENS)).filter((f) => f.type === 'DAILY');

    // Today (Sep 4) is excluded entirely, like Open-Meteo/Visual Crossing; Sep 5-7 each
    // contribute all 3 slots.
    expect(daily.map((f) => f.timestamp.toISOString())).toEqual([
      '2026-09-05T05:00:00.000Z', // 08:00 Athens
      '2026-09-05T12:00:00.000Z', // 15:00 Athens
      '2026-09-05T18:00:00.000Z', // 21:00 Athens
      '2026-09-06T05:00:00.000Z', // 08:00 Athens
      '2026-09-06T12:00:00.000Z', // 15:00 Athens
      '2026-09-06T18:00:00.000Z', // 21:00 Athens
      '2026-09-07T05:00:00.000Z', // 08:00 Athens
      '2026-09-07T12:00:00.000Z', // 15:00 Athens
      '2026-09-07T18:00:00.000Z', // 21:00 Athens
    ]);
    expect(daily.every((f) => f.offsetMinutes === 180)).toBe(true);
  });

  it('marks a DAILY slot as danger only when an alert of a configured severity covers it', async () => {
    stubFetch(
      buildResponse([
        { severity: 'Extreme', effective: '2026-09-05T10:00:00Z', expires: '2026-09-05T20:00:00Z' },
      ]),
    );
    const provider = new WeatherApiProvider('key', new Set(['extreme']), 15_000);

    const daily = (await provider.fetchForCity(ATHENS)).filter((f) => f.type === 'DAILY');
    const bySep5Hour = (h: number) => daily.find((f) => f.timestamp.toISOString().startsWith(`2026-09-05T${pad(h)}`));

    // Sep 5 15:00 Athens => 12:00Z, inside the alert window.
    expect(bySep5Hour(12)?.danger).toBe(true);
    // Sep 5 08:00 Athens => 05:00Z, before the alert window starts.
    expect(bySep5Hour(5)?.danger).toBe(false);
  });

  it('ignores an alert whose severity is not in the configured danger set', async () => {
    stubFetch(
      buildResponse([{ severity: 'Minor', effective: '2026-09-05T10:00:00Z', expires: '2026-09-05T20:00:00Z' }]),
    );
    const provider = new WeatherApiProvider('key', new Set(['extreme']), 15_000);

    const forecasts = await provider.fetchForCity(ATHENS);

    expect(forecasts.every((f) => f.danger === false)).toBe(true);
  });
});
