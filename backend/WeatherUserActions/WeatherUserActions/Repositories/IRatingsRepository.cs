namespace WeatherUserActions.Repositories
{
    public interface IRatingsRepository
    {
        // Creates or updates the user's rating (unique on UserId+ForecastId) for the given
        // forecast. Returns (true, false) if forecastId doesn't exist - nothing is written.
        // Returns (false, true) if persistence failed.
        Task<(bool Succeeded, bool ForecastExists)> UpsertRatingAsync(
            string userId, int forecastId, int value, CancellationToken cancellationToken = default);

        // Deletes the user's rating for the given forecast, if one exists. Idempotent - returns
        // true even when no rating existed. Returns false only if persistence failed.
        Task<bool> RemoveRatingAsync(string userId, int forecastId, CancellationToken cancellationToken = default);
    }
}
