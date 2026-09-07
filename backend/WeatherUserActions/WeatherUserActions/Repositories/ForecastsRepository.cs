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
                var now = DateTime.UtcNow;
                var forecasts = await (
                    from userCitySite in _db.UserCitySites
                    where userCitySite.UserId == userId
                    join forecast in _db.Forecasts on userCitySite.CitySiteId equals forecast.CitySiteId
                    where forecast.Timestamp >= now
                    join rating in _db.Ratings.Where(r => r.UserId == userId)
                        on forecast.ForecastId equals rating.ForecastId into ratingGroup
                    from rating in ratingGroup.DefaultIfEmpty()
                    orderby forecast.CitySite.Service.Name, forecast.CitySite.City.Name, forecast.Timestamp
                    select new ForecastItemDto(
                        forecast.ForecastId,
                        forecast.CitySite.City.Name,
                        forecast.CitySite.City.Country,
                        forecast.CitySite.Service.Name,
                        forecast.Type,
                        forecast.Timestamp,
                        forecast.OffsetMinutes,
                        forecast.Temperature,
                        forecast.Humidity,
                        forecast.WindSpeed,
                        forecast.DangerFlag,
                        rating != null ? (int?)rating.Value : null))
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
