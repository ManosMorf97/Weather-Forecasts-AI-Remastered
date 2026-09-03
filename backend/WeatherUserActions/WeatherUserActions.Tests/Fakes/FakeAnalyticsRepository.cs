using WeatherUserActions.Dtos;
using WeatherUserActions.Repositories;

namespace WeatherUserActions.Tests.Fakes
{
    // Mocks only the IAnalyticsRepository boundary for AnalyticsControllerResponseTests -
    // AnalyticsService and AnalyticsController run for real.
    public class FakeAnalyticsRepository : IAnalyticsRepository
    {
        private readonly bool _scopeSucceeds;
        private readonly HashSet<int> _cityIds;
        private readonly HashSet<int> _serviceIds;
        private readonly bool _enqueueSucceeds;

        private FakeAnalyticsRepository(
            bool scopeSucceeds, HashSet<int> cityIds, HashSet<int> serviceIds, bool enqueueSucceeds)
        {
            _scopeSucceeds = scopeSucceeds;
            _cityIds = cityIds;
            _serviceIds = serviceIds;
            _enqueueSucceeds = enqueueSucceeds;
        }

        public static FakeAnalyticsRepository ReturningScope(IEnumerable<int> cityIds, IEnumerable<int> serviceIds) =>
            new(scopeSucceeds: true, cityIds.ToHashSet(), serviceIds.ToHashSet(), enqueueSucceeds: true);

        public static FakeAnalyticsRepository FailingToLoadScope() =>
            new(scopeSucceeds: false, [], [], enqueueSucceeds: true);

        public static FakeAnalyticsRepository FailingToEnqueue(IEnumerable<int> cityIds, IEnumerable<int> serviceIds) =>
            new(scopeSucceeds: true, cityIds.ToHashSet(), serviceIds.ToHashSet(), enqueueSucceeds: false);

        public Task<(bool Succeeded, HashSet<int> CityIds, HashSet<int> ServiceIds)> TryGetUserSelectionScopeAsync(
            string userId, CancellationToken cancellationToken = default) =>
            Task.FromResult((_scopeSucceeds, _cityIds, _serviceIds));

        public Task<bool> TryEnqueueReportsAsync(
            string userId,
            Guid batchId,
            IReadOnlyCollection<int> cityIds,
            IReadOnlyCollection<int> serviceIds,
            DateOnly dateRangeStart,
            DateOnly dateRangeEnd,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_enqueueSucceeds);

        // The methods below belong to the background-worker path (ProcessQueuedReportsAsync), which
        // AnalyticsControllerResponseTests never exercises - they run for real against Testcontainers
        // in AnalyticsControllerLogicTests. They throw so a test that accidentally reaches the worker
        // path via this fake fails loudly instead of silently getting an empty success.

        public Task<(bool Succeeded, List<QueuedAnalyticsReport> Reports)> TryGetQueuedReportsAsync(
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("FakeAnalyticsRepository does not implement the worker path.");

        public Task<(bool Succeeded, List<ForecastSample> Samples)> TryGetForecastSamplesAsync(
            int serviceId,
            IReadOnlyCollection<int> cityIds,
            DateOnly dateRangeStart,
            DateOnly dateRangeEnd,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("FakeAnalyticsRepository does not implement the worker path.");

        public Task<bool> TrySaveReportResultAsync(
            int reportId,
            IReadOnlyCollection<CityMetricResult> cityMetrics,
            string status,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("FakeAnalyticsRepository does not implement the worker path.");

        public Task<(bool Succeeded, List<DeliverableAnalyticsBatch> Batches)> TryGetUndeliveredBatchesAsync(
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("FakeAnalyticsRepository does not implement the worker path.");

        public Task<bool> TryMarkBatchDeliveredAsync(Guid batchId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("FakeAnalyticsRepository does not implement the worker path.");
    }
}
