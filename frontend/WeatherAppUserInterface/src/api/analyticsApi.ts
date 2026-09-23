import { UnauthorizedError } from './profileApi';
import { problemDetailFrom } from './selectionsApi';

// Same base URL rules as selectionsApi (Vite proxy in dev, YOUR_API_URL in prod).
const apiBaseUrl = import.meta.env.YOUR_API_URL ?? '';

// UC8 (Request Analytics): asynchronous - this only acknowledges the queued request. The
// finished report (temperature/humidity/wind stats + danger-day count per city) is emailed
// to the user later; there is no synchronous preview or status polling.
export async function requestAnalytics(
  jwt: string,
  cityIds: number[],
  serviceIds: number[],
  dateRangeStart: string,
  dateRangeEnd: string,
): Promise<string> {
  const response = await fetch(`${apiBaseUrl}/api/Analytics`, {
    method: 'POST',
    headers: {
      Authorization: `Bearer ${jwt}`,
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({ cityIds, serviceIds, dateRangeStart, dateRangeEnd }),
  });

  if (response.status === 401) {
    throw new UnauthorizedError();
  }
  if (!response.ok) {
    throw new Error(await problemDetailFrom(response, 'Failed to request analytics'));
  }

  const result = (await response.json()) as { batchId: string };
  return result.batchId;
}
