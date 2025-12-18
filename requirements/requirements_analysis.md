# Weather Forecasts — Requirements Analysis

## Overview
This document captures functional and non-functional requirements, actors, use cases, acceptance criteria and a brief API sketch for the Weather Forecasts service described in the project spec.

## Actors
- End User: registers, creates profile, selects services and cities, searches forecasts, rates forecasts, downloads analytics, receives warnings.
- Scheduler: ingests and stores forecasts periodically.

## Functional Requirements
1. User Registration & Profile
   - Users can register and manage a profile containing Cities of interest and selected forecasting services.
2. Forecast Data Types
   - Current weather
   - Hourly forecast for the next 3 hours
   - Daily forecast for the next 3 days with 3 forecasts per day (08:00, 15:00, 21:00)
3. Search and View
   - Users can search forecasts by City and view current, hourly and daily forecasts.
4. Service Selection
   - When creating/editing profile, users choose which partner forecasting services and Cities to include.
5. Ratings
   - Users can rate predictions on scale 1–5 per forecast item; ratings are stored and aggregated per service and City.
6. Aggregation & Analytics
   - System can return: (a) analytics containing forecasts, (b) aggregated forecasts based on service ratings.
   - For each city, user receives the forecast from the service with the maximum average rating for that city.
7. Download
   - Users can download analytics or aggregates (download = receive system response containing multiple forecasts/aggregate data in a structured format, e.g., JSON/CSV/PDF).
8. Alerts/Warnings
   - Scheduler sends warning messages for life-threatening weather to users subscribed to affected areas.

## Non-Functional Requirements
- Scalability: support many regions and multiple partner services; ingestion must scale horizontally.
- Performance: search responses should be returned within acceptable latency (e.g., < 300ms for cached results).
- Availability: high availability for critical features (search, alerts delivery).
- Consistency: ensure ratings-driven aggregates are eventually consistent after updates.
- Security & Privacy: protect user data, authenticate partner pushes (API keys, mutual TLS, or signed requests), rate-limit APIs.
- Data retention: define retention policy for forecasts and ratings (e.g., keep raw forecasts for 30 days, aggregates longer).
- Localisation: support timezones and localization for area names and times.

## Use Cases (brief)
- Login
- Create / Edit Profile (Cities + selected services)
- Search Forecast by Area
- View Current / Hourly / Daily Forecast
- Rate Forecast
- Request / Download Analytics or Aggregates
- Receive Warning Notifications
- System: Scheduled Polling and Storage

## Acceptance Criteria (examples)
- Users can receive forecasts from at least one partner services for any City.
- Downloaded analytics when requested.
- User ratings update the per-service score used in aggregated selection.
- Alerts are sent within a defined SLA after a qualifying hazard is detected.

## API Sketch (partner-facing)
- POST /api/v1/forecasts/push
  - Auth: API key / signed request
  - Payload: { serviceId, CityId, timestamp, forecasts: [{type: CURRENT|HOURLY|DAILY, time, values...}] }
  - Response: 200 OK

- GET /api/v1/forecasts?region={regionId}&type={current|hourly|daily}
  - Returns cached latest forecasts and metadata about contributing services.

## Notes & Open Questions
- How to define Cities (cityIDs, admin units)?
- Delivery channel for warnings (email, SMS, push notification) — who is responsible for sending?
- Exact aggregate algorithm (weighted by rating, recency, or hybrid) — propose configurable strategy.

---
Generated from project spec in Weather-Forecasts.md.
