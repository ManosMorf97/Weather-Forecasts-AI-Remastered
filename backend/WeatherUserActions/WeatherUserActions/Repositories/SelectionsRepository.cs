using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using WeatherUserActions.Data;
using WeatherUserActions.Dtos;
using WeatherUserActions.Models;

namespace WeatherUserActions.Repositories
{
    public class SelectionsRepository : ISelectionsRepository
    {
        private readonly WeatherUserActionsDbContext _db;
        private readonly ILogger<SelectionsRepository> _logger;

        public SelectionsRepository(WeatherUserActionsDbContext db, ILogger<SelectionsRepository> logger)
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

        public async Task<bool> ReplaceUserSelectionAsync(
            string userId,
            IReadOnlyCollection<CityDto> cities,
            IReadOnlyCollection<int> serviceIds,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

                var cityIds = await UpsertCitiesAsync(cities, cancellationToken);
                var citySiteIds = await UpsertCitySitesAsync(cityIds, serviceIds.Distinct().ToList(), cancellationToken);
                await ReplaceUserCitySitesAsync(userId, citySiteIds, cancellationToken);

                await _db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return true;
            }
            catch (DbException ex)
            {
                _logger.LogError(ex, "Failed to save city/service selection for {UserId}", userId);
                return false;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Failed to save city/service selection for {UserId}", userId);
                return false;
            }
        }

        // Fetches cities already in the DB (A), creates whichever of the requested cities are
        // missing, and returns the CityId for every requested city.
        private async Task<List<int>> UpsertCitiesAsync(IReadOnlyCollection<CityDto> cities, CancellationToken cancellationToken)
        {
            var names = cities.Select(city => city.Name).Distinct().ToList();
            var countries = cities.Select(city => city.Country).Distinct().ToList();

            var existingByKey = await _db.Cities
                .Where(city => names.Contains(city.Name) && countries.Contains(city.Country))
                .ToDictionaryAsync(CityKey, city => city.CityId, cancellationToken);

            var newCitiesByKey = new Dictionary<(string, string, decimal, decimal), City>();
            foreach (var cityDto in cities)
            {
                var key = CityKey(cityDto);
                if (existingByKey.ContainsKey(key) || newCitiesByKey.ContainsKey(key))
                {
                    continue;
                }

                var newCity = new City
                {
                    Name = cityDto.Name,
                    Country = cityDto.Country,
                    Latitude = cityDto.Latitude,
                    Longitude = cityDto.Longitude,
                };
                newCitiesByKey[key] = newCity;
                _db.Cities.Add(newCity);
            }

            if (newCitiesByKey.Count > 0)
            {
                await _db.SaveChangesAsync(cancellationToken);
            }

            return cities
                .Select(cityDto => existingByKey.TryGetValue(CityKey(cityDto), out var cityId)
                    ? cityId
                    : newCitiesByKey[CityKey(cityDto)].CityId)
                .Distinct()
                .ToList();
        }

        // Fetches CitySites already in the DB for this (cities x services) cross product, creates
        // whichever pairs are missing, and returns the full target set of CitySiteIds.
        private async Task<HashSet<int>> UpsertCitySitesAsync(
            IReadOnlyCollection<int> cityIds, IReadOnlyCollection<int> serviceIds, CancellationToken cancellationToken)
        {
            var existingByKey = await _db.CitySites
                .Where(citySite => cityIds.Contains(citySite.CityId) && serviceIds.Contains(citySite.ServiceId))
                .ToDictionaryAsync(citySite => (citySite.CityId, citySite.ServiceId), citySite => citySite.CitySiteId, cancellationToken);

            var newCitySitesByKey = new Dictionary<(int, int), CitySite>();
            foreach (var cityId in cityIds)
            {
                foreach (var serviceId in serviceIds)
                {
                    var key = (cityId, serviceId);
                    if (existingByKey.ContainsKey(key) || newCitySitesByKey.ContainsKey(key))
                    {
                        continue;
                    }

                    var newCitySite = new CitySite { CityId = cityId, ServiceId = serviceId };
                    newCitySitesByKey[key] = newCitySite;
                    _db.CitySites.Add(newCitySite);
                }
            }

            if (newCitySitesByKey.Count > 0)
            {
                await _db.SaveChangesAsync(cancellationToken);
            }

            var citySiteIds = new HashSet<int>(existingByKey.Values);
            foreach (var citySite in newCitySitesByKey.Values)
            {
                citySiteIds.Add(citySite.CitySiteId);
            }

            return citySiteIds;
        }

        // Removes the user's UserCitySite rows that are no longer wanted and adds the ones that are
        // new; rows already matching the target set are left untouched (AddedAt is preserved).
        private async Task ReplaceUserCitySitesAsync(
            string userId, HashSet<int> targetCitySiteIds, CancellationToken cancellationToken)
        {
            var existingSelections = await _db.UserCitySites
                .Where(userCitySite => userCitySite.UserId == userId)
                .ToListAsync(cancellationToken);

            var existingCitySiteIds = existingSelections.Select(ucs => ucs.CitySiteId).ToHashSet();

            _db.UserCitySites.RemoveRange(
                existingSelections.Where(ucs => !targetCitySiteIds.Contains(ucs.CitySiteId)));

            var now = DateTime.UtcNow;
            foreach (var citySiteId in targetCitySiteIds.Where(id => !existingCitySiteIds.Contains(id)))
            {
                _db.UserCitySites.Add(new UserCitySite { UserId = userId, CitySiteId = citySiteId, AddedAt = now });
            }
        }

        private static (string Name, string Country, decimal Latitude, decimal Longitude) CityKey(City city) =>
            (city.Name, city.Country, city.Latitude, city.Longitude);

        private static (string Name, string Country, decimal Latitude, decimal Longitude) CityKey(CityDto city) =>
            (city.Name, city.Country, city.Latitude, city.Longitude);
    }
}
