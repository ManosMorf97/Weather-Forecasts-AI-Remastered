# PredictionUpdater

UC11 scheduler. A **run-once job**: one process start = one poll → store → notify cycle, then
exit. An external scheduler (cron / k8s CronJob / cloud scheduler) decides the interval.

Separate from `WeatherUserActions`. **They never call each other** — the only shared thing is
the SQL Server database, whose schema is owned by `WeatherUserActions`' EF Core migrations.

## What one cycle does

| UC11 | Code |
|---|---|
| 2–3 read distinct (city, service) from `UserCitySite` | `repositories/selectionsRepository.ts` |
| 4–6 fetch each provider, validate | `pipeline/poll.ts` + `providers/*` |
| 7 (A4/A5) insert new / update changed / skip duplicate | `pipeline/store.ts` + `forecastsRepository.ts` |
| 8–10 danger rows → subscribers → drop already-notified → group per user | `pipeline/notify.ts` + `notificationsRepository.ts` |
| 11 batch email lookup (ids chunked ≤ 100) | `notifications/appwriteUsers.ts` |
| 12–13 send one email per user, log a `Notification` per forecast | `notifications/emailSender.ts` |
| 14–15 log summary, exit (1 if every service failed) | `pipeline/run.ts` → `main.ts` |

## Providers (4)

`providers/registry.ts` keys adapters by `ForecastingServices.Name`. Those rows must exist with
**exactly** these names:

| `Name` | Adapter | Key | Danger |
|---|---|---|---|
| `Open-Meteo` | `openMeteo.ts` ✅ done | none | never flags |
| `OpenWeatherMap` | `openWeatherMap.ts` — TODO | `OPENWEATHERMAP_API_KEY` | never flags |
| `WeatherAPI` | `weatherApi.ts` — TODO | `WEATHERAPI_API_KEY` | Extreme/Severe CAP alert |
| `Visual Crossing` | `visualCrossing.ts` — TODO | `VISUALCROSSING_API_KEY` | Extreme/Severe CAP alert |

A service with no adapter registered (missing key) is logged and skipped.

All adapters normalise to: **°C, %, km/h, UTC instants**. DAILY = next 3 local days at
08:00 / 15:00 / 21:00 local (resolved from each provider's own offset).

## Setup

```bash
npm install

# These machine env vars must be set (same ones WeatherUserActions uses):
#   YOUR_SERVER  YOUR_USER  YOUR_PASSWORD          -> DB connection (db: weather_forecasts_ai)
#   YOUR_APPWRITE_ENDPOINT / _PROJECT_ID / _API_KEY -> danger-notification email lookup
cp .env.example .env            # this service's own API keys / SMTP - all optional

# Generate the Prisma client into src/generated/prisma (also runs on postinstall).
# schema.prisma is the introspected mirror of the EF schema - re-pull when the DB changes:
npm run db:pull                 # prisma db pull + generate  (keeps the @map names)
# or, offline:
npm run db:generate

npm test                        # provider tests need no DB; the repo tests use Testcontainers
npm run dev                     # one cycle against the assembled connection
```

`prisma.config.ts` (Prisma 7) resolves the CLI's connection URL: `DATABASE_URL` if set,
otherwise assembled from `YOUR_SERVER` / `YOUR_USER` / `YOUR_PASSWORD` / `DB_NAME`. The runtime
client does not use a URL - it connects through `@prisma/adapter-mssql` with the same parts
(see `src/db/client.ts`).

## Deployment

- Build: `npm run build` → `npm start` (`node dist/main.js`).
- Schedule the process however you like; there is no internal timer.
- Exit code `1` = every attempted service failed (E1) — wire the scheduler to alert on it.
- Give it a **dedicated SQL login**: `SELECT` on `Users`, `Cities`, `ForecastingServices`,
  `CitySites`, `UserCitySites`, `UserServices`; `SELECT/INSERT/UPDATE` on `Forecasts` and
  `Notifications`. (Tests use the Testcontainers admin login, not this one.)

## When the .NET schema changes

Re-run `npm run db:pull` and `npm test` before deploying. A migration that renames or drops a
column this job uses (`Forecasts`, `Notifications`, `CitySites`, …) will otherwise fail at runtime.

## Still TODO

- `openWeatherMap.ts`, `weatherApi.ts`, `visualCrossing.ts` adapters (notes in each file).
- CAP alert → row mapping helper for the two alert providers.
- DB-backed pipeline tests (`store`, `notify`) via `@testcontainers/mssqlserver`.
