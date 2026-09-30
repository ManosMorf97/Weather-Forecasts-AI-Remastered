# Weather Forecasts AI — Remastered

A weather-aggregation platform that lets users track multiple cities across several partner forecasting APIs, surfaces the best-rated forecast per city, and proactively warns subscribers about life-threatening conditions. Also users can rate each forecasting partner's accuracy.Built as two independently
deployable backend services sharing one SQL Server database, plus a React frontend — all running
on Kubernetes (DigitalOcean) behind HTTPS.

**Live:** https://weatherappmorf.duckdns.org

[![CI/CD](https://github.com/ManosMorf97/Weather-Forecasts-AI-Remastered/actions/workflows/ci-cd-pipeline.yml/badge.svg)](https://github.com/ManosMorf97/Weather-Forecasts-AI-Remastered/actions/workflows/ci-cd-pipeline.yml)
![.NET 10](https://img.shields.io/badge/.NET-10-512BD4)
![TypeScript](https://img.shields.io/badge/TypeScript-5-3178C6)
![React](https://img.shields.io/badge/React-19-61DAFB)
![SQL Server](https://img.shields.io/badge/SQL_Server-EF_Core%20%2B%20Prisma-CC2927)
![Appwrite](https://img.shields.io/badge/Auth-Appwrite-FD366E)
![Kubernetes](https://img.shields.io/badge/Kubernetes-DigitalOcean-326CE5)
![Built with Claude Code](https://img.shields.io/badge/Built_with-Claude_Code-D97757)

---

## What it does

- **Aggregates weather from 4 independent providers** (Open-Meteo, OpenWeatherMap, WeatherAPI,
  Visual Crossing) behind one common interface, normalized to the same units (°C, %, km/h, UTC).
- **Surfaces the best forecast per city** — the aggregated view picks the partner service with
  the highest user-rated accuracy for that city, not just the newest data.
- **Warns users proactively** — a scheduled job polls every subscribed (city, service) pair,
  detects life-threatening forecasts (CAP severity alerts), and emails each affected user exactly
  once per distinct danger record, even for hazards that existed before they subscribed.
- **Generates analytics reports asynchronously** — a background worker turns a user's request
  into per-city temperature/humidity/wind statistics and charts, rendered to a PDF and emailed,
  without blocking the HTTP request that triggered it.
- **Delegates authentication entirely to Appwrite** — the backend never issues or stores
  passwords; it verifies each caller's JWT server-side, live, on every request.

## Architecture

Two backend services, deliberately **not** calling each other — the only thing they share is the
SQL Server schema (owned by `WeatherUserActions`' EF Core migrations) — plus a frontend that talks
to `WeatherUserActions` over HTTP.

![System design](requirements/system_design/system_design.png)

| Service | Role | Stack |
|---|---|---|
| [`WeatherUserActions`](backend/WeatherUserActions) | User-facing REST API: profiles, city/service selection, forecast search, ratings, aggregated forecasts, analytics reports | ASP.NET Core (.NET 10), EF Core, MailKit, QuestPDF, ScottPlot |
| [`PredictionUpdater`](backend/PredictionUpdater) | Run-once scheduler job (UC11): one process start = one poll → store → notify cycle, then exit; an external scheduler owns the interval | Node.js + TypeScript, Prisma, Zod |
| [`WeatherAppUserInterface`](frontend/WeatherAppUserInterface) | React frontend: auth screens, profile setup, dashboard (forecast search + ratings), aggregated forecasts, analytics requests | React 19, TypeScript, Vite, Bootstrap |

All three connect to the same **Appwrite** project for authentication — the frontend talks to
Appwrite directly for sign-up/login/token refresh (UC1, UC13, UC14), and `WeatherUserActions`'s
only auth job is to verify the bearer token it receives by calling Appwrite's own `account.get()`,
rather than trusting a client-decoded JWT.

## Use cases

<details>
<summary><strong>14 use cases</strong>, each documented in <a href="requirements/usecase_analysis">requirements/usecase_analysis</a> with actors, flows, alternative paths and postconditions</summary>

| # | Use case |
|---|---|
| UC1 | Login / Sign Up |
| UC2 | Create Profile |
| UC3 | Edit Selections |
| UC4 | Select Cities |
| UC5 | Select Forecasting Services |
| UC6 | Search Forecast |
| UC7 | Rate Forecasting Service |
| UC8 | Request Analytics (async, emailed as PDF) |
| UC9 | Download Analytics *(planned: on-demand, JSON/CSV/PDF)* |
| UC10 | View Aggregated Forecast (best-rated service per city) |
| UC11 | Poll Forecasts & Notify Users (the scheduler job) |
| UC12 | Calculate Aggregates |
| UC13 | Change Authentication Details |
| UC14 | Logout |

</details>

## Engineering highlights

- **Tests hit a real database, not mocks.** Both services use Testcontainers to spin up an actual
  SQL Server instance for repository/integration tests — including asserting on the exact rows
  written, not just that a call "succeeded."
- **Provider adapters are honest about their own limits.** Each weather API adapter is a pure
  `response → NormalizedForecast[]` function behind a shared interface, independently unit
  tested with realistic fake payloads. One provider only offers 3-hourly data, so it simply
  emits no `HOURLY` row rather than fabricating one — a documented, deliberate trade-off, not
  an oversight.
- **Danger detection is CAP-alert-driven**, not a heuristic — providers that expose alerts (CAP
  `severity` + effective/expiry window) drive the `danger` flag; providers with no alert feed
  honestly report `danger: false` instead of guessing.
- **Async work doesn't block requests.** Analytics report generation (DB query → statistics →
  charts → PDF → email) can take tens of seconds, so the controller returns `202 Accepted`
  immediately and a background worker (`AnalyticsReportWorker`) drives the job to completion.
- **~320 automated tests** across all three parts (~150 xUnit tests in `WeatherUserActions.Tests`,
  ~35 Vitest tests in `PredictionUpdater`, ~135 Vitest + React Testing Library tests in
  `WeatherAppUserInterface`), run in CI on every push/PR.
- **CI/CD only builds and deploys what changed** — a path-filtered GitHub Actions pipeline
  ([`ci-cd-pipeline.yml`](.github/workflows/ci-cd-pipeline.yml)) runs each part's
  lint/build/test job only when files under that part changed, and on `main` deploys only that
  part to Kubernetes.

## Data model

![ER diagram](requirements/ER_Diagram/ERDiagram.png)

## Repository layout

```
backend/
  WeatherUserActions/     ASP.NET Core API — profiles, forecasts, ratings, analytics, aggregation
    WeatherUserActions/         Controllers, Services, Repositories, EF Core Models & Migrations
    WeatherUserActions.Tests/   xUnit: controller-logic, repository (real DB), full HTTP journeys
  PredictionUpdater/      Node/TS scheduler job — poll, store, notify (see its own README for detail)
    src/providers/              One adapter per partner weather API, normalized to one shape
    src/pipeline/                Poll → store → notify, orchestrating the repositories
    test/                       Vitest: provider unit tests + Testcontainers repository tests
frontend/
  WeatherAppUserInterface/  React + Vite frontend — auth screens, routing guards, profile sync
    src/auth/                   Auth context/hooks/components, plus their Vitest + RTL tests
Deployment/                Kubernetes manifests — one folder per part, plus mssql and ingress
requirements/              Use-case analysis, ER diagram, sequence/activity diagrams, system design
.github/workflows/         CI/CD
```

## Deployment

Everything runs on **DigitalOcean Kubernetes** in the `weather` namespace, deployed by the
[CI/CD pipeline](.github/workflows/ci-cd-pipeline.yml) on every push to `main`.

```
Browser ──HTTPS──▶ DigitalOcean Load Balancer ──▶ ingress-nginx
                                                     ├─ /api/* ──▶ WeatherUserActions ─┐
                                                     └─ /*     ──▶ Frontend             ├──▶ SQL Server
                   PredictionUpdater (CronJob, 00:00/08:00/16:00 UTC) ─────────────────┘   (internal only)
```

| Part | Kubernetes resource | Notes |
|---|---|---|
| SQL Server | StatefulSet + PVC + ClusterIP Service | Never exposed publicly |
| `WeatherUserActions` | Deployment + ClusterIP Service | EF Core migrations run as an init container (`efbundle`) before the API starts |
| `WeatherAppUserInterface` | Deployment + ClusterIP Service | |
| `PredictionUpdater` | CronJob | Run-once job; `concurrencyPolicy: Forbid`, retried at most twice |
| Ingress | ingress-nginx + cert-manager | One public address; frontend and API share an origin, so no CORS. Free Let's Encrypt certificate, renewed automatically |

- **Images** are pushed to a private Docker Hub repository, tagged `<part>-<commit sha>`.
- **Secrets** (DB password, Appwrite, SMTP, API keys) come from GitHub secrets and are created
  in the cluster by the pipeline — none are committed.
- **Domain:** a free [DuckDNS](https://www.duckdns.org) subdomain pointing at the load balancer
  IP, also registered as a Web app in Appwrite.

See [`Deployment/`](Deployment) for the manifests; each one explains its choices in comments.

## Getting started

Each part is self-contained and has its own setup instructions:

- [`backend/WeatherUserActions/README.md`](backend/WeatherUserActions/README.md) *(ASP.NET Core API)*
- [`backend/PredictionUpdater/README.md`](backend/PredictionUpdater/README.md) *(scheduler job)*
- [`frontend/WeatherAppUserInterface`](frontend/WeatherAppUserInterface) *(React + Vite app)* —
  `npm install && npm run dev` (or `npm test` for the Vitest + React Testing Library suite)

All three expect the same machine-level environment variables for the shared SQL Server connection
and Appwrite project (`YOUR_SERVER` / `YOUR_USER` / `YOUR_PASSWORD`, `YOUR_APPWRITE_*`) — see either
backend README for the full list. Two more matter specifically for local frontend/backend wiring:

- `YOUR_API_URL` — leave **unset** for local dev; the frontend then calls the backend through
  Vite's dev proxy (`vite.config.ts`), which needs no CORS. Set it only when the frontend and
  backend are genuinely separate origins (production, or calling the backend directly).
- `YOUR_FRONTEND_URL` — the frontend origin `WeatherUserActions` allows via CORS; only needed when
  `YOUR_API_URL` is set (i.e. the proxy isn't in the picture).

## Documentation

Full requirements analysis, per-use-case specs, sequence/activity diagrams and the system design
source live in [`requirements/`](requirements).

## Built with Claude Code

This project was developed with the help of [Claude Code](https://claude.com/claude-code),
Anthropic's AI coding assistant. I used it as a pair programmer across the whole project:
turning the use cases in [`requirements/`](requirements) into code, writing and reviewing tests,
explaining concepts along the way (dependency injection, testing against a real database,
Kubernetes networking), and debugging the CI/CD pipeline and the Kubernetes deployment.
Project-specific guidance for it lives in [`CLAUDE.md`](CLAUDE.md).

Design decisions, reviews and every commit are my own.
