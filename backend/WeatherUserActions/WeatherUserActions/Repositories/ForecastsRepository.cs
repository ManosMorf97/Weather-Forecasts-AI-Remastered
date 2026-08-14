using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using WeatherUserActions.Data;
using WeatherUserActions.Dtos;

namespace WeatherUserActions.Repositories
{
    public class ForecastsRepository : IForecastsRepository
    {
        private readonly WeatherUserActionsDbContext _db;
        private readonly ILogger<ForecastsRepository> _logger;

        public ForecastsRepository(WeatherUserActionsDbContext db, ILogger<ForecastsRepository> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<(bool Succeeded, List<ForecastItemDto> Forecasts)> TryGetUserForecastsAsync(
            string userId, CancellationToken cancellationToken = default)
        {
            try
            {
                var citySiteIds = await _db.UserCitySites
                    .Where(userCitySite => userCitySite.UserId == userId)
                    .Select(userCitySite => userCitySite.CitySiteId)
                    .ToListAsync(cancellationToken);

                var now = DateTime.UtcNow;
                var forecasts = await _db.Forecasts
                    .Where(forecast => citySiteIds.Contains(forecast.CitySiteId) && forecast.Timestamp >= now)
                    .Include(forecast => forecast.CitySite).ThenInclude(citySite => citySite.City)
                    .Include(forecast => forecast.CitySite).ThenInclude(citySite => citySite.Service)
                    .OrderBy(forecast => forecast.CitySite.Service.Name)
                    .ThenBy(forecast => forecast.CitySite.City.Name)
                    .ThenBy(forecast => forecast.Timestamp)
                    .Select(forecast => new ForecastItemDto(
                        forecast.CitySite.City.Name,
                        forecast.CitySite.City.Country,
                        forecast.CitySite.Service.Name,
                        forecast.Type,
                        forecast.Timestamp,
                        forecast.Temperature,
                        forecast.Humidity,
                        forecast.WindSpeed,
                        forecast.DangerFlag))
                    .ToListAsync(cancellationToken);

                return (true, forecasts);
            }
            catch (DbException ex)
            {
                _logger.LogError(ex, "Failed to load forecasts for {UserId}", userId);
                return (false, []);
            }
        }
    }
}
