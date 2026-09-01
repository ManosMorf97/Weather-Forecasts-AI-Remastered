using WeatherUserActions.Dtos;

namespace WeatherUserActions.Repositories
{
    public interface IAnalyticsRepository
    {
        // The distinct city ids and service ids the user currently has selected (via UserCitySite).
        // A requested analytics city/service is only valid if it appears in these sets.
        // Returns (false, [], []) if the read could not be performed.
        Task<(bool Succeeded, HashSet<int> CityIds, HashSet<int> ServiceIds)> TryGetUserSelectionScopeAsync(
            string userId, CancellationToken cancellationToken = default);

        // UC8: writes one Queued AnalyticsReport per service, all sharing batchId, each with an
        // (empty) AnalyticsReportCityMetric row per requested city. Returns false on failure.
        Task<bool> TryEnqueueReportsAsync(
            string userId,
            Guid batchId,
            IReadOnlyCollection<int> cityIds,
            IReadOnlyCollection<int> serviceIds,
            DateOnly dateRangeStart,
            DateOnly dateRangeEnd,
            CancellationToken cancellationToken = default);

        // Every AnalyticsReport still in Queued status, with the cities each one must cover.
        // Returns (false, []) if the read could not be performed.
        Task<(bool Succeeded, List<QueuedAnalyticsReport> Reports)> TryGetQueuedReportsAsync(
            CancellationToken cancellationToken = default);

        // Forecast rows (reduced to ForecastSample) for the given service and cities whose
        // Timestamp falls on or within [dateRangeStart, dateRangeEnd]. Returns (false, []) on failure.
        Task<(bool Succeeded, List<ForecastSample> Samples)> TryGetForecastSamplesAsync(
            int serviceId,
            IReadOnlyCollection<int> cityIds,
            DateOnly dateRangeStart,
            DateOnly dateRangeEnd,
            CancellationToken cancellationToken = default);

        // Writes the computed per-city numbers onto the report's AnalyticsReportCityMetric rows and
        // sets the report status. Returns false on failure.
        Task<bool> TrySaveReportResultAsync(
            int reportId,
            IReadOnlyCollection<CityMetricResult> cityMetrics,
            string status,
            CancellationToken cancellationToken = default);

        // Batches that have at least one report, no report still Queued, and DeliveredAt not set -
        // i.e. everything generated but not yet emailed. Returns (false, []) if the read failed.
        Task<(bool Succeeded, List<DeliverableAnalyticsBatch> Batches)> TryGetUndeliveredBatchesAsync(
            CancellationToken cancellationToken = default);

        // Stamps DeliveredAt on every report in the batch. Returns false on failure (the batch is
        // then retried by the next delivery pass).
        Task<bool> TryMarkBatchDeliveredAsync(Guid batchId, CancellationToken cancellationToken = default);
    }
}
