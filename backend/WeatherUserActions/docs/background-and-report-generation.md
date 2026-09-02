# Background Processing & Report Generation — Learning Notes

Notes for understanding how the analytics report feature (UC8) works:
background workers, chart generation, and PDF generation.

Files involved:

| File | Role |
|------|------|
| `Controllers/AnalyticsController.cs` | Accepts the HTTP request, returns fast |
| `Services/AnalyticsService.cs` | The actual work: validate, compute, deliver |
| `BackgroundServices/AnalyticsReportWorker.cs` | The "clock" that drives the work |
| `Analytics/AnalyticsReportRenderer.cs` | Turns numbers into charts + a PDF |

---

## 1. Why a background worker at all?

Building a report is **slow** (big DB query, statistics, 3 charts per city,
render a PDF, look up an email, send it over SMTP). That can take many seconds.

If we did all of that *inside* the HTTP request:

- The user stares at a spinner for 30–60s. Browsers/proxies time out ~30–120s,
  so they may get an error even when the work would have finished.
- One failure (SMTP down at second 40) kills the whole request. The user must
  retry, and we recompute everything.
- 50 users clicking at once = 50 heavy jobs running on threads that are supposed
  to be answering *other* web requests.

So we split it:

```
HTTP request  ──►  validate + write "Queued" rows in DB  ──►  return 202 Accepted (fast)

Background worker  ──►  every few seconds: "anything queued? do it."
```

**What this saves: the USER's waiting time, not the computer's.**
The server does the same amount of work either way — it just happens in the
background where nobody is blocked waiting for it. Think of ordering coffee:
"got it, I'll call your name" (fast), you sit down, the coffee still takes
4 minutes to make.

It also gives us **reliability**: the request is saved as DB rows, so a failure
or a server restart doesn't lose it — the next tick retries.

---

## 2. How the worker starts and runs

### It is a "hosted service"

`Program.cs`:

```csharp
builder.Services.AddHostedService<AnalyticsReportWorker>();
```

This tells the .NET host: "run this class for the whole lifetime of the app."
Nobody *calls* the worker. When the app boots, the host starts it automatically,
alongside the web server. When the app shuts down, the host stops it.

- **Controller** = shop assistant: does nothing until a customer walks in (an HTTP request).
- **Worker** = a clock on the wall: the host winds it up at startup, it ticks on its own forever.

### `BackgroundService` and `ExecuteAsync`

`AnalyticsReportWorker : BackgroundService`. We override one method:

```csharp
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
```

The host calls `ExecuteAsync` once, in the background, and lets it run. Our whole
loop lives inside it.

### The timer

```csharp
using var timer = new PeriodicTimer(_pollInterval);   // e.g. every 5 seconds

while (!stoppingToken.IsCancellationRequested)
{
    // ... do one pass of work ...
    await timer.WaitForNextTickAsync(stoppingToken);   // sleep until next tick
}
```

`PeriodicTimer` is the trigger. Each loop: do a pass, then sleep ~5s, repeat.
`_pollInterval` comes from config (`Analytics:PollSeconds`, default 5, min 1).

### Polling the DB, not an in-memory queue

We *ask the database* "what's queued?" every tick, instead of keeping a list in
memory. Reason: if the process restarts, an in-memory list is gone, but the
`Queued` rows are still in the DB and get picked up.

### DI scope per tick

```csharp
using var scope = _scopeFactory.CreateScope();
var analyticsService = scope.ServiceProvider.GetRequiredService<IAnalyticsService>();
```

`AnalyticsService` (and its `DbContext`) is **scoped** — normally one instance per
HTTP request. The worker has no HTTP request, so it creates a fresh scope each
tick and disposes it at the end. This gives every tick a clean `DbContext`
instead of one giant long-lived one.

### Cancellation (graceful shutdown)

The `while` condition is only checked *between* ticks. But shutdown can happen
*during* a tick, while `ProcessQueuedReportsAsync(stoppingToken)` is running.
When the host cancels `stoppingToken`, that call throws
`OperationCanceledException` immediately.

```csharp
catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
{
    break;   // expected: we are shutting down, exit cleanly
}
catch (Exception ex)
{
    _logger.LogError(ex, "Analytics report worker iteration failed");
    // NOT rethrown -> loop continues on the next tick
}
```

- The `when (...)` filter means: only swallow the cancellation if it's *our*
  shutdown. Any other `OperationCanceledException` falls through to the generic
  catch and is logged as a real error.
- The generic `catch` keeps the loop alive: one bad tick is logged, the worker
  keeps going.

### Single instance only

A `Queued` row is not locked while being processed. Two copies of the app would
both grab it and send the report twice. Documented in the class comment: before
scaling to multiple instances, add a row-level claim (a `Processing` status +
owner + timestamp).

---

## 3. The work itself: `AnalyticsService.ProcessQueuedReportsAsync`

One tick runs **two independent passes**:

```csharp
await GenerateQueuedReportsAsync(cancellationToken);   // pass 1
await DeliverCompletedBatchesAsync(cancellationToken); // pass 2
```

### Pass 1 — Generate

For each `Queued` report:

1. Load raw forecast samples for its service + cities + date range.
2. `Summarise(...)` each city: average, standard deviation, min, max for
   temperature / humidity / wind, plus a count of "danger days".
3. Save the numbers with status `Completed` (or `Failed` if the query failed).

If saving fails, the report is left `Queued` and retried next tick.

### Pass 2 — Deliver

For each batch that is fully generated but not yet emailed:

1. Look up the user's email from Appwrite (`GetEmailAsync`, needs the API key —
   there is no user/JWT present in a background job).
2. Render a PDF from the completed sections.
3. Send the email with the PDF attached.
4. Only if the send succeeds, mark the batch `Delivered`.

**Why two separate passes?** If email/SMTP is down, the *numbers* we computed in
pass 1 are already saved. Pass 2 just retries the send next tick. We never
recompute an already-generated report because of a mail failure.

### Failure handling summary

| Failure | What happens |
|---------|--------------|
| Sample query fails | Report marked `Failed`, user gets a "could not generate" email |
| Result save fails | Report stays `Queued`, retried next tick |
| Email lookup fails | Batch stays undelivered, retried next tick |
| PDF render fails | Falls back to a **text-only** email (numbers still go out) |
| SMTP send fails | Batch stays undelivered, retried next tick |
| Whole tick throws | Logged, loop continues next tick |

---

## 4. Chart generation — ScottPlot

Package: `ScottPlot` 5.x. It's a plotting library — you give it numbers, it gives
you an image.

In `AnalyticsReportRenderer.BarChartPng(...)`:

```csharp
ScottPlot.Plot plot = new();

// one bar per city
var bars = section.Cities.Select((row, index) => new ScottPlot.Bar
{
    Position = index,                                  // x position (0, 1, 2, ...)
    Value    = (double)(value(row.Metrics) ?? 0m),     // bar height  (e.g. avg temperature)
    Error    = (double)(error(row.Metrics) ?? 0m),     // error bar   (e.g. std deviation)
}).ToList();

plot.Add.Bars(bars);

// label the x axis with city names instead of 0, 1, 2
var ticks = section.Cities
    .Select((row, index) => new ScottPlot.Tick(index, row.CityName))
    .ToArray();
plot.Axes.Bottom.TickGenerator = new ScottPlot.TickGenerators.NumericManual(ticks);

plot.Title(title);
plot.Axes.Left.Label.Text = yLabel;
plot.HideGrid();

return plot.GetImage(720, 380).GetImageBytes();   // -> PNG bytes (width 720, height 380)
```

Key ideas:

- A `Plot` is a blank canvas. You **add** things to it (bars, ticks, title).
- `Value` is the bar height; `Error` draws the little "whisker" showing spread
  (here, one standard deviation).
- Bars sit at integer positions `0, 1, 2...`. A custom `TickGenerator` swaps
  those numbers for the city names.
- `GetImage(w, h).GetImageBytes()` renders to an in-memory **PNG** (a `byte[]`).
  Nothing is written to disk.
- `?? 0m` — a city with no data has `null` metrics, so we draw a zero-height bar
  rather than crash. The city still appears.

Called 3 times per service section (temperature, humidity, wind speed). The
`Func<CityMetricResult, decimal?>` parameters (`value`, `error`) are just a way to
pass "which field to read" without writing the method 3 times.

---

## 5. PDF generation — QuestPDF

Package: `QuestPDF` 2026.x. A layout library — you describe the document with C#
code (like building HTML), it produces a PDF.

### Licence

```csharp
static AnalyticsReportRenderer()
{
    QuestPDF.Settings.License = LicenseType.Community;
}
```

A static constructor runs **once**, the first time the class is used. QuestPDF
requires you to declare a licence type or it throws. Community is free for our
use.

### Document structure

```csharp
Document.Create(container =>
{
    container.Page(page =>
    {
        page.Size(PageSizes.A4);
        page.Margin(30);
        page.DefaultTextStyle(style => style.FontSize(10));

        page.Header().Text("Weather analytics report").FontSize(18).SemiBold();

        page.Content().PaddingVertical(10).Column(column =>
        {
            column.Spacing(18);
            column.Item().Text($"Reference: {batchId}");

            foreach (var section in sections)
            {
                column.Item().Text($"{section.ServiceName} ...").FontSize(14).SemiBold();

                column.Item().Image(BarChartPng(...));   // <- the ScottPlot PNG bytes
                column.Item().Image(BarChartPng(...));
                column.Item().Image(BarChartPng(...));

                column.Item().Element(e => NumbersTable(e, section));
            }
        });
    });
}).GeneratePdf();   // -> byte[]
```

Mental model:

- `Document` → `Page` → `Header` / `Content` / `Footer`.
- `Content()` is a vertical `Column`. Each `column.Item()` is one stacked block.
- `column.Spacing(18)` puts 18 points of gap between items automatically.
- `.Image(pngBytes)` drops the chart image straight in — this is the link
  between ScottPlot and QuestPDF. Charts are PNGs; QuestPDF just embeds them.
- QuestPDF handles page breaks itself: if the content overflows A4, it flows
  onto page 2.
- `GeneratePdf()` returns a `byte[]` — again, nothing touches disk. That array
  becomes an email attachment:

  ```csharp
  var pdf = _reportRenderer.RenderPdf(batch.BatchId, sections);
  attachments = [new EmailAttachment($"analytics-{batch.BatchId}.pdf", "application/pdf", pdf)];
  ```

### The numbers table

```csharp
container.Table(table =>
{
    table.ColumnsDefinition(columns =>
    {
        columns.RelativeColumn(2);   // "City"  -> 2 parts wide
        columns.RelativeColumn();    // each data column -> 1 part wide
        columns.RelativeColumn();
        columns.RelativeColumn();
        columns.RelativeColumn();
        columns.RelativeColumn();
    });

    table.Header(header => { header.Cell().Text("City").SemiBold(); /* ... */ });

    foreach (var row in section.Cities)
    {
        table.Cell().Text(row.CityName);
        table.Cell().Text(AvgWithSpread(metric.AvgTemperature, metric.StdDevTemperature));
        // ...
    }
});
```

- `RelativeColumn(n)` splits the width **proportionally**. Factors here are
  `2,1,1,1,1,1` = 7 parts total, so "City" gets 2/7 and each data column 1/7.
  The city column is wider because it holds long names ("San Francisco").
- `table.Header(...)` cells repeat automatically if the table breaks across pages.
- Cells are filled left-to-right, top-to-bottom, in the order you call
  `table.Cell()`.

---

## 6. The whole flow, end to end

```
1. User -> POST /api/analytics            (AnalyticsController)
      RequestAnalyticsAsync: verify JWT, validate cities/services/date range,
      check the user actually selected those cities/services,
      write "Queued" rows (one per service) tagged with a batchId
   <- 202 Accepted { batchId }            (returns in milliseconds)

2. ... time passes ...

3. AnalyticsReportWorker tick (every ~5s)
      -> AnalyticsService.ProcessQueuedReportsAsync
         Pass 1 Generate:
            load forecast samples -> Summarise per city -> save Completed
         Pass 2 Deliver:
            GetEmailAsync (Appwrite, API key)
            for each service: 3 ScottPlot charts -> PNG
            QuestPDF: assemble charts + tables -> PDF byte[]
            email PDF as attachment
            mark batch Delivered

4. User receives an email with analytics-<batchId>.pdf attached.
   (UC9: they can also download it later.)
```

---

## 7. Glossary

| Term | Meaning |
|------|---------|
| Hosted service | A class the .NET host runs for the app's whole lifetime |
| `BackgroundService` | Base class for a hosted service with one `ExecuteAsync` loop |
| `PeriodicTimer` | Async timer; `WaitForNextTickAsync()` sleeps until the next interval |
| `CancellationToken` | A signal object; "stop what you're doing." Cancelled on shutdown |
| `OperationCanceledException` | Thrown when work is cancelled mid-flight via the token |
| DI scope | A lifetime boundary; scoped services live for one scope (usually one request) |
| Polling | Repeatedly asking "is there work?" instead of being pushed a notification |
| Idempotent / claim | Making sure the same job isn't processed twice |
| `byte[]` output | The image/PDF lives in memory only — never written to disk |
