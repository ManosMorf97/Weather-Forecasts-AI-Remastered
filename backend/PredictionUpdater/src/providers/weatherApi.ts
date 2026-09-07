import type { CityInput, NormalizedForecast, WeatherProvider } from './types.js';

// TODO(next tranche): implement against forecast.json with alerts.
//
//   GET https://api.weatherapi.com/v1/forecast.json
//       ?key={key}&q={lat},{lon}&days=3&alerts=yes&aqi=no
//
// Response notes:
//   - `current`: temp_c, humidity, wind_kph.
//   - `forecast.forecastday[].hour[]`: time (local "YYYY-MM-DD HH:mm"), time_epoch (UTC s),
//     temp_c, humidity, wind_kph.
//   - `location.tz_id` / `location.localtime_epoch` for local-time handling; derive offsetMinutes
//     from them and set it on every NormalizedForecast row.
//   - `alerts.alert[]`: { severity, effective, expires, ... }. severity is a CAP value
//     ("Extreme" / "Severe" / ...). Compare against config.dangerSeverities; if a matching
//     alert's [effective, expires] window covers a forecast instant, set danger = true.
// Mapping: CURRENT -> `current`; HOURLY -> next 3 `hour` entries; DAILY -> hour entries at
// local 08/15/21 on the 3 forecast days.
export class WeatherApiProvider implements WeatherProvider {
  readonly serviceName = 'WeatherAPI';

  constructor(
    private readonly apiKey: string,
    private readonly dangerSeverities: ReadonlySet<string>,
    private readonly timeoutMs: number,
  ) {}

  fetchForCity(_city: CityInput, _signal?: AbortSignal): Promise<NormalizedForecast[]> {
    void this.apiKey;
    void this.dangerSeverities;
    void this.timeoutMs;
    return Promise.reject(new Error('WeatherApiProvider not implemented yet'));
  }
}
