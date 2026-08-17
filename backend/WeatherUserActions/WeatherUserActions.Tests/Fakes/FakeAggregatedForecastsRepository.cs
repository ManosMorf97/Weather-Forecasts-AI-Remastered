using WeatherUserActions.Dtos;
using WeatherUserActions.Repositories;

namespace WeatherUserActions.Tests.Fakes
{
    public class FakeAggregatedForecastsRepository : IAggregatedForecastsRepository
    {
        private readonly bool _succeeds;
        private readonly List<AggregatedForecastItemDto> _forecasts;
        private readonly List<ServiceAggregationMetadataDto> _serviceMetadata;

        private FakeAggregatedForecastsRepository(
            bool succeeds,
            List<AggregatedForecastItemDto>? forecasts = null,
            List<ServiceAggregationMetadataDto>? serviceMetadata = null)
        {
            _succeeds = succeeds;
            _forecasts = forecasts ?? [];
            _serviceMetadata = serviceMetadata ?? [];
        }

        public static FakeAggregatedForecastsRepository ReturningForecasts(
            List<AggregatedForecastItemDto> forecasts, List<ServiceAggregationMetadataDto> serviceMetadata) =>
            new(succeeds: true, forecasts: forecasts, serviceMetadata: serviceMetadata);

        public static FakeAggregatedForecastsRepository FailingToLoadForecasts() =>
            new(succeeds: false);

        public Task<(bool Succeeded, List<AggregatedForecastItemDto> Forecasts, List<ServiceAggregationMetadataDto> ServiceMetadata)>
            TryGetAggregatedForecastsAsync(string userId, CancellationToken cancellationToken = default) =>
            Task.FromResult((_succeeds, _forecasts, _serviceMetadata));
    }
}
