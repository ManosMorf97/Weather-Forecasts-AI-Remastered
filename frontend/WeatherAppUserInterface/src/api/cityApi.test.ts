import { beforeEach, describe, expect, it, vi } from 'vitest';
import { searchCities } from './cityApi';

function jsonResponse(body: unknown, ok = true, status = 200): Response {
  return {
    ok,
    status,
    json: () => Promise.resolve(body),
  } as Response;
}

beforeEach(() => {
  vi.stubGlobal('fetch', vi.fn());
});

describe('searchCities (UC4 step 3)', () => {
  it('maps matching results to GeocodedCity', async () => {
    vi.mocked(fetch).mockResolvedValue(
      jsonResponse({
        results: [{ name: 'Athens', country: 'Greece', latitude: 37.98, longitude: 23.73 }],
      }),
    );

    const results = await searchCities('Athens');

    expect(results).toEqual([{ name: 'Athens', country: 'Greece', latitude: 37.98, longitude: 23.73 }]);
    const requestedUrl = vi.mocked(fetch).mock.calls[0][0] as URL;
    expect(requestedUrl.origin + requestedUrl.pathname).toBe('https://geocoding-api.open-meteo.com/v1/search');
    expect(requestedUrl.searchParams.get('name')).toBe('Athens');
  });

  it('A1: returns an empty list when the API has no matches', async () => {
    vi.mocked(fetch).mockResolvedValue(jsonResponse({}));

    const results = await searchCities('zzzzz');

    expect(results).toEqual([]);
  });

  it('drops results with no country, since City.Country is required', async () => {
    vi.mocked(fetch).mockResolvedValue(
      jsonResponse({
        results: [{ name: 'Nowhere', latitude: 0, longitude: 0 }],
      }),
    );

    const results = await searchCities('Nowhere');

    expect(results).toEqual([]);
  });

  it('E1: retries once before throwing', async () => {
    vi.mocked(fetch)
      .mockRejectedValueOnce(new Error('network error'))
      .mockResolvedValueOnce(
        jsonResponse({ results: [{ name: 'Rome', country: 'Italy', latitude: 41.9, longitude: 12.5 }] }),
      );

    const results = await searchCities('Rome');

    expect(results).toEqual([{ name: 'Rome', country: 'Italy', latitude: 41.9, longitude: 12.5 }]);
    expect(fetch).toHaveBeenCalledTimes(2);
  });

  it('throws after the retry also fails', async () => {
    vi.mocked(fetch).mockResolvedValue(jsonResponse({}, false, 500));

    await expect(searchCities('Rome')).rejects.toThrow('City search failed (status 500)');
    expect(fetch).toHaveBeenCalledTimes(2);
  });
});
