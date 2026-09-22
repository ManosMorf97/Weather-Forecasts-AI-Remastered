// Formats a UTC timestamp as that city's own local wall-clock time (via its offsetMinutes),
// not the browser's local timezone. Shared by DashboardPage (UC6) and AggregatedForecastsPage (UC10).
export function formatLocalTime(timestamp: string, offsetMinutes: number): string {
  const local = new Date(new Date(timestamp).getTime() + offsetMinutes * 60_000);
  const date = local.toLocaleDateString(undefined, { timeZone: 'UTC', month: 'short', day: 'numeric' });
  const time = local.toLocaleTimeString(undefined, { timeZone: 'UTC', hour: '2-digit', minute: '2-digit' });
  return `${date}, ${time}`;
}
