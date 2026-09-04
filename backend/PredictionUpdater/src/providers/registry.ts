import { loadConfig, type Config } from '../config.js';
import { logger } from '../logging/logger.js';
import { OpenMeteoProvider } from './openMeteo.js';
import { OpenWeatherMapProvider } from './openWeatherMap.js';
import { VisualCrossingProvider } from './visualCrossing.js';
import { WeatherApiProvider } from './weatherApi.js';
import type { WeatherProvider } from './types.js';

// Maps ForecastingServices.Name -> the adapter that polls it. A (city, service) selection
// whose service name is absent here is logged and skipped by the poll step (so a service
// row with no key configured simply does not get polled).
export function buildProviderRegistry(config: Config = loadConfig()): Map<string, WeatherProvider> {
  const providers: WeatherProvider[] = [new OpenMeteoProvider(config.HTTP_TIMEOUT_MS)];

  if (config.OPENWEATHERMAP_API_KEY) {
    providers.push(new OpenWeatherMapProvider(config.OPENWEATHERMAP_API_KEY, config.HTTP_TIMEOUT_MS));
  }
  if (config.WEATHERAPI_API_KEY) {
    providers.push(
      new WeatherApiProvider(config.WEATHERAPI_API_KEY, config.dangerSeverities, config.HTTP_TIMEOUT_MS),
    );
  }
  if (config.VISUALCROSSING_API_KEY) {
    providers.push(
      new VisualCrossingProvider(config.VISUALCROSSING_API_KEY, config.dangerSeverities, config.HTTP_TIMEOUT_MS),
    );
  }

  const registry = new Map(providers.map((p) => [p.serviceName, p]));
  logger.info({ services: [...registry.keys()] }, 'provider registry built');
  return registry;
}
