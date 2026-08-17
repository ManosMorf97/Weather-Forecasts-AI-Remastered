using WeatherUserActions.Services.Results;

namespace WeatherUserActions.Services
{
    public interface IAggregatedForecastsService
    {
        // Verifies the ID token, then returns the best-rated-service forecasts per selected city
        // (UC10 main flow), computed via UC12 (Calculate Aggregates).
        Task<GetAggregatedForecastsResult> GetAggregatedForecastsAsync(string idToken, CancellationToken cancellationToken = default);
    }
}
