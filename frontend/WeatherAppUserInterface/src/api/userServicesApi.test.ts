import { beforeEach, describe, expect, it, vi } from 'vitest';
import { UnauthorizedError } from './profileApi';
import { saveServices } from './userServicesApi';
// NEW TICKET start
import { readCache, writeCache } from './apiCache';
// NEW TICKET end

function jsonResponse(body: unknown, ok = true, status = 200): Response {
  return {
    ok,
    status,
    json: () => Promise.resolve(body),
  } as Response;
}

beforeEach(() => {
  vi.stubGlobal('fetch', vi.fn());
  // NEW TICKET start
  localStorage.clear();
  // NEW TICKET end
});

describe('saveServices', () => {
  const serviceIds = [1, 2];

  it('PUTs the service ids as JSON to the UserServices endpoint', async () => {
    vi.mocked(fetch).mockResolvedValue(jsonResponse(null));

    await saveServices('jwt-token', serviceIds);

    expect(fetch).toHaveBeenCalledWith(
      expect.stringMatching(/\/api\/UserServices$/),
      expect.objectContaining({
        method: 'PUT',
        headers: { Authorization: 'Bearer jwt-token', 'Content-Type': 'application/json' },
        body: JSON.stringify({ serviceIds }),
      }),
    );
  });

  it('E2: throws UnauthorizedError on a 401', async () => {
    vi.mocked(fetch).mockResolvedValue(jsonResponse(null, false, 401));

    await expect(saveServices('jwt-token', serviceIds)).rejects.toBeInstanceOf(UnauthorizedError);
  });

  it('surfaces the ProblemDetails detail message on a 400', async () => {
    vi.mocked(fetch).mockResolvedValue(
      jsonResponse({ detail: 'One or more selected forecasting services do not exist.' }, false, 400),
    );

    await expect(saveServices('jwt-token', serviceIds)).rejects.toThrow(
      'One or more selected forecasting services do not exist.',
    );
  });

  it('falls back to a generic message when the body has no detail', async () => {
    vi.mocked(fetch).mockResolvedValue(jsonResponse(null, false, 500));

    await expect(saveServices('jwt-token', serviceIds)).rejects.toThrow(
      'Failed to save services (status 500)',
    );
  });

  // NEW TICKET start
  it('invalidates the cached selections, forecasts and aggregated forecasts after a successful save', async () => {
    writeCache('selections', { services: [], cities: [] });
    writeCache('forecasts', []);
    writeCache('aggregatedForecasts', { forecasts: [], serviceMetadata: [] });
    vi.mocked(fetch).mockResolvedValue(jsonResponse(null));

    await saveServices('jwt-token', serviceIds);

    expect(readCache('selections')).toBeNull();
    expect(readCache('forecasts')).toBeNull();
    expect(readCache('aggregatedForecasts')).toBeNull();
  });

  it('keeps the cache when the save fails', async () => {
    writeCache('selections', { services: [], cities: [] });
    writeCache('forecasts', []);
    writeCache('aggregatedForecasts', { forecasts: [], serviceMetadata: [] });
    vi.mocked(fetch).mockResolvedValue(jsonResponse(null, false, 500));

    await expect(saveServices('jwt-token', serviceIds)).rejects.toThrow();

    expect(readCache('selections')).toEqual({ services: [], cities: [] });
    expect(readCache('forecasts')).toEqual([]);
    expect(readCache('aggregatedForecasts')).toEqual({ forecasts: [], serviceMetadata: [] });
  });
  // NEW TICKET end
});
