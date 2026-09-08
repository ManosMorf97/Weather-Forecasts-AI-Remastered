import { z } from 'zod';
import type { SqlServerParts } from './db/connectionUrl.js';

// All runtime configuration comes from the environment. The YOUR_* names are ambient machine
// env vars shared with WeatherUserActions (its Program.cs reads the same YOUR_SERVER / YOUR_USER
// / YOUR_PASSWORD / YOUR_APPWRITE_*). Everything else lives in .env for local runs.
const schema = z.object({
  // Database connection parts (same convention as the .NET side). Assembled into a Prisma URL below.
  YOUR_SERVER: z.string().min(1),
  YOUR_USER: z.string().min(1),
  YOUR_PASSWORD: z.string().min(1),
  DB_NAME: z.string().min(1).default('weather_forecasts_ai'),

  // Open-Meteo needs no key. A provider whose key is absent is left out of the registry.
  OPENWEATHERMAP_API_KEY: z.string().min(1).optional(),
  WEATHERAPI_API_KEY: z.string().min(1).optional(),
  VISUALCROSSING_API_KEY: z.string().min(1).optional(),

  DANGER_CAP_SEVERITIES: z.string().default('Extreme,Severe'),

  YOUR_APPWRITE_ENDPOINT: z.string().min(1).optional(),
  YOUR_APPWRITE_PROJECT_ID: z.string().min(1).optional(),
  YOUR_APPWRITE_API_KEY: z.string().min(1).optional(),
  APPWRITE_ID_CHUNK_SIZE: z.coerce.number().int().positive().max(100).default(100),

  SMTP_HOST: z.string().min(1).optional(),
  SMTP_PORT: z.coerce.number().int().positive().default(587),
  SMTP_USER: z.string().min(1).optional(),
  SMTP_PASSWORD: z.string().min(1).optional(),
  EMAIL_FROM: z.string().default('Weather Alerts <noreply@forecastsUpdater.local>'),

  HTTP_TIMEOUT_MS: z.coerce.number().int().positive().default(15_000),
  LOG_LEVEL: z.string().default('info'),
});

export type Config = z.infer<typeof schema> & {
  /** DB connection parts from YOUR_SERVER / YOUR_USER / YOUR_PASSWORD / DB_NAME. */
  readonly sqlServer: SqlServerParts;
  /** CAP severities normalised to a lower-case set for matching. */
  readonly dangerSeverities: ReadonlySet<string>;
};

let cached: Config | undefined;

export function loadConfig(env: NodeJS.ProcessEnv = process.env): Config {
  if (cached) return cached;

  const parsed = schema.parse(env);
  cached = {
    ...parsed,
    sqlServer: {
      server: parsed.YOUR_SERVER,
      user: parsed.YOUR_USER,
      password: parsed.YOUR_PASSWORD,
      database: parsed.DB_NAME,
    },
    dangerSeverities: new Set(
      parsed.DANGER_CAP_SEVERITIES.split(',')
        .map((s) => s.trim().toLowerCase())
        .filter(Boolean),
    ),
  };
  return cached;
}

// Test helper: forget the memoised config so the next loadConfig() re-reads the environment.
export function resetConfigCache(): void {
  cached = undefined;
}
