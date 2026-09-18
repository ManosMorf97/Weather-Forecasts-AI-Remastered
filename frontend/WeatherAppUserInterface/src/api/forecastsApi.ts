import { UnauthorizedError } from './profileApi';

export interface ForecastItemDto {
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
  userRating: number | null;
}

interface GetForecastsResponse {
  forecasts: ForecastItemDto[];
}

// Empty in dev, where the Vite proxy forwards /api to WeatherUserActions (see vite.config.ts).
// In prod, frontend and backend are separate services, so YOUR_API_URL must point at the
// backend's origin.
const apiBaseUrl = import.meta.env.YOUR_API_URL ?? '';

// UC6 main flow: every current/upcoming forecast for the user's saved cities and selected
// services, ordered by Service name, City name, then Timestamp.
export async function getForecasts(jwt: string): Promise<ForecastItemDto[]> {
  const response = await fetch(`${apiBaseUrl}/api/Forecasts`, {
    headers: { Authorization: `Bearer ${jwt}` },
  });

  if (response.status === 401) {
    throw new UnauthorizedError();
  }
  if (!response.ok) {
    throw new Error(`Failed to load forecasts (status ${response.status})`);
  }

  const body = (await response.json()) as GetForecastsResponse;
  return body.forecasts;
}

// UC7 main flow / A1: creates or updates the user's rating for this forecast.
export async function rateForecast(jwt: string, forecastId: number, value: number): Promise<void> {
  const response = await fetch(`${apiBaseUrl}/api/Forecasts/${forecastId}/rating`, {
    method: 'PUT',
    headers: {
      Authorization: `Bearer ${jwt}`,
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({ value }),
  });

  if (response.status === 401) {
    throw new UnauthorizedError();
  }
  if (!response.ok) {
    throw new Error(await problemDetailFrom(response, 'save'));
  }
}

// UC7 A2: removes the user's rating for this forecast, if any (idempotent).
export async function removeRating(jwt: string, forecastId: number): Promise<void> {
  const response = await fetch(`${apiBaseUrl}/api/Forecasts/${forecastId}/rating`, {
    method: 'DELETE',
    headers: { Authorization: `Bearer ${jwt}` },
  });

  if (response.status === 401) {
    throw new UnauthorizedError();
  }
  if (!response.ok) {
    throw new Error(await problemDetailFrom(response, 'remove'));
  }
}

async function problemDetailFrom(response: Response, action: 'save' | 'remove'): Promise<string> {
  try {
    const problem = (await response.json()) as { detail?: string };
    if (problem.detail) {
      return problem.detail;
    }
  } catch {
    // Body wasn't JSON - fall through to the generic message below.
  }
  return `Failed to ${action} rating (status ${response.status})`;
}
