using WeatherUserActions.Dtos;

namespace WeatherUserActions.Repositories
{
    public interface IAggregatedForecastsRepository
    {
        // UC10 / UC12: for every one of the user's selected cities, ranks the selected services by
        // all-time average rating (across all users' ratings on that service's forecasts for the
        // city) and returns every current/upcoming forecast (Timestamp >= now) from the
        // highest-ranked service that actually has one, plus metadata about why that service won.
        // A lower-ranked service is used if a higher-ranked one has no upcoming forecast. Cities
        // where no selected service has any forecast data at all, or none has an upcoming forecast,
        // are omitted. Returns (false, [], []) if the read could not be performed.
        Task<(bool Succeeded, List<AggregatedForecastItemDto> Forecasts, List<ServiceAggregationMetadataDto> ServiceMetadata)>
            TryGetAggregatedForecastsAsync(string userId, CancellationToken cancellationToken = default);
    }
}
