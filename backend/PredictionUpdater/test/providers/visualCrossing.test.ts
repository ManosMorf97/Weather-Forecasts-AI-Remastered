import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { VisualCrossingProvider } from '../../src/providers/visualCrossing.js';
import type { CityInput } from '../../src/providers/types.js';

// Athens: UTC+3 in summer (tzoffset = 3). "Now" is pinned so the CURRENT / HOURLY / DAILY
// selection is deterministic.
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

// Hourly grid, every local hour, for local days Sep 4-7 - deliberately more days than we want,
// mirroring how the Timeline API returns extra days when no date range is given; the adapter
// must self-filter down to the next 3 local days.
function buildResponse(alerts: Array<{ severity?: string; event: string; onset: string; ends?: string }> = []) {
  const days = [4, 5, 6, 7].map((day) => ({
    datetime: `2026-09-${pad(day)}`,
    hours: Array.from({ length: 24 }, (_, hour) => {
      const utcMs = Date.UTC(2026, 8, day, hour, 0, 0) - 180 * 60_000; // local -3h = UTC
      return {
        datetimeEpoch: utcMs / 1000,
        temp: 20 + hour * 0.1,
        humidity: 50,
        windspeed: 10,
      };
    }),
  }));

  return {
    tzoffset: 3,
    currentConditions: { datetimeEpoch: NOW.getTime() / 1000, temp: 30.4, humidity: 45, windspeed: 12 },
    days,
    alerts,
  };
}

function stubFetch(body: unknown) {
  vi.stubGlobal(
    'fetch',
    vi.fn(async () => new Response(JSON.stringify(body), { status: 200 })),
  );
}

describe('VisualCrossingProvider', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(NOW);
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.unstubAllGlobals();
  });

  it('requests a UTC date range wide enough to cover any local "next 3 days", not the Timeline API default', async () => {
    stubFetch(buildResponse());
    const provider = new VisualCrossingProvider('key', new Set(['extreme']), 15_000);

    await provider.fetchForCity(ATHENS);

    const requestedUrl = vi.mocked(fetch).mock.calls[0]?.[0] as URL;
    // NOW is 2026-09-04T12:00:00Z: yesterday UTC through 4 days ahead UTC.
    expect(requestedUrl.pathname).toBe(
      '/VisualCrossing/rest/services/timeline/37.9838,23.7275/2026-09-03/2026-09-08',
    );
  });

  it('returns one CURRENT reading with normalized units', async () => {
    stubFetch(buildResponse());
    const provider = new VisualCrossingProvider('key', new Set(['extreme']), 15_000);

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
    const provider = new VisualCrossingProvider('key', new Set(['extreme']), 15_000);

    const hourly = (await provider.fetchForCity(ATHENS)).filter((f) => f.type === 'HOURLY');

    expect(hourly.map((f) => f.timestamp.toISOString())).toEqual([
      '2026-09-04T13:00:00.000Z', // 16:00 Athens
      '2026-09-04T14:00:00.000Z',
      '2026-09-04T15:00:00.000Z',
    ]);
    expect(hourly.every((f) => f.offsetMinutes === 180 && f.danger === false)).toBe(true);
  });

  it('returns 08/15/21-local slots only for the next 3 local days, ignoring today and the extra 4th day', async () => {
    stubFetch(buildResponse());
    const provider = new VisualCrossingProvider('key', new Set(['extreme']), 15_000);

    const daily = (await provider.fetchForCity(ATHENS)).filter((f) => f.type === 'DAILY');

    // 3 slots (08/15/21 Athens => 05/12/18 UTC) x 3 wanted days (Sep 5-7) - today (Sep 4) and
    // the extra 4th day (Sep 8) are excluded by this exact list, not just spot-checked.
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
  });

  it('marks a slot as danger only when an alert of a configured severity covers it', async () => {
    stubFetch(
      buildResponse([
        { severity: 'Extreme', event: 'Heat Wave', onset: '2026-09-05T10:00:00Z', ends: '2026-09-05T20:00:00Z' },
      ]),
    );
    const provider = new VisualCrossingProvider('key', new Set(['extreme']), 15_000);

    const daily = (await provider.fetchForCity(ATHENS)).filter((f) => f.type === 'DAILY');
    const bySep5Hour = (h: number) => daily.find((f) => f.timestamp.toISOString().startsWith(`2026-09-05T${pad(h)}`));

    expect(bySep5Hour(12)?.danger).toBe(true); // 15:00 Athens, inside the window
    expect(bySep5Hour(5)?.danger).toBe(false); // 08:00 Athens, before onset
  });

  it('treats an alert with no severity as non-danger (safe default)', async () => {
    stubFetch(
      buildResponse([{ event: 'Heat Wave', onset: '2026-09-05T10:00:00Z', ends: '2026-09-05T20:00:00Z' }]),
    );
    const provider = new VisualCrossingProvider('key', new Set(['extreme']), 15_000);

    const forecasts = await provider.fetchForCity(ATHENS);

    expect(forecasts.every((f) => f.danger === false)).toBe(true);
  });

  it('treats a missing `ends` as an open-ended window', async () => {
    stubFetch(buildResponse([{ severity: 'Extreme', event: 'Heat Wave', onset: '2026-09-05T00:00:00Z' }]));
    const provider = new VisualCrossingProvider('key', new Set(['extreme']), 15_000);

    const daily = (await provider.fetchForCity(ATHENS)).filter((f) => f.type === 'DAILY');
    const sep7Hour21 = daily.find((f) => f.timestamp.toISOString() === '2026-09-07T18:00:00.000Z');

    expect(sep7Hour21?.danger).toBe(true);
  });
});
