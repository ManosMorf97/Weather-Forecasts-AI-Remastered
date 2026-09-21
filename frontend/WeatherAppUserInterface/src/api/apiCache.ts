// localStorage cache for GET responses from WeatherUserActions, so pages that need the same
// data don't hit the backend again. An entry stays until the user changes the underlying data
// (save selections/services, rate a forecast) or the session ends (see clearApiCache).
export type CacheKey = 'selections' | 'forecasts';

// Bump the version if a cached DTO's shape changes, so old entries are ignored.
const KEY_PREFIX = 'weatherApp.cache.v1.';

// Storage can be missing or throw (private window, blocked site data) - the cache is only an
// optimization, so every failure falls back to "not cached".
export function readCache<T>(key: CacheKey): T | null {
  try {
    const raw = localStorage.getItem(KEY_PREFIX + key);
    return raw === null ? null : (JSON.parse(raw) as T);
  } catch {
    return null;
  }
}

export function writeCache<T>(key: CacheKey, value: T): void {
  try {
    localStorage.setItem(KEY_PREFIX + key, JSON.stringify(value));
  } catch {
    // Not cached - the next read just goes to the backend.
  }
}

export function invalidateCache(...keys: CacheKey[]): void {
  try {
    keys.forEach((key) => localStorage.removeItem(KEY_PREFIX + key));
  } catch {
    // Nothing to invalidate if storage is unavailable.
  }
}

// Called on login/register/logout/session loss so one user's data is never shown to the next.
export function clearApiCache(): void {
  try {
    Object.keys(localStorage)
      .filter((key) => key.startsWith(KEY_PREFIX))
      .forEach((key) => localStorage.removeItem(key));
  } catch {
    // Nothing to clear if storage is unavailable.
  }
}
