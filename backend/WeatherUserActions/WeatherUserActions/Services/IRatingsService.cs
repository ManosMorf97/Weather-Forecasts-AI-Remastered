using WeatherUserActions.Services.Results;

namespace WeatherUserActions.Services
{
    public interface IRatingsService
    {
        // Verifies the JWT, then creates or updates the user's rating (1-5) for the given
        // forecast (UC7 main flow / A1).
        Task<RateForecastResult> RateForecastAsync(
            string jwt, int forecastId, int value, CancellationToken cancellationToken = default);

        // Verifies the JWT, then removes the user's rating for the given forecast, if any
        // (UC7 A2). Idempotent - succeeds even if no rating existed.
        Task<RemoveRatingResult> RemoveRatingAsync(
            string jwt, int forecastId, CancellationToken cancellationToken = default);
    }
}
