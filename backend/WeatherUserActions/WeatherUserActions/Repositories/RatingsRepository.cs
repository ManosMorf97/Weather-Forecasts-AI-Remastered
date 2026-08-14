using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using WeatherUserActions.Data;
using WeatherUserActions.Models;

namespace WeatherUserActions.Repositories
{
    public class RatingsRepository : IRatingsRepository
    {
        private readonly WeatherUserActionsDbContext _db;
        private readonly ILogger<RatingsRepository> _logger;

        public RatingsRepository(WeatherUserActionsDbContext db, ILogger<RatingsRepository> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<(bool Succeeded, bool ForecastExists)> UpsertRatingAsync(
            string userId, int forecastId, int value, CancellationToken cancellationToken = default)
        {
            try
            {
                var forecastExists = await _db.Forecasts.AnyAsync(forecast => forecast.ForecastId == forecastId, cancellationToken);
                if (!forecastExists)
                {
                    return (true, false);
                }

                var existingRating = await _db.Ratings
                    .SingleOrDefaultAsync(rating => rating.UserId == userId && rating.ForecastId == forecastId, cancellationToken);

                var now = DateTime.UtcNow;
                if (existingRating is null)
                {
                    _db.Ratings.Add(new Rating
                    {
                        UserId = userId,
                        ForecastId = forecastId,
                        Value = value,
                        CreatedAt = now,
                        UpdatedAt = now,
                    });
                }
                else
                {
                    existingRating.Value = value;
                    existingRating.UpdatedAt = now;
                }

                await _db.SaveChangesAsync(cancellationToken);
                return (true, true);
            }
            catch (DbException ex)
            {
                _logger.LogError(ex, "Failed to save rating for {UserId} on forecast {ForecastId}", userId, forecastId);
                return (false, true);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Failed to save rating for {UserId} on forecast {ForecastId}", userId, forecastId);
                return (false, true);
            }
        }

        public async Task<bool> RemoveRatingAsync(string userId, int forecastId, CancellationToken cancellationToken = default)
        {
            try
            {
                await _db.Ratings
                    .Where(rating => rating.UserId == userId && rating.ForecastId == forecastId)
                    .ExecuteDeleteAsync(cancellationToken);
                return true;
            }
            catch (DbException ex)
            {
                _logger.LogError(ex, "Failed to remove rating for {UserId} on forecast {ForecastId}", userId, forecastId);
                return false;
            }
        }
    }
}
