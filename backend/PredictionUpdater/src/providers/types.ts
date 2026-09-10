// The shape every weather-provider adapter normalises its response into, and the contract
// the poll step depends on. Units are fixed here: Celsius, percent, km/h, UTC instants.

export type ForecastType = 'CURRENT' | 'HOURLY' | 'DAILY';

export interface CityInput {
  cityId: number;
  citySiteId: number;
  name: string;
  country: string;
  latitude: number;
  longitude: number;
}

export interface NormalizedForecast {
  type: ForecastType;
  /** UTC instant. For DAILY this is the city-local 08:00 / 15:00 / 21:00 converted to UTC. */
  timestamp: Date;
  /** Minutes east of UTC for the city's location at `timestamp` (e.g. Athens summer = 180). */
  offsetMinutes: number;
  temperatureC: number;
  humidityPct: number;
  windSpeedKmh: number;
  /**
   * True when this provider judges the instant life-threatening. Each adapter decides from
   * whatever danger signal its API exposes (e.g. an official CAP alert of Extreme/Severe);
   * an adapter with no such signal reports false. The poll/notify pipeline treats every
   * provider the same - it never assumes which services can or cannot raise danger.
   */
  danger: boolean;
}

export interface WeatherProvider {
  /** Must equal the ForecastingServices.Name row this adapter serves. */
  readonly serviceName: string;

  /** UC11 steps 4-5. Throws on transport / HTTP / validation failure (the poll step logs it). */
  fetchForCity(city: CityInput, signal?: AbortSignal): Promise<NormalizedForecast[]>;
}
