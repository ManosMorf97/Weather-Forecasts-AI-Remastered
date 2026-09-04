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
  temperatureC: number;
  humidityPct: number;
  windSpeedKmh: number;
  /**
   * True only when the provider supplied an official CAP alert (Extreme/Severe) covering this
   * instant. Providers without an alerts feed (Open-Meteo, OpenWeatherMap /forecast) always
   * report false - see the services discussion.
   */
  danger: boolean;
}

export interface WeatherProvider {
  /** Must equal the ForecastingServices.Name row this adapter serves. */
  readonly serviceName: string;

  /** UC11 steps 4-5. Throws on transport / HTTP / validation failure (the poll step logs it). */
  fetchForCity(city: CityInput, signal?: AbortSignal): Promise<NormalizedForecast[]>;
}
