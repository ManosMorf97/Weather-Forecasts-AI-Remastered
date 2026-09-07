import type { CityInput, NormalizedForecast, WeatherProvider } from './types.js';

// TODO(next tranche): implement against the Timeline API.
//
//   GET https://weather.visualcrossing.com/VisualCrossing/rest/services/timeline/{lat},{lon}
//       ?unitGroup=metric&include=current,hours,alerts&key={key}
//
// Response notes:
//   - `currentConditions`: temp (C), humidity (%), windspeed (km/h with unitGroup=metric).
//   - `days[].hours[]`: datetime ("HH:mm:ss"), datetimeEpoch (UTC s), temp, humidity, windspeed.
//   - `tzoffset`: hours from UTC (may be fractional) -> offsetMinutes = tzoffset * 60, set on every
//     NormalizedForecast row.
//   - `alerts[]`: { severity?, event, onset, ends, ... }. severity is not always present -
//     when absent, fall back to treating any alert as non-danger unless `event` clearly maps.
// Mapping: CURRENT -> currentConditions; HOURLY -> next 3 hours; DAILY -> hours at local
// 08/15/21 across the next 3 days.
export class VisualCrossingProvider implements WeatherProvider {
  readonly serviceName = 'Visual Crossing';

  constructor(
    private readonly apiKey: string,
    private readonly dangerSeverities: ReadonlySet<string>,
    private readonly timeoutMs: number,
  ) {}

  fetchForCity(_city: CityInput, _signal?: AbortSignal): Promise<NormalizedForecast[]> {
    void this.apiKey;
    void this.dangerSeverities;
    void this.timeoutMs;
    return Promise.reject(new Error('VisualCrossingProvider not implemented yet'));
  }
}
