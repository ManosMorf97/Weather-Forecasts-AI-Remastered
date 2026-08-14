using WeatherUserActions.Dtos;

namespace WeatherUserActions.Repositories
{
    public interface IForecastsRepository
    {
        // Returns every stored Forecast (Timestamp >= now) for CitySites the user has selected,
        // each with the requesting user's own rating (null if unrated), ordered by Service name,
        // City name, then Timestamp. Returns (false, []) if the read could not be performed.
        Task<(bool Succeeded, List<ForecastItemDto> Forecasts)> TryGetUserForecastsAsync(
            string userId, CancellationToken cancellationToken = default);
    }
}
