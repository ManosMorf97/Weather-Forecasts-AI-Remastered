import { beforeEach, describe, expect, it, vi } from 'vitest';
import { UnauthorizedError } from './profileApi';
import { getAggregatedForecasts } from './aggregatedForecastsApi';
import { readCache } from './apiCache';

function jsonResponse(body: unknown, ok = true, status = 200): Response {
  return {
    ok,
    status,
    json: () => Promise.resolve(body),
  } as Response;
}

function sampleBody() {
  return {
    forecasts: [
      {
        forecastId: 1,
        city: 'Athens',
        country: 'Greece',
        service: 'Open-Meteo',
        type: 'CURRENT',
        timestamp: '2026-09-04T12:00:00Z',
        offsetMinutes: 180,
        temperature: 30.4,
        humidity: 45,
        windSpeed: 12,
        dangerFlag: false,
      },
    ],
    serviceMetadata: [
      {
        city: 'Athens',
        service: 'Open-Meteo',
        averageRating: 4.5,
        ratingCount: 2,
        aggregationApplicable: true,
        isTie: false,
        isUnratedSelection: false,
      },
    ],
  };
}

beforeEach(() => {
  vi.stubGlobal('fetch', vi.fn());
  localStorage.clear();
});

describe('getAggregatedForecasts', () => {
  it('fetches the aggregated forecasts with the bearer token', async () => {
    const body = sampleBody();
    vi.mocked(fetch).mockResolvedValue(jsonResponse(body));

    const result = await getAggregatedForecasts('jwt-token');

    expect(result).toEqual(body);
    expect(fetch).toHaveBeenCalledWith(
      expect.stringMatching(/\/api\/Forecasts\/aggregated$/),
      expect.objectContaining({ headers: { Authorization: 'Bearer jwt-token' } }),
    );
  });

  it('E2: throws UnauthorizedError on a 401', async () => {
    vi.mocked(fetch).mockResolvedValue(jsonResponse(null, false, 401));

    await expect(getAggregatedForecasts('jwt-token')).rejects.toBeInstanceOf(UnauthorizedError);
  });

  it('throws on other failures', async () => {
    vi.mocked(fetch).mockResolvedValue(jsonResponse(null, false, 500));

    await expect(getAggregatedForecasts('jwt-token')).rejects.toThrow(
      'Failed to load aggregated forecasts (status 500)',
    );
  });

  it('caches the response so a second call does not hit the backend again', async () => {
    const body = sampleBody();
    vi.mocked(fetch).mockResolvedValue(jsonResponse(body));

    const first = await getAggregatedForecasts('jwt-token');
    const second = await getAggregatedForecasts('jwt-token');

    expect(first).toEqual(body);
    expect(second).toEqual(body);
    expect(fetch).toHaveBeenCalledTimes(1);
    expect(readCache('aggregatedForecasts')).toEqual(body);
  });

  it('does not cache a failed response', async () => {
    vi.mocked(fetch).mockResolvedValue(jsonResponse(null, false, 500));

    await expect(getAggregatedForecasts('jwt-token')).rejects.toThrow();

    expect(readCache('aggregatedForecasts')).toBeNull();
  });
  it('does not cache a failed response from unauthorized', async () => {
    vi.mocked(fetch).mockResolvedValue(jsonResponse(null, false, 401));

    await expect(getAggregatedForecasts('jwt-token')).rejects.toBeInstanceOf(UnauthorizedError);

    expect(readCache('aggregatedForecasts')).toBeNull();
  });
});