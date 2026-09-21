import { UnauthorizedError } from './profileApi';
import { problemDetailFrom } from './selectionsApi';
import { invalidateCache } from './apiCache';

// Same base URL rules as selectionsApi (Vite proxy in dev, YOUR_API_URL in prod).
const apiBaseUrl = import.meta.env.YOUR_API_URL ?? '';

// UC5 (standalone): replaces the user's pending service selection when no city could be
// chosen (e.g. the City API is down). The backend stores it until cities are added later.
export async function saveServices(jwt: string, serviceIds: number[]): Promise<void> {
  const response = await fetch(`${apiBaseUrl}/api/UserServices`, {
    method: 'PUT',
    headers: {
      Authorization: `Bearer ${jwt}`,
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({ serviceIds }),
  });

  if (response.status === 401) {
    throw new UnauthorizedError();
  }
  if (!response.ok) {
    throw new Error(await problemDetailFrom(response, 'Failed to save services'));
  }

  // The saved pending services change what GET /api/Selections reports.
  invalidateCache('selections', 'forecasts');
}
