import type { CityInput, NormalizedForecast, WeatherProvider } from './types.js';

// TODO(next tranche): implement against the free 5-day / 3-hour endpoint.
//
//   GET https://api.openweathermap.org/data/2.5/forecast
//       ?lat={lat}&lon={lon}&units=metric&appid={key}
//
// Response notes:
//   - `list[]`: 3-hourly steps. Each: dt (UTC seconds), main.temp (C), main.humidity (%),
//     wind.speed (m/s -> *3.6 for km/h).
//   - `city.timezone`: offset from UTC in seconds -> derive local 08/15/21 for DAILY, and set
//     offsetMinutes (city.timezone / 60) on every NormalizedForecast row.
//   - No alerts on this endpoint: danger is always false.
// Mapping:
//   - CURRENT  -> first `list` entry at/after now.
//   - HOURLY   -> next 3 entries (they are 3h apart, not 1h - documented deviation from "hourly").
//   - DAILY    -> entries whose local hour is 08/15/21 on the next 3 local days.
export class OpenWeatherMapProvider implements WeatherProvider {
  readonly serviceName = 'OpenWeatherMap';

  constructor(
    private readonly apiKey: string,
    private readonly timeoutMs: number,
  ) {}

  fetchForCity(_city: CityInput, _signal?: AbortSignal): Promise<NormalizedForecast[]> {
    void this.apiKey;
    void this.timeoutMs;
    return Promise.reject(new Error('OpenWeatherMapProvider not implemented yet'));
  }
}
