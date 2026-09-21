import { UnauthorizedError } from './profileApi';

export interface CityDto {
  name: string;
  country: string;
  latitude: number;
  longitude: number;
}

export interface ServiceSelectionDto {
  serviceId: number;
  name: string;
  selected: boolean;
}

export interface Selections {
  services: ServiceSelectionDto[];
  cities: CityDto[];
}

// Empty in dev, where the Vite proxy forwards /api to WeatherUserActions (see vite.config.ts).
// In prod, frontend and backend are separate services, so YOUR_API_URL must point at the
// backend's origin.
const apiBaseUrl = import.meta.env.YOUR_API_URL ?? '';

// UC3: current city/service selections - every forecasting service flagged selected/not,
// plus the user's currently selected cities.
export async function getSelections(jwt: string): Promise<Selections> {
  const response = await fetch(`${apiBaseUrl}/api/Selections`, {
    headers: { Authorization: `Bearer ${jwt}` },
  });

  if (response.status === 401) {
    throw new UnauthorizedError();
  }
  if (!response.ok) {
    throw new Error(`Failed to load selections (status ${response.status})`);
  }

  return (await response.json()) as Selections;
}

// UC3/UC4/UC5: replaces the user's full city/service selection with exactly this set.
export async function saveSelections(
  jwt: string,
  cities: CityDto[],
  serviceIds: number[],
): Promise<void> {
  const response = await fetch(`${apiBaseUrl}/api/Selections`, {
    method: 'PUT',
    headers: {
      Authorization: `Bearer ${jwt}`,
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({ cities, serviceIds }),
  });

  if (response.status === 401) {
    throw new UnauthorizedError();
  }
  if (!response.ok) {
    throw new Error(await problemDetailFrom(response));
  }
}

export async function problemDetailFrom(
  response: Response,
  fallback = 'Failed to save selections',
): Promise<string> {
  try {
    const problem = (await response.json()) as { detail?: string };
    if (problem.detail) {
      return problem.detail;
    }
  } catch {
    // Body wasn't JSON - fall through to the generic message below.
  }
  return `${fallback} (status ${response.status})`;
}
