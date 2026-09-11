import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { OpenWeatherMapProvider } from '../../src/providers/openWeatherMap.js';
import type { CityInput } from '../../src/providers/types.js';

// Athens: UTC+3 in summer (timezone = 10800). "Now" is pinned so the CURRENT / DAILY selection
// is deterministic.
const NOW = new Date('2026-09-04T12:00:00Z'); // 15:00 Athens local

const ATHENS: CityInput = {
  cityId: 1,
  citySiteId: 10,
  name: 'Athens',
  country: 'Greece',
  latitude: 37.9838,
  longitude: 23.7275,
};

function buildResponse() {
  // 3-hourly grid, every 3h for 4 local days starting 2026-09-04T00:00 local (UTC+3, so
  // 2026-09-03T21:00Z).
  const list: Array<{ dt: number; main: { temp: number; humidity: number }; wind: { speed: number } }> = [];
  const startUtcMs = Date.UTC(2026, 8, 3, 21, 0, 0); // 2026-09-04T00:00 Athens local
  for (let step = 0; step < 32; step++) {
    list.push({
      dt: (startUtcMs + step * 3 * 60 * 60 * 1000) / 1000,
      main: { temp: 20 + step * 0.1, humidity: 50 },
      wind: { speed: 10 },
    });
  }
  return { city: { timezone: 10_800 }, list };
}

describe('OpenWeatherMapProvider', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(NOW);
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => new Response(JSON.stringify(buildResponse()), { status: 200 })),
    );
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.unstubAllGlobals();
  });

  it('returns one CURRENT reading: the first 3h step at/after now', async () => {
    const provider = new OpenWeatherMapProvider('key', 15_000);

    const forecasts = await provider.fetchForCity(ATHENS);

    const current = forecasts.filter((f) => f.type === 'CURRENT');
    expect(current).toHaveLength(1);
    // Steps land on 00/03/06/.../21 Athens local; NOW is 15:00 Athens, so first step at/after
    // now is 15:00 Athens => 12:00Z.
    expect(current[0]?.timestamp.toISOString()).toBe('2026-09-04T12:00:00.000Z');
    expect(current[0]).toMatchObject({ offsetMinutes: 180, humidityPct: 50, windSpeedKmh: 36, danger: false });
  });

  it('never emits an HOURLY row - the API has no true hourly granularity to draw one from', async () => {
    const provider = new OpenWeatherMapProvider('key', 15_000);

    const forecasts = await provider.fetchForCity(ATHENS);

    expect(forecasts.some((f) => f.type === 'HOURLY')).toBe(false);
  });

  it('returns DAILY steps at 15/21 local for each of the next 3 local days (08 never lands on the 3h grid)', async () => {
    const provider = new OpenWeatherMapProvider('key', 15_000);

    const daily = (await provider.fetchForCity(ATHENS)).filter((f) => f.type === 'DAILY');

    // The 3h grid only ever lands on 00/03/06/.../21 local, so of the 08/15/21 slots only 15
    // and 21 are reachable - 2 slots x 3 wanted days (Sep 5-7), each -3h to UTC.
    expect(daily.map((f) => f.timestamp.toISOString())).toEqual([
      '2026-09-05T12:00:00.000Z',
      '2026-09-05T18:00:00.000Z',
      '2026-09-06T12:00:00.000Z',
      '2026-09-06T18:00:00.000Z',
      '2026-09-07T12:00:00.000Z',
      '2026-09-07T18:00:00.000Z',
    ]);
    expect(daily.every((f) => f.danger === false && f.offsetMinutes === 180)).toBe(true);
    // Today's 15:00 slot is the CURRENT row - it must not also appear as DAILY.
    expect(daily.some((f) => f.timestamp.toISOString() === '2026-09-04T12:00:00.000Z')).toBe(false);
  });
});
