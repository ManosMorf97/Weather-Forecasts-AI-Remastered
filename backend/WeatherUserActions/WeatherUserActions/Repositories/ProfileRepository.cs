using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using WeatherUserActions.Data;
using WeatherUserActions.Models;

namespace WeatherUserActions.Repositories
{
    public class ProfileRepository : IProfileRepository
    {
        private readonly WeatherUserActionsDbContext _db;
        private readonly ILogger<ProfileRepository> _logger;

        public ProfileRepository(WeatherUserActionsDbContext db, ILogger<ProfileRepository> logger)
        {
            _db = db;
            _logger = logger;
        }

        // Idempotent upsert: insert if absent, no-op if already present.
        public async Task<bool> EnsureUserProvisionedAsync(string userId, CancellationToken cancellationToken = default)
        {
            if (await _db.Users.FindAsync([userId], cancellationToken) is not null)
            {
                return true;
            }

            _db.Users.Add(new User { UserId = userId, CreatedAt = DateTime.UtcNow });

            try
            {
                await _db.SaveChangesAsync(cancellationToken);
                return true;
            }
            catch (DbUpdateException ex)
            {
                // A concurrent request may have already inserted the same user; that's success too.
                _db.ChangeTracker.Clear();
                if (await _db.Users.FindAsync([userId], cancellationToken) is not null)
                {
                    return true;
                }

                _logger.LogError(ex, "Failed to upsert user profile for {UserId}", userId);
                return false;
            }
        }

        public async Task<(bool Succeeded, bool HasCitySiteSelection)> TryGetHasCitySiteSelectionAsync(
            string userId, CancellationToken cancellationToken = default)
        {
            try
            {
                var hasCitySiteSelection = await _db.UserCitySites
                    .AnyAsync(userCitySite => userCitySite.UserId == userId, cancellationToken);
                return (true, hasCitySiteSelection);
            }
            catch (DbException ex)
            {
                _logger.LogError(ex, "Failed to check city site selection for {UserId}", userId);
                return (false, false);
            }
        }
    }
}
