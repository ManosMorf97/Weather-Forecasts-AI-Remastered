import { beforeEach, describe, expect, it, vi } from 'vitest';
import { UnauthorizedError } from './profileApi';
import { getForecasts, rateForecast, removeRating } from './forecastsApi';
import { readCache, writeCache } from './apiCache';

function jsonResponse(body: unknown, ok = true, status = 200): Response {
  return {
    ok,
    status,
    json: () => Promise.resolve(body),
  } as Response;
}

beforeEach(() => {
  vi.stubGlobal('fetch', vi.fn());
  localStorage.clear();
});

describe('getForecasts', () => {
  it('fetches the forecast list with the bearer token', async () => {
    const forecasts = [
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
        userRating: null,
      },
    ];
    vi.mocked(fetch).mockResolvedValue(jsonResponse({ forecasts }));

    const result = await getForecasts('jwt-token');

    expect(result).toEqual(forecasts);
    expect(fetch).toHaveBeenCalledWith(
      expect.stringMatching(/\/api\/Forecasts$/),
      expect.objectContaining({ headers: { Authorization: 'Bearer jwt-token' } }),
    );
  });

  it('E2: throws UnauthorizedError on a 401', async () => {
    vi.mocked(fetch).mockResolvedValue(jsonResponse(null, false, 401));

    await expect(getForecasts('jwt-token')).rejects.toBeInstanceOf(UnauthorizedError);
  });

  it('throws on other failures', async () => {
    vi.mocked(fetch).mockResolvedValue(jsonResponse(null, false, 500));

    await expect(getForecasts('jwt-token')).rejects.toThrow('Failed to load forecasts (status 500)');
  });
});

describe('rateForecast', () => {
  it('PUTs the rating value as JSON', async () => {
    vi.mocked(fetch).mockResolvedValue(jsonResponse(null));

    await rateForecast('jwt-token', 42, 4);

    expect(fetch).toHaveBeenCalledWith(
      expect.stringMatching(/\/api\/Forecasts\/42\/rating$/),
      expect.objectContaining({
        method: 'PUT',
        headers: { Authorization: 'Bearer jwt-token', 'Content-Type': 'application/json' },
        body: JSON.stringify({ value: 4 }),
      }),
    );
  });

  it('E2: throws UnauthorizedError on a 401', async () => {
    vi.mocked(fetch).mockResolvedValue(jsonResponse(null, false, 401));

    await expect(rateForecast('jwt-token', 42, 4)).rejects.toBeInstanceOf(UnauthorizedError);
  });

  it('surfaces the ProblemDetails detail message on a 404', async () => {
    vi.mocked(fetch).mockResolvedValue(jsonResponse({ detail: 'No forecast exists with the given id.' }, false, 404));

    await expect(rateForecast('jwt-token', 42, 4)).rejects.toThrow('No forecast exists with the given id.');
  });

  it('falls back to a generic message when the body has no detail', async () => {
    vi.mocked(fetch).mockResolvedValue(jsonResponse(null, false, 500));

    await expect(rateForecast('jwt-token', 42, 4)).rejects.toThrow('Failed to save rating (status 500)');
  });
});

describe('removeRating', () => {
  it('DELETEs the rating', async () => {
    vi.mocked(fetch).mockResolvedValue(jsonResponse(null));

    await removeRating('jwt-token', 42);

    expect(fetch).toHaveBeenCalledWith(
      expect.stringMatching(/\/api\/Forecasts\/42\/rating$/),
      expect.objectContaining({ method: 'DELETE', headers: { Authorization: 'Bearer jwt-token' } }),
    );
  });

  it('E2: throws UnauthorizedError on a 401', async () => {
    vi.mocked(fetch).mockResolvedValue(jsonResponse(null, false, 401));

    await expect(removeRating('jwt-token', 42)).rejects.toBeInstanceOf(UnauthorizedError);
  });

  it('falls back to a generic message when the body has no detail', async () => {
    vi.mocked(fetch).mockResolvedValue(jsonResponse(null, false, 500));

    await expect(removeRating('jwt-token', 42)).rejects.toThrow('Failed to remove rating (status 500)');
  });
});


function forecastItem(forecastId: number, userRating: number | null) {
  return {
    forecastId,
    city: 'Athens',
    country: 'Greece',
    service: 'Open-Meteo',
    type: 'CURRENT' as const,
    timestamp: '2026-09-04T12:00:00Z',
    offsetMinutes: 180,
    temperature: 30.4,
    humidity: 45,
    windSpeed: 12,
    dangerFlag: false,
    userRating,
  };
}

describe('getForecasts - caching', () => {
  it('caches the forecast list so a second call does not hit the backend again', async () => {
    const forecasts = [forecastItem(1, null), forecastItem(2, 3)];
    vi.mocked(fetch).mockResolvedValue(jsonResponse({ forecasts }));

    const first = await getForecasts('jwt-token');
    const second = await getForecasts('jwt-token');

    expect(first).toEqual(forecasts);
    expect(second).toEqual(forecasts);
    expect(fetch).toHaveBeenCalledTimes(1);
    expect(readCache('forecasts')).toEqual(forecasts);
  });

  it('does not cache a failed response', async () => {
    vi.mocked(fetch).mockResolvedValue(jsonResponse(null, false, 500));

    await expect(getForecasts('jwt-token')).rejects.toThrow();

    expect(readCache('forecasts')).toBeNull();
  });
});

describe('rating - cached forecasts', () => {
  it('rateForecast patches only that forecast in the cache', async () => {
    writeCache('forecasts', [forecastItem(1, null), forecastItem(2, 3)]);
    vi.mocked(fetch).mockResolvedValue(jsonResponse(null));

    await rateForecast('jwt-token', 1, 5);

    expect(readCache('forecasts')).toEqual([forecastItem(1, 5), forecastItem(2, 3)]);
  });

  it('removeRating sets only that forecast rating to null in the cache', async () => {
    writeCache('forecasts', [forecastItem(1, 4), forecastItem(2, 3)]);
    vi.mocked(fetch).mockResolvedValue(jsonResponse(null));

    await removeRating('jwt-token', 1);

    expect(readCache('forecasts')).toEqual([forecastItem(1, null), forecastItem(2, 3)]);
  });


  // A rating can change which service has the highest average for a city (UC12), so the
  // aggregated view is dropped entirely rather than patched.
  it('rateForecast invalidates the cached aggregated forecasts', async () => {
    writeCache('aggregatedForecasts', { forecasts: [], serviceMetadata: [] });
    vi.mocked(fetch).mockResolvedValue(jsonResponse(null));

    await rateForecast('jwt-token', 1, 5);

    expect(readCache('aggregatedForecasts')).toBeNull();
  });

  it('removeRating invalidates the cached aggregated forecasts', async () => {
    writeCache('aggregatedForecasts', { forecasts: [], serviceMetadata: [] });
    vi.mocked(fetch).mockResolvedValue(jsonResponse(null));

    await removeRating('jwt-token', 1);

    expect(readCache('aggregatedForecasts')).toBeNull();
  });

  it('leaves the cached aggregated forecasts untouched when rating fails', async () => {
    writeCache('aggregatedForecasts', { forecasts: [], serviceMetadata: [] });
    vi.mocked(fetch).mockResolvedValue(jsonResponse(null, false, 500));

    await expect(rateForecast('jwt-token', 1, 5)).rejects.toThrow();

    expect(readCache('aggregatedForecasts')).toEqual({ forecasts: [], serviceMetadata: [] });
  });


  it('leaves the cache untouched when rating fails', async () => {
    writeCache('forecasts', [forecastItem(1, null)]);
    vi.mocked(fetch).mockResolvedValue(jsonResponse(null, false, 500));

    await expect(rateForecast('jwt-token', 1, 5)).rejects.toThrow();

    expect(readCache('forecasts')).toEqual([forecastItem(1, null)]);
  });

  it('does not create a cache entry when nothing was cached', async () => {
    vi.mocked(fetch).mockResolvedValue(jsonResponse(null));

    await rateForecast('jwt-token', 1, 5);

    expect(readCache('forecasts')).toBeNull();
  });
});