using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using WeatherUserActions.Data;
using WeatherUserActions.Models;

namespace WeatherUserActions.Repositories
{
    public class UserServicesRepository : IUserServicesRepository
    {
        private readonly WeatherUserActionsDbContext _db;
        private readonly ILogger<UserServicesRepository> _logger;

        public UserServicesRepository(WeatherUserActionsDbContext db, ILogger<UserServicesRepository> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<(bool Succeeded, bool AllExist)> TryValidateServiceIdsAsync(
            IReadOnlyCollection<int> serviceIds, CancellationToken cancellationToken = default)
        {
            try
            {
                var distinctIds = serviceIds.Distinct().ToList();
                var existingCount = await _db.ForecastingServices
                    .CountAsync(service => distinctIds.Contains(service.ServiceId), cancellationToken);

                return (true, existingCount == distinctIds.Count);
            }
            catch (DbException ex)
            {
                _logger.LogError(ex, "Failed to validate service ids");
                return (false, false);
            }
        }

        public async Task<bool> ReplaceUserServicesAsync(
            string userId, IReadOnlyCollection<int> serviceIds, CancellationToken cancellationToken = default)
        {
            try
            {
                var distinctServiceIds = serviceIds.Distinct().ToHashSet();

                var existingUserServices = await _db.UserServices
                    .Where(userService => userService.UserId == userId)
                    .ToListAsync(cancellationToken);

                var staleUserServices = existingUserServices.Where(us => !distinctServiceIds.Contains(us.ServiceId));
                _db.UserServices.RemoveRange(staleUserServices);

                var existingServiceIds = existingUserServices.Select(us => us.ServiceId).ToHashSet();
                var now = DateTime.UtcNow;
                var newUserServices = distinctServiceIds
                    .Where(serviceId => !existingServiceIds.Contains(serviceId))
                    .Select(serviceId => new UserService { UserId = userId, ServiceId = serviceId, AddedAt = now })
                    .ToList();

                _db.UserServices.AddRange(newUserServices);

                await _db.SaveChangesAsync(cancellationToken);
                return true;
            }
            catch (DbException ex)
            {
                _logger.LogError(ex, "Failed to save pending service selection for {UserId}", userId);
                return false;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Failed to save pending service selection for {UserId}", userId);
                return false;
            }
        }
    }
}
