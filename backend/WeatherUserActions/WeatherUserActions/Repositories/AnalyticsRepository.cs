using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using WeatherUserActions.Analytics;
using WeatherUserActions.Data;
using WeatherUserActions.Dtos;
using WeatherUserActions.Models;

namespace WeatherUserActions.Repositories
{
    public class AnalyticsRepository : IAnalyticsRepository
    {
        private readonly WeatherUserActionsDbContext _db;
        private readonly ILogger<AnalyticsRepository> _logger;

        public AnalyticsRepository(WeatherUserActionsDbContext db, ILogger<AnalyticsRepository> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<(bool Succeeded, HashSet<int> CityIds, HashSet<int> ServiceIds)> TryGetUserSelectionScopeAsync(
            string userId, CancellationToken cancellationToken = default)
        {
            try
            {
                var pairs = await _db.UserCitySites
                    .Where(userCitySite => userCitySite.UserId == userId)
                    .Select(userCitySite => new { userCitySite.CitySite.CityId, userCitySite.CitySite.ServiceId })
                    .ToListAsync(cancellationToken);

                var cityIds = pairs.Select(pair => pair.CityId).ToHashSet();
                var serviceIds = pairs.Select(pair => pair.ServiceId).ToHashSet();
                return (true, cityIds, serviceIds);
            }
            catch (DbException ex)
            {
                _logger.LogError(ex, "Failed to load analytics selection scope for {UserId}", userId);
                return (false, [], []);
            }
        }

        public async Task<bool> TryEnqueueReportsAsync(
            string userId,
            Guid batchId,
            IReadOnlyCollection<int> cityIds,
            IReadOnlyCollection<int> serviceIds,
            DateOnly dateRangeStart,
            DateOnly dateRangeEnd,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var distinctCityIds = cityIds.Distinct().ToList();
                var batch = new AnalyticsReportBatch
                {
                    BatchId = batchId,
                    UserId = userId,
                    DateRangeStart = dateRangeStart,
                    DateRangeEnd = dateRangeEnd,
                    Format = "JSON",
                    CreatedAt = DateTime.UtcNow,
                    Reports = serviceIds.Distinct().Select(serviceId => new AnalyticsReport
                    {
                        ServiceId = serviceId,
                        Status = AnalyticsReportStatus.Queued,
                        CityMetrics = distinctCityIds
                            .Select(cityId => new AnalyticsReportCityMetric { CityId = cityId })
                            .ToList(),
                    }).ToList(),
                };

                _db.AnalyticsReportBatches.Add(batch);
                await _db.SaveChangesAsync(cancellationToken);
                return true;
            }
            catch (DbException ex)
            {
                _logger.LogError(ex, "Failed to queue analytics reports for {UserId}", userId);
                return false;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Failed to queue analytics reports for {UserId}", userId);
                return false;
            }
        }

        public async Task<(bool Succeeded, List<QueuedAnalyticsReport> Reports)> TryGetQueuedReportsAsync(
            CancellationToken cancellationToken = default)
        {
            try
            {
                var reports = await _db.AnalyticsReports
                    .Where(report => report.Status == AnalyticsReportStatus.Queued)
                    .OrderBy(report => report.ReportId)
                    .Select(report => new QueuedAnalyticsReport(
                        report.ReportId,
                        report.BatchId,
                        report.Batch.UserId,
                        report.ServiceId,
                        report.Service.Name,
                        report.Batch.DateRangeStart,
                        report.Batch.DateRangeEnd,
                        report.CityMetrics
                            .Select(metric => new QueuedAnalyticsReportCity(metric.CityId, metric.City.Name, metric.City.Country))
                            .ToList()))
                    .ToListAsync(cancellationToken);

                return (true, reports);
            }
            catch (DbException ex)
            {
                _logger.LogError(ex, "Failed to load queued analytics reports");
                return (false, []);
            }
        }

        public async Task<(bool Succeeded, List<ForecastSample> Samples)> TryGetForecastSamplesAsync(
            int serviceId,
            IReadOnlyCollection<int> cityIds,
            DateOnly dateRangeStart,
            DateOnly dateRangeEnd,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var distinctCityIds = cityIds.Distinct().ToList();
                var rangeStart = dateRangeStart.ToDateTime(TimeOnly.MinValue);
                // End date is inclusive - take everything strictly before the following midnight.
                var rangeEndExclusive = dateRangeEnd.AddDays(1).ToDateTime(TimeOnly.MinValue);

                var samples = await _db.Forecasts
                    .Where(forecast => forecast.CitySite.ServiceId == serviceId
                        && distinctCityIds.Contains(forecast.CitySite.CityId)
                        && forecast.Timestamp >= rangeStart
                        && forecast.Timestamp < rangeEndExclusive)
                    .Select(forecast => new ForecastSample(
                        forecast.CitySite.CityId,
                        forecast.Temperature,
                        forecast.Humidity,
                        forecast.WindSpeed,
                        forecast.Timestamp,
                        forecast.DangerFlag))
                    .ToListAsync(cancellationToken);

                return (true, samples);
            }
            catch (DbException ex)
            {
                _logger.LogError(ex, "Failed to load forecast samples for service {ServiceId}", serviceId);
                return (false, []);
            }
        }

        public async Task<bool> TrySaveReportResultAsync(
            int reportId,
            IReadOnlyCollection<CityMetricResult> cityMetrics,
            string status,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var report = await _db.AnalyticsReports
                    .Include(analyticsReport => analyticsReport.CityMetrics)
                    .SingleOrDefaultAsync(analyticsReport => analyticsReport.ReportId == reportId, cancellationToken);

                if (report is null)
                {
                    _logger.LogWarning("Analytics report {ReportId} vanished before its result could be saved", reportId);
                    return false;
                }

                var resultsByCity = cityMetrics.ToDictionary(result => result.CityId);
                foreach (var metric in report.CityMetrics)
                {
                    if (!resultsByCity.TryGetValue(metric.CityId, out var result))
                    {
                        continue;
                    }

                    metric.AvgTemperature = result.AvgTemperature;
                    metric.StdDevTemperature = result.StdDevTemperature;
                    metric.MinTemperature = result.MinTemperature;
                    metric.MaxTemperature = result.MaxTemperature;
                    metric.AvgHumidity = result.AvgHumidity;
                    metric.StdDevHumidity = result.StdDevHumidity;
                    metric.MinHumidity = result.MinHumidity;
                    metric.MaxHumidity = result.MaxHumidity;
                    metric.AvgWindSpeed = result.AvgWindSpeed;
                    metric.StdDevWindSpeed = result.StdDevWindSpeed;
                    metric.MinWindSpeed = result.MinWindSpeed;
                    metric.MaxWindSpeed = result.MaxWindSpeed;
                    metric.DangerDayCount = result.DangerDayCount;
                    metric.SampleCount = result.SampleCount;
                }

                report.Status = status;
                await _db.SaveChangesAsync(cancellationToken);
                return true;
            }
            catch (DbException ex)
            {
                _logger.LogError(ex, "Failed to save analytics result for report {ReportId}", reportId);
                return false;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Failed to save analytics result for report {ReportId}", reportId);
                return false;
            }
        }

        public async Task<(bool Succeeded, List<DeliverableAnalyticsBatch> Batches)> TryGetUndeliveredBatchesAsync(
            CancellationToken cancellationToken = default)
        {
            try
            {
                var batches = await _db.AnalyticsReportBatches
                    .AsNoTracking()
                    .Where(batch => batch.DeliveredAt == null
                        && batch.Reports.Any()
                        && batch.Reports.All(report => report.Status != AnalyticsReportStatus.Queued))
                    .OrderBy(batch => batch.BatchId)
                    .Select(batch => new DeliverableAnalyticsBatch(
                        batch.BatchId,
                        batch.UserId,
                        batch.DateRangeStart,
                        batch.DateRangeEnd,
                        batch.Reports
                            .OrderBy(report => report.ServiceId)
                            .Select(report => new DeliverableAnalyticsReport(
                                report.Service.Name,
                                report.Status,
                                report.CityMetrics
                                    .OrderBy(metric => metric.CityId)
                                    .Select(metric => new AnalyticsReportCityRow(
                                        metric.City.Name,
                                        new CityMetricResult(
                                            metric.CityId,
                                            metric.AvgTemperature, metric.StdDevTemperature, metric.MinTemperature, metric.MaxTemperature,
                                            metric.AvgHumidity, metric.StdDevHumidity, metric.MinHumidity, metric.MaxHumidity,
                                            metric.AvgWindSpeed, metric.StdDevWindSpeed, metric.MinWindSpeed, metric.MaxWindSpeed,
                                            metric.DangerDayCount, metric.SampleCount)))
                                    .ToList()))
                            .ToList()))
                    .ToListAsync(cancellationToken);

                return (true, batches);
            }
            catch (DbException ex)
            {
                _logger.LogError(ex, "Failed to load undelivered analytics batches");
                return (false, []);
            }
        }

        public async Task<bool> TryMarkBatchDeliveredAsync(Guid batchId, CancellationToken cancellationToken = default)
        {
            try
            {
                var batch = await _db.AnalyticsReportBatches
                    .SingleOrDefaultAsync(batch => batch.BatchId == batchId, cancellationToken);

                if (batch is null)
                {
                    _logger.LogWarning("Analytics batch {BatchId} vanished before it could be marked delivered", batchId);
                    return false;
                }

                batch.DeliveredAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(cancellationToken);
                return true;
            }
            catch (DbException ex)
            {
                _logger.LogError(ex, "Failed to mark analytics batch {BatchId} delivered", batchId);
                return false;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Failed to mark analytics batch {BatchId} delivered", batchId);
                return false;
            }
        }
    }
}
