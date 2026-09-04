import { logger } from '../logging/logger.js';
import type { ServiceSelection } from '../repositories/selectionsRepository.js';
import type { NormalizedForecast, WeatherProvider } from '../providers/types.js';

export interface CitySiteForecasts {
  citySiteId: number;
  serviceName: string;
  forecasts: NormalizedForecast[];
}

export interface PollSummary {
  servicesAttempted: number;
  servicesSucceeded: number;
  failedServices: string[];
  results: CitySiteForecasts[];
}

// UC11 steps 3-6. A1 (a provider failing) is logged and skipped without stopping other
// services or cities. A service counts as "succeeded" if at least one of its cities returned.
export async function pollForecasts(
  selections: ServiceSelection[],
  registry: Map<string, WeatherProvider>,
): Promise<PollSummary> {
  const results: CitySiteForecasts[] = [];
  const failedServices: string[] = [];
  let servicesSucceeded = 0;

  for (const selection of selections) {
    const provider = registry.get(selection.serviceName);
    if (!provider) {
      logger.warn({ service: selection.serviceName }, 'no provider registered for service; skipping');
      failedServices.push(selection.serviceName);
      continue;
    }

    let anyCitySucceeded = false;
    for (const city of selection.cities) {
      try {
        const fetched = await provider.fetchForCity(city);
        const valid = fetched.filter(isPhysicallyValid);
        if (valid.length !== fetched.length) {
          logger.warn(
            { service: selection.serviceName, cityId: city.cityId, dropped: fetched.length - valid.length },
            'dropped invalid forecast rows',
          );
        }
        results.push({ citySiteId: city.citySiteId, serviceName: selection.serviceName, forecasts: valid });
        anyCitySucceeded = true;
      } catch (err) {
        logger.error(
          { err, service: selection.serviceName, cityId: city.cityId },
          'provider fetch failed',
        );
      }
    }

    if (anyCitySucceeded) servicesSucceeded++;
    else failedServices.push(selection.serviceName);
  }

  return {
    servicesAttempted: selections.length,
    servicesSucceeded,
    failedServices,
    results,
  };
}

// UC11 step 6: reject values that cannot be real. Also keeps rows inside the DB check
// constraints (Temperature -90..60, Humidity 0..100, WindSpeed 0..253).
function isPhysicallyValid(f: NormalizedForecast): boolean {
  return (
    Number.isFinite(f.temperatureC) && f.temperatureC >= -90 && f.temperatureC <= 60 &&
    Number.isFinite(f.humidityPct) && f.humidityPct >= 0 && f.humidityPct <= 100 &&
    Number.isFinite(f.windSpeedKmh) && f.windSpeedKmh >= 0 && f.windSpeedKmh <= 253 &&
    f.timestamp instanceof Date && !Number.isNaN(f.timestamp.getTime())
  );
}
