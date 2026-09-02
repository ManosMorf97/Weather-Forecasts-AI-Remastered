# UC8: Request Analytics

**ID:** UC8
**Name:** Request Analytics
**Actor:** End User
**Description:** User requests an analytics report of forecast statistics for a chosen
subset of their own selected cities and services over a date range. Generation and
delivery are **asynchronous**: the request is queued immediately, a background worker
computes the statistics, and the finished report is **emailed to the user as a PDF**.

**Preconditions:**
- User is logged in (valid Appwrite JWT)
- User has at least one city + service selection (UC4 / UC5)
- Historical forecast data exists

**Main Flow:**
1. User selects "Request Analytics"
2. Frontend displays the request form
3. User specifies **cities**, **services** (each a subset of their own selections) and a
   **date range**. Metrics are fixed and always computed: temperature, humidity and
   wind speed (average, standard deviation, min, max), plus danger-day count and
   sample count per city.
4. User submits the request (`POST /api/analytics`, Bearer JWT)
5. System verifies the JWT and validates the parameters (see A1, A2)
6. System creates **one queued report per requested service**, all sharing a single
   `batch_id`, and immediately responds `202 Accepted` with the `batch_id`
7. Later, the **Analytics Report Worker** (polls roughly every 5 s) picks up each queued
   report and computes its per-city statistics from stored forecasts
8. Once **every** report in the batch has finished generating, the worker fetches the
   user's email from the Authentication Service, renders a PDF (bar charts with
   standard-deviation error bars + a numbers table), emails it, and marks the batch
   delivered. Delivery is retried on later cycles until it succeeds.

**Alternative Flows:**
- **A1: Invalid parameters**
  - At step 5, if the city list is empty, the service list is empty, the start date is
    after the end date, or the range exceeds **366 days**, the system returns `400`
    with the specific reason. User returns to step 3.
- **A2: City or service not in the user's selection**
  - At step 5, if any requested city or service is not part of the user's own
    selections, the system returns `400` (`InvalidCities` / `InvalidServices`).
    User returns to step 3.
- **A3: Some services cannot be processed**
  - During generation, a report whose forecast query returns nothing or fails is
    marked `Failed`. The batch email is still sent, covering the services that
    succeeded and naming the ones that could not be processed.
- **A4: No services succeeded**
  - If every report in the batch failed, the worker emails a plain "report could not
    be generated" message with no attachment.
- **A5: PDF rendering fails**
  - The numbers were computed but the PDF could not be built: the worker sends a
    **text-only email** containing the statistics instead of the PDF.

**Postconditions:**
- One `AnalyticsReport` row per requested service (each with per-city
  `AnalyticsReportCityMetric` rows), status `Completed` or `Failed`
- The batch has been emailed to the user (`delivered_at` set)

**Exceptions:**
- **E1:** JWT verification fails - `401`, nothing is queued
- **E2:** Database unavailable while queuing - `500`, nothing is queued; user may retry
- **E3:** Email lookup or mail transport fails - the batch is left undelivered and
  retried on the next worker cycle

**Notes:**
- There is **no synchronous preview** and no "generation timeout" path - asynchronous
  delivery is the design, not a fallback.
- Analytics does **not** use aggregation (UC12 is not involved); statistics are computed
  directly over stored forecasts for the user's chosen services.
- Delivery format is always **PDF** (plus the plain-text email body). The stored
  `format` column is currently unused.
- A user-initiated download with a choice of format (UC9) is **planned, not yet
  implemented** - see UC9.
