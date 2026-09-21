import { beforeEach, describe, expect, it, vi } from 'vitest';
import { clearApiCache, invalidateCache, readCache, writeCache } from './apiCache';

beforeEach(() => {
  vi.restoreAllMocks();
  localStorage.clear();
});

describe('readCache / writeCache', () => {
  it('returns null when nothing is cached', () => {
    expect(readCache('selections')).toBeNull();
  });

  it('returns what was written', () => {
    const selections = { services: [{ serviceId: 1, name: 'Open-Meteo', selected: true }], cities: [] };

    writeCache('selections', selections);

    expect(readCache('selections')).toEqual(selections);
  });

  it('keeps each key separate', () => {
    writeCache('selections', { services: [], cities: [] });
    writeCache('forecasts', []);

    expect(readCache('selections')).toEqual({ services: [], cities: [] });
    expect(readCache('forecasts')).toEqual([]);
  });

  it('returns null when the stored value is not valid JSON', () => {
    localStorage.setItem('weatherApp.cache.v1.selections', '{not json');

    expect(readCache('selections')).toBeNull();
  });

  it('falls back to "not cached" when storage throws', () => {
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new Error('storage blocked');
    });
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('storage blocked');
    });

    expect(() => writeCache('selections', { services: [], cities: [] })).not.toThrow();
    expect(readCache('selections')).toBeNull();
  });
});

describe('invalidateCache', () => {
  it('removes only the given keys', () => {
    writeCache('selections', { services: [], cities: [] });
    writeCache('forecasts', []);

    invalidateCache('selections');

    expect(readCache('selections')).toBeNull();
    expect(readCache('forecasts')).toEqual([]);
  });

  it('removes several keys at once', () => {
    writeCache('selections', { services: [], cities: [] });
    writeCache('forecasts', []);

    invalidateCache('selections', 'forecasts');

    expect(readCache('selections')).toBeNull();
    expect(readCache('forecasts')).toBeNull();
  });
});

describe('clearApiCache', () => {
  it('removes every cached entry but leaves unrelated localStorage keys alone', () => {
    writeCache('selections', { services: [], cities: [] });
    writeCache('forecasts', []);
    localStorage.setItem('unrelated', 'keep me');

    clearApiCache();

    expect(readCache('selections')).toBeNull();
    expect(readCache('forecasts')).toBeNull();
    expect(localStorage.getItem('unrelated')).toBe('keep me');
  });
});
