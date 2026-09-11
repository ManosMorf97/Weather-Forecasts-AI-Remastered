# WeatherUserActions

The user-facing REST API: profiles, city/service selection, forecast search, ratings, the
aggregated "best-rated forecast per city" view, and analytics report requests.

Separate from `PredictionUpdater`. **They never call each other** — the only shared thing is the
SQL Server database, whose schema this service owns via EF Core migrations.

## Controllers

| Route | Controller | Covers |
|---|---|---|
| `api/Profile` | `ProfileController` | Create/view profile (UC2) |
| `api/Selections` | `SelectionsController` | Edit city + service selections (UC3, UC4, UC5) |
| `api/Forecasts` | `ForecastsController` | Search current/hourly/daily forecasts by city (UC6) |
| `api/UserServices` | `UserServicesController` | Rate a forecasting service (UC7) |
| `api/Analytics` | `AnalyticsController` | Queue an analytics report; returns `202 Accepted` (UC8) |
| — | `AggregatedForecastsService` (used by `ForecastsController`) | Best-rated-service-per-city view (UC10, UC12) |

Authentication is a bearer token verified against **Appwrite** on every request
(`AppwriteAuthService`) — the API never issues, stores, or validates passwords itself; the
frontend talks to Appwrite directly for sign-up/login/token refresh (UC1, UC13, UC14).

## Analytics: request → background worker → emailed PDF

`AnalyticsController` returns fast (`202 Accepted`) after validating the request and writing a
`Queued` row; `AnalyticsReportWorker` (an `IHostedService`) polls for queued reports, computes
per-city statistics, renders charts (ScottPlot) into a PDF (QuestPDF), and emails it (MailKit) —
so a request that takes tens of seconds never ties up a web request thread. See
[`docs/background-and-report-generation.md`](docs/background-and-report-generation.md) and
[`docs/email-sending.md`](docs/email-sending.md) for the full walkthrough.

## Setup

```bash
# Machine env vars (shared with PredictionUpdater):
#   YOUR_SERVER  YOUR_USER  YOUR_PASSWORD           -> DB connection (db: weather_forecasts_AI)
#   YOUR_APPWRITE_ENDPOINT / _PROJECT_ID / _API_KEY  -> caller JWT verification + user email lookup

cp WeatherUserActions/secrets.example.json WeatherUserActions/secrets.json
# fill in SMTP creds for real email delivery - without them, emails are just logged (LoggingEmailSender)

dotnet restore WeatherUserActions.slnx
dotnet ef database update --project WeatherUserActions   # applies the 7 migrations under Migrations/
dotnet build WeatherUserActions.slnx

dotnet test WeatherUserActions.slnx    # 150+ xUnit tests: controller-logic, repository (real DB via
                                        # Testcontainers), and full HTTP-journey integration tests
dotnet run --project WeatherUserActions
```

`appsettings.Development.json` holds the `Database` / `Appwrite` / `Email` / `Analytics` config
shape; any `YOUR_*` environment variable overrides the matching setting when present, so local
dev can use either file-based config or the shared machine env vars.

## When you change the schema

Add an EF Core migration (`dotnet ef migrations add <Name> --project WeatherUserActions`) and
apply it. `PredictionUpdater` mirrors this schema via Prisma introspection (`npm run db:pull`) —
re-run that on its side after a migration that touches a table it reads (`Forecasts`,
`Notifications`, `CitySites`, …), and see its own README for details.
