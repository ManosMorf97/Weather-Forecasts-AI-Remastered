import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { OpenMeteoProvider } from '../../src/providers/openMeteo.js';
import type { CityInput } from '../../src/providers/types.js';

// Athens: UTC+3 in summer (utc_offset_seconds = 10800). "Now" is pinned so the CURRENT /
// HOURLY / DAILY selection is deterministic.
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
  // Hourly grid: every hour for 4 local days from 00:00 on 2026-09-04.
  const times: string[] = [];
  const temps: number[] = [];
  const humidity: number[] = [];
  const wind: number[] = [];
  for (let day = 4; day <= 7; day++) {
    for (let hour = 0; hour < 24; hour++) {
      times.push(`2026-09-0${day}T${String(hour).padStart(2, '0')}:00`);
      temps.push(20 + hour * 0.1);
      humidity.push(50);
      wind.push(10);
    }
  }
  return {
    utc_offset_seconds: 10_800,
    current: {
      time: '2026-09-04T15:00',
      temperature_2m: 30.4,
      relative_humidity_2m: 45,
      wind_speed_10m: 12,
    },
    hourly: { time: times, temperature_2m: temps, relative_humidity_2m: humidity, wind_speed_10m: wind },
  };
}

describe('OpenMeteoProvider', () => {
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

  it('returns one CURRENT reading with normalized units', async () => {
    const provider = new OpenMeteoProvider(15_000);

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
    // 15:00 Athens (UTC+3) => 12:00 UTC
    expect(current[0]?.timestamp.toISOString()).toBe('2026-09-04T12:00:00.000Z');
  });

  it('returns the next 3 hourly readings after now', async () => {
    const provider = new OpenMeteoProvider(15_000);

    const hourly = (await provider.fetchForCity(ATHENS)).filter((f) => f.type === 'HOURLY');

    expect(hourly.map((f) => f.timestamp.toISOString())).toEqual([
      '2026-09-04T13:00:00.000Z', // 16:00 Athens
      '2026-09-04T14:00:00.000Z',
      '2026-09-04T15:00:00.000Z',
    ]);
    expect(hourly.every((f) => f.offsetMinutes === 180)).toBe(true);
  });

  it('returns 3 daily slots (08/15/21 local) for each of the next 3 days', async () => {
    const provider = new OpenMeteoProvider(15_000);

    const daily = (await provider.fetchForCity(ATHENS)).filter((f) => f.type === 'DAILY');

    // 3 slots (08/15/21 Athens => 05/12/18 UTC) x 3 wanted days (Sep 5-7).
    expect(daily.map((f) => f.timestamp.toISOString())).toEqual([
      '2026-09-05T05:00:00.000Z',
      '2026-09-05T12:00:00.000Z',
      '2026-09-05T18:00:00.000Z',
      '2026-09-06T05:00:00.000Z',
      '2026-09-06T12:00:00.000Z',
      '2026-09-06T18:00:00.000Z',
      '2026-09-07T05:00:00.000Z',
      '2026-09-07T12:00:00.000Z',
      '2026-09-07T18:00:00.000Z',
    ]);
    expect(daily.every((f) => f.danger === false)).toBe(true);
    expect(daily.every((f) => f.offsetMinutes === 180)).toBe(true);
  });
});
