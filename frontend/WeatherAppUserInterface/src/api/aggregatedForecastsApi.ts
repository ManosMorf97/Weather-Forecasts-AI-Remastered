import { UnauthorizedError } from './profileApi';
import { readCache, writeCache } from './apiCache';

export interface AggregatedForecastItemDto {
  forecastId: number;
  city: string;
  country: string;
  service: string;
  type: 'CURRENT' | 'HOURLY' | 'DAILY';
  timestamp: string;
  offsetMinutes: number;
  temperature: number;
  humidity: number;
  windSpeed: number;
  dangerFlag: boolean;
}

// Explains why "service" won the aggregation for this city (UC12 steps 3-6).
export interface ServiceAggregationMetadataDto {
  city: string;
  service: string;
  averageRating: number | null;
  ratingCount: number;
  aggregationApplicable: boolean;
  isTie: boolean;
  isUnratedSelection: boolean | null;
}

export interface AggregatedForecasts {
  forecasts: AggregatedForecastItemDto[];
  serviceMetadata: ServiceAggregationMetadataDto[];
}

// Empty in dev, where the Vite proxy forwards /api to WeatherUserActions (see vite.config.ts).
// In prod, frontend and backend are separate services, so YOUR_API_URL must point at the
// backend's origin.
const apiBaseUrl = import.meta.env.YOUR_API_URL ?? '';

// UC10 main flow: for every selected city with data, the current/upcoming forecast from the
// selected service with the highest all-time average rating (UC12), plus per-city metadata
// explaining that choice.
export async function getAggregatedForecasts(jwt: string): Promise<AggregatedForecasts> {
  const cached = readCache<AggregatedForecasts>('aggregatedForecasts');
  if (cached) {
    return cached;
  }

  const response = await fetch(`${apiBaseUrl}/api/Forecasts/aggregated`, {
    headers: { Authorization: `Bearer ${jwt}` },
  });

  if (response.status === 401) {
    throw new UnauthorizedError();
  }
  if (!response.ok) {
    throw new Error(`Failed to load aggregated forecasts (status ${response.status})`);
  }

  const result = (await response.json()) as AggregatedForecasts;
  writeCache('aggregatedForecasts', result);
  return result;
}
