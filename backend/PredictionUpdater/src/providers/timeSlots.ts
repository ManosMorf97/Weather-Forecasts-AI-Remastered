// FR2 forecast shape, shared by every adapter:
//   CURRENT - one reading "now"
//   HOURLY  - the next 3 hourly readings
//   DAILY   - the next 3 local days, at 08:00 / 15:00 / 21:00 local time

export const HOURLY_COUNT = 3;
export const DAILY_DAYS = 3;
export const DAILY_SLOT_HOURS = [8, 15, 21] as const;

export interface LocalWallTime {
  year: number;
  month: number; // 1-12
  day: number;
  hour: number;
  minute?: number;
}

// Convert a local wall-clock time to the UTC instant it refers to, given the location's
// offset from UTC in minutes (e.g. Athens in summer = +180).
export function localWallTimeToUtc(local: LocalWallTime, offsetMinutes: number): Date {
  const asIfUtc = Date.UTC(local.year, local.month - 1, local.day, local.hour, local.minute ?? 0);
  return new Date(asIfUtc - offsetMinutes * 60_000);
}

// Parse an offset-less ISO local timestamp ("2026-09-04T15:00" or "...T15:00:00") into parts.
export function parseIsoLocal(isoLocal: string): Required<LocalWallTime> {
  const [date = '', time = '00:00'] = isoLocal.split('T');
  const [year, month, day] = date.split('-').map(Number) as [number, number, number];
  const [hour, minute = 0] = time.split(':').map(Number) as [number, number];
  return { year, month, day, hour, minute };
}

// The set of local calendar dates we want DAILY rows for: the next DAILY_DAYS days after
// `todayLocal`. Keys are "YYYY-M-D" (no zero padding) for comparison against parsed parts.
export function wantedDailyDateKeys(todayLocal: LocalWallTime): Set<string> {
  const startUtcDay = Date.UTC(todayLocal.year, todayLocal.month - 1, todayLocal.day);
  const keys = new Set<string>();
  for (let i = 1; i <= DAILY_DAYS; i++) {
    const d = new Date(startUtcDay + i * 86_400_000);
    keys.add(`${d.getUTCFullYear()}-${d.getUTCMonth() + 1}-${d.getUTCDate()}`);
  }
  return keys;
}

export function localDateKey(local: LocalWallTime): string {
  return `${local.year}-${local.month}-${local.day}`;
}
