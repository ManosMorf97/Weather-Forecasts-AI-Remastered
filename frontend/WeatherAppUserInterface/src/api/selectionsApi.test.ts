import { beforeEach, describe, expect, it, vi } from 'vitest';
import { UnauthorizedError } from './profileApi';
import { getSelections, saveSelections } from './selectionsApi';
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

describe('getSelections', () => {
  it('fetches the current selections with the bearer token', async () => {
    const body = {
      services: [{ serviceId: 1, name: 'Open-Meteo', selected: true }],
      cities: [{ name: 'Athens', country: 'Greece', latitude: 37.98, longitude: 23.73 }],
    };
    vi.mocked(fetch).mockResolvedValue(jsonResponse(body));

    const result = await getSelections('jwt-token');

    expect(result).toEqual(body);
    expect(fetch).toHaveBeenCalledWith(
      expect.stringMatching(/\/api\/Selections$/),
      expect.objectContaining({ headers: { Authorization: 'Bearer jwt-token' } }),
    );
  });

  it('E2: throws UnauthorizedError on a 401', async () => {
    vi.mocked(fetch).mockResolvedValue(jsonResponse(null, false, 401));

    await expect(getSelections('jwt-token')).rejects.toBeInstanceOf(UnauthorizedError);
  });

  it('throws on other failures', async () => {
    vi.mocked(fetch).mockResolvedValue(jsonResponse(null, false, 500));

    await expect(getSelections('jwt-token')).rejects.toThrow('Failed to load selections (status 500)');
  });

  it('caches the response so a second call does not hit the backend again', async () => {
    const body = {
      services: [{ serviceId: 1, name: 'Open-Meteo', selected: true }],
      cities: [{ name: 'Athens', country: 'Greece', latitude: 37.98, longitude: 23.73 }],
    };
    vi.mocked(fetch).mockResolvedValue(jsonResponse(body));

    const first = await getSelections('jwt-token');
    const second = await getSelections('jwt-token');

    expect(first).toEqual(body);
    expect(second).toEqual(body);
    expect(fetch).toHaveBeenCalledTimes(1);
    expect(readCache('selections')).toEqual(body);
  });

  it('does not cache a failed response', async () => {
    vi.mocked(fetch).mockResolvedValue(jsonResponse(null, false, 500));

    await expect(getSelections('jwt-token')).rejects.toThrow();

    expect(readCache('selections')).toBeNull();
  });
});

describe('saveSelections', () => {
  const cities = [{ name: 'Athens', country: 'Greece', latitude: 37.98, longitude: 23.73 }];
  const serviceIds = [1, 2];

  it('PUTs the full selection as JSON', async () => {
    vi.mocked(fetch).mockResolvedValue(jsonResponse(null));

    await saveSelections('jwt-token', cities, serviceIds);

    expect(fetch).toHaveBeenCalledWith(
      expect.stringMatching(/\/api\/Selections$/),
      expect.objectContaining({
        method: 'PUT',
        headers: { Authorization: 'Bearer jwt-token', 'Content-Type': 'application/json' },
        body: JSON.stringify({ cities, serviceIds }),
      }),
    );
  });

  it('E2: throws UnauthorizedError on a 401', async () => {
    vi.mocked(fetch).mockResolvedValue(jsonResponse(null, false, 401));

    await expect(saveSelections('jwt-token', cities, serviceIds)).rejects.toBeInstanceOf(UnauthorizedError);
  });

  it('surfaces the ProblemDetails detail message on a 400', async () => {
    vi.mocked(fetch).mockResolvedValue(
      jsonResponse({ detail: 'One or more selected forecasting services do not exist.' }, false, 400),
    );

    await expect(saveSelections('jwt-token', cities, serviceIds)).rejects.toThrow(
      'One or more selected forecasting services do not exist.',
    );
  });

  it('falls back to a generic message when the body has no detail', async () => {
    vi.mocked(fetch).mockResolvedValue(jsonResponse(null, false, 500));

    await expect(saveSelections('jwt-token', cities, serviceIds)).rejects.toThrow(
      'Failed to save selections (status 500)',
    );
  });

  it('invalidates the cached selections and forecasts after a successful save', async () => {
    writeCache('selections', { services: [], cities: [] });
    writeCache('forecasts', []);
    vi.mocked(fetch).mockResolvedValue(jsonResponse(null));

    await saveSelections('jwt-token', cities, serviceIds);

    expect(readCache('selections')).toBeNull();
    expect(readCache('forecasts')).toBeNull();
  });

  it('keeps the cache when the save fails', async () => {
    writeCache('selections', { services: [], cities: [] });
    writeCache('forecasts', []);
    vi.mocked(fetch).mockResolvedValue(jsonResponse(null, false, 500));

    await expect(saveSelections('jwt-token', cities, serviceIds)).rejects.toThrow();

    expect(readCache('selections')).toEqual({ services: [], cities: [] });
    expect(readCache('forecasts')).toEqual([]);
  });
});
