using System.Text;
using WeatherUserActions.Analytics;
using WeatherUserActions.AppwriteServices;
using WeatherUserActions.Dtos;
using WeatherUserActions.Email;
using WeatherUserActions.Models;
using WeatherUserActions.Repositories;
using WeatherUserActions.Services.Results;

namespace WeatherUserActions.Services
{
    public class AnalyticsService : IAnalyticsService
    {
        // UC8 A1: reject a date range that is "too large" before it reaches the database. This is the
        // maximum number of days the range may span, counted inclusively.
        private const int MaxDateRangeDays = 366;

        private readonly IAppwriteAuthService _appwriteAuthService;
        private readonly IAppwriteUsersService _appwriteUsersService;
        private readonly IAnalyticsRepository _analyticsRepository;
        private readonly IAnalyticsReportRenderer _reportRenderer;
        private readonly IEmailSender _emailSender;
        private readonly ILogger<AnalyticsService> _logger;

        public AnalyticsService(
            IAppwriteAuthService appwriteAuthService,
            IAppwriteUsersService appwriteUsersService,
            IAnalyticsRepository analyticsRepository,
            IAnalyticsReportRenderer reportRenderer,
            IEmailSender emailSender,
            ILogger<AnalyticsService> logger)
        {
            _appwriteAuthService = appwriteAuthService;
            _appwriteUsersService = appwriteUsersService;
            _analyticsRepository = analyticsRepository;
            _reportRenderer = reportRenderer;
            _emailSender = emailSender;
            _logger = logger;
        }

        public async Task<RequestAnalyticsResult> RequestAnalyticsAsync(
            string jwt, RequestAnalyticsRequest request, CancellationToken cancellationToken = default)
        {
            string userId;
            try
            {
                userId = await _appwriteAuthService.VerifyJwtAsync(jwt, cancellationToken);
            }
            catch (AppwriteTokenVerificationException ex)
            {
                _logger.LogWarning(ex, "Appwrite JWT verification failed");
                return RequestAnalyticsResult.Unauthorized();
            }

            var cityIds = request.CityIds?.Distinct().ToList() ?? [];
            var serviceIds = request.ServiceIds?.Distinct().ToList() ?? [];

            if (cityIds.Count == 0)
            {
                return RequestAnalyticsResult.InvalidCities();
            }

            if (serviceIds.Count == 0)
            {
                return RequestAnalyticsResult.InvalidServices();
            }

            var rangeDays = request.DateRangeEnd.DayNumber - request.DateRangeStart.DayNumber + 1;
            if (request.DateRangeStart > request.DateRangeEnd || rangeDays > MaxDateRangeDays)
            {
                return RequestAnalyticsResult.InvalidDateRange();
            }

            var (scopeLoaded, selectedCityIds, selectedServiceIds) =
                await _analyticsRepository.TryGetUserSelectionScopeAsync(userId, cancellationToken);
            if (!scopeLoaded)
            {
                return RequestAnalyticsResult.Failed();
            }

            if (!cityIds.All(selectedCityIds.Contains))
            {
                return RequestAnalyticsResult.InvalidCities();
            }

            if (!serviceIds.All(selectedServiceIds.Contains))
            {
                return RequestAnalyticsResult.InvalidServices();
            }

            var batchId = Guid.NewGuid();
            var enqueued = await _analyticsRepository.TryEnqueueReportsAsync(
                userId, batchId, cityIds, serviceIds, request.DateRangeStart, request.DateRangeEnd, cancellationToken);

            return enqueued ? RequestAnalyticsResult.Accepted(batchId) : RequestAnalyticsResult.Failed();
        }

        // One worker tick: generate every queued report, then email every batch that has finished
        // generating but not yet been delivered. The two passes are independent so a mail-transport
        // or user-lookup failure only delays delivery - it never loses an already-generated report.
        public async Task ProcessQueuedReportsAsync(CancellationToken cancellationToken = default)
        {
            await GenerateQueuedReportsAsync(cancellationToken);
            await DeliverCompletedBatchesAsync(cancellationToken);
        }

        // --- generation ---

        private async Task GenerateQueuedReportsAsync(CancellationToken cancellationToken)
        {
            var (loaded, reports) = await _analyticsRepository.TryGetQueuedReportsAsync(cancellationToken);
            if (!loaded || reports.Count == 0)
            {
                return;
            }

            foreach (var report in reports)
            {
                await GenerateReportAsync(report, cancellationToken);
            }
        }

        private async Task GenerateReportAsync(QueuedAnalyticsReport report, CancellationToken cancellationToken)
        {
            var cityIds = report.Cities.Select(city => city.CityId).ToList();
            var (samplesLoaded, samples) = await _analyticsRepository.TryGetForecastSamplesAsync(
                report.ServiceId, cityIds, report.DateRangeStart, report.DateRangeEnd, cancellationToken);

            if (!samplesLoaded)
            {
                await _analyticsRepository.TrySaveReportResultAsync(
                    report.ReportId, [], AnalyticsReportStatus.Failed, cancellationToken);
                return;
            }

            var samplesByCity = samples.ToLookup(sample => sample.CityId);
            var metrics = report.Cities
                .Select(city => Summarise(city.CityId, samplesByCity[city.CityId].ToList()))
                .ToList();

            var saved = await _analyticsRepository.TrySaveReportResultAsync(
                report.ReportId, metrics, AnalyticsReportStatus.Completed, cancellationToken);

            if (!saved)
            {
                // The report is left Queued - the next worker tick retries it.
                _logger.LogWarning(
                    "Analytics report {ReportId} was generated but its result could not be saved; will retry",
                    report.ReportId);
            }
        }

        // --- delivery ---

        private async Task DeliverCompletedBatchesAsync(CancellationToken cancellationToken)
        {
            var (loaded, batches) = await _analyticsRepository.TryGetUndeliveredBatchesAsync(cancellationToken);
            if (!loaded || batches.Count == 0)
            {
                return;
            }

            foreach (var batch in batches)
            {
                await DeliverBatchAsync(batch, cancellationToken);
            }
        }

        private async Task DeliverBatchAsync(DeliverableAnalyticsBatch batch, CancellationToken cancellationToken)
        {
            string email;
            try
            {
                email = await _appwriteUsersService.GetEmailAsync(batch.UserId, cancellationToken);
            }
            catch (AppwriteUserLookupException ex)
            {
                // The batch is not marked delivered, so the next tick retries the lookup.
                _logger.LogError(
                    ex, "Analytics batch {BatchId} is ready but the user's email lookup failed; will retry", batch.BatchId);
                return;
            }

            var completed = batch.Reports
                .Where(report => report.Status == AnalyticsReportStatus.Completed)
                .ToList();
            var failedServiceNames = batch.Reports
                .Where(report => report.Status != AnalyticsReportStatus.Completed)
                .Select(report => report.ServiceName)
                .ToList();

            bool delivered;
            if (completed.Count == 0)
            {
                delivered = await SendSafelyAsync(
                    email, "Your analytics report could not be generated", FailureBody(batch.BatchId),
                    attachments: null, batch.BatchId, cancellationToken);
            }
            else
            {
                var sections = completed
                    .Select(report => new AnalyticsReportSection(
                        report.ServiceName, batch.DateRangeStart, batch.DateRangeEnd, report.Cities))
                    .ToList();

                IReadOnlyCollection<EmailAttachment>? attachments = null;
                try
                {
                    var pdf = _reportRenderer.RenderPdf(batch.BatchId, sections);
                    attachments = [new EmailAttachment($"analytics-{batch.BatchId}.pdf", "application/pdf", pdf)];
                }
                catch (Exception ex)
                {
                    // The numbers were generated fine - fall back to a text-only email rather than fail.
                    _logger.LogError(ex, "Failed to render the analytics PDF for batch {BatchId}; sending text-only", batch.BatchId);
                }

                var subject = failedServiceNames.Count == 0
                    ? "Your analytics report is ready"
                    : "Your analytics report is ready (some services could not be processed)";

                delivered = await SendSafelyAsync(
                    email, subject, SuccessBody(batch.BatchId, sections, failedServiceNames),
                    attachments, batch.BatchId, cancellationToken);
            }

            if (delivered)
            {
                await _analyticsRepository.TryMarkBatchDeliveredAsync(batch.BatchId, cancellationToken);
            }
        }

        private async Task<bool> SendSafelyAsync(
            string email,
            string subject,
            string body,
            IReadOnlyCollection<EmailAttachment>? attachments,
            Guid batchId,
            CancellationToken cancellationToken)
        {
            try
            {
                await _emailSender.SendAsync(email, subject, body, attachments, cancellationToken);
                return true;
            }
            catch (Exception ex)
            {
                // A mail-transport failure must not fail an already-generated report - leave the batch
                // undelivered so the next tick retries it.
                _logger.LogError(ex, "Failed to email analytics batch {BatchId}; will retry", batchId);
                return false;
            }
        }

        // One forecast city summary. Averages/spread are null when there is no data (UC8: the city
        // still appears in the report, just empty).
        private static CityMetricResult Summarise(int cityId, IReadOnlyList<ForecastSample> samples)
        {
            if (samples.Count == 0)
            {
                return new CityMetricResult(cityId,
                    null, null, null, null,
                    null, null, null, null,
                    null, null, null, null,
                    DangerDayCount: 0, SampleCount: 0);
            }

            var temperatures = samples.Select(sample => sample.Temperature).ToList();
            var humidities = samples.Select(sample => sample.Humidity).ToList();
            var windSpeeds = samples.Select(sample => sample.WindSpeed).ToList();

            var dangerDayCount = samples
                .Where(sample => sample.DangerFlag)
                .Select(sample => DateOnly.FromDateTime(sample.Timestamp))
                .Distinct()
                .Count();

            return new CityMetricResult(cityId,
                Average(temperatures), StandardDeviation(temperatures), temperatures.Min(), temperatures.Max(),
                Average(humidities), StandardDeviation(humidities), humidities.Min(), humidities.Max(),
                Average(windSpeeds), StandardDeviation(windSpeeds), windSpeeds.Min(), windSpeeds.Max(),
                dangerDayCount, samples.Count);
        }

        private static decimal Average(IReadOnlyCollection<decimal> values) => Math.Round(values.Average(), 2);

        // Population standard deviation, rounded like the averages; 0 for a single sample.
        private static decimal StandardDeviation(IReadOnlyCollection<decimal> values)
        {
            if (values.Count == 1)
            {
                return 0m;
            }

            var mean = values.Average();
            var variance = values.Sum(value => (value - mean) * (value - mean)) / values.Count;
            return Math.Round((decimal)Math.Sqrt((double)variance), 2);
        }

        private static string SuccessBody(
            Guid batchId, List<AnalyticsReportSection> sections, IReadOnlyCollection<string> failedServiceNames)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"Your analytics report (reference {batchId}) is ready.");
            builder.AppendLine();

            foreach (var section in sections)
            {
                builder.AppendLine(
                    $"{section.ServiceName}  ({section.DateRangeStart:yyyy-MM-dd} to {section.DateRangeEnd:yyyy-MM-dd})");

                foreach (var row in section.Cities)
                {
                    var metric = row.Metrics;

                    if (metric.SampleCount == 0)
                    {
                        builder.AppendLine($"  {row.CityName}: no forecast data in this range");
                        continue;
                    }

                    builder.AppendLine($"  {row.CityName} ({metric.SampleCount} forecasts)");
                    builder.AppendLine(
                        $"    Temperature: avg {metric.AvgTemperature} (SD {metric.StdDevTemperature}), range {metric.MinTemperature} to {metric.MaxTemperature}");
                    builder.AppendLine(
                        $"    Humidity:    avg {metric.AvgHumidity} (SD {metric.StdDevHumidity}), range {metric.MinHumidity} to {metric.MaxHumidity}");
                    builder.AppendLine(
                        $"    Wind speed:  avg {metric.AvgWindSpeed} (SD {metric.StdDevWindSpeed}), range {metric.MinWindSpeed} to {metric.MaxWindSpeed}");
                    builder.AppendLine($"    Danger days: {metric.DangerDayCount}");
                }

                builder.AppendLine();
            }

            if (failedServiceNames.Count > 0)
            {
                builder.AppendLine(
                    $"We could not process the following service(s): {string.Join(", ", failedServiceNames)}.");
                builder.AppendLine();
            }

            builder.AppendLine("The full report with charts is attached as a PDF.");
            return builder.ToString();
        }

        private static string FailureBody(Guid batchId) =>
            $"We could not generate your analytics report (reference {batchId}). Please request it again.";
    }
}
