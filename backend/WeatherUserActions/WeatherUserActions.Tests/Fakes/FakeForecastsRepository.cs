using WeatherUserActions.Dtos;
using WeatherUserActions.Repositories;

namespace WeatherUserActions.Tests.Fakes
{
    public class FakeForecastsRepository : IForecastsRepository
    {
        private readonly bool _succeeds;
        private readonly List<ForecastItemDto> _forecasts;

        private FakeForecastsRepository(bool succeeds, List<ForecastItemDto>? forecasts = null)
        {
            _succeeds = succeeds;
            _forecasts = forecasts ?? [];
        }

        public static FakeForecastsRepository ReturningForecasts(List<ForecastItemDto> forecasts) =>
            new(succeeds: true, forecasts: forecasts);

        public static FakeForecastsRepository FailingToLoadForecasts() =>
            new(succeeds: false);

        public Task<(bool Succeeded, List<ForecastItemDto> Forecasts)> TryGetUserForecastsAsync(
            string userId, CancellationToken cancellationToken = default) =>
            Task.FromResult((_succeeds, _forecasts));
    }
}
