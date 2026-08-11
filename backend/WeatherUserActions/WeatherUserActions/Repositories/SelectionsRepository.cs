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
        private async Task<List<int>> UpsertCitiesAsync(IReadOnlyCollection<CityDto> requestedCityDtos, CancellationToken cancellationToken)
        {
            //instead of checking all db (memory overload) we check a partion (City COuntry) of DTOs

            var distinctCityDtos = requestedCityDtos.DistinctBy(CitySignature).ToList();
            var requestedSignatures = distinctCityDtos.Select(CitySignature).ToHashSet();

            var requestedCityCountryfromDTO = distinctCityDtos.Select(cityDto => cityDto.Name + "|" + cityDto.Country).Distinct().ToList();

            // The DB fetch above only narrows by Name+Country, so it can return extra rows that
            // share a name/country with a requested city but not its exact Lat/Long. Re-filter down
            // to exact signature matches here, so everything after this point only deals with cities
            // that were actually requested.
            var existingDbCitiesByKey = (await _db.Cities
                .Where(dbCity => requestedCityCountryfromDTO.Contains(dbCity.Name + "|" + dbCity.Country))
                .ToDictionaryAsync(CitySignature, dbCity => dbCity.CityId, cancellationToken))
                .Where(entry => requestedSignatures.Contains(entry.Key))
                .ToDictionary(entry => entry.Key, entry => entry.Value);

            var newDbCities = distinctCityDtos
                .Where(cityDto => !existingDbCitiesByKey.ContainsKey(CitySignature(cityDto)))
                .Select(cityDto => new City
                {
                    Name = cityDto.Name,
                    Country = cityDto.Country,
                    Latitude = cityDto.Latitude,
                    Longitude = cityDto.Longitude,
                })
                .ToList();

            _db.Cities.AddRange(newDbCities);
            if (newDbCities.Count > 0)
            {
                try
                {
                    await _db.SaveChangesAsync(cancellationToken);
                }
                catch (DbUpdateException)
                {
                    // A concurrent request may have already inserted one or more of these
                    // cities (unique index on Name+Country+Latitude+Longitude); re-check
                    // what exists now and only insert whichever cities are still missing.
                    _db.ChangeTracker.Clear();

                    existingDbCitiesByKey = (await _db.Cities
                        .Where(dbCity => requestedCityCountryfromDTO.Contains(dbCity.Name + "|" + dbCity.Country))
                        .ToDictionaryAsync(CitySignature, dbCity => dbCity.CityId, cancellationToken))
                        .Where(entry => requestedSignatures.Contains(entry.Key))
                        .ToDictionary(entry => entry.Key, entry => entry.Value);

                    newDbCities = newDbCities
                        .Where(city => !existingDbCitiesByKey.ContainsKey(CitySignature(city)))
                        .ToList();

                    if (newDbCities.Count > 0)
                    {
                        _db.Cities.AddRange(newDbCities);
                        await _db.SaveChangesAsync(cancellationToken);
                    }
                }
            }

            var newDbCitiesByKey = newDbCities.ToDictionary(CitySignature, dbCity => dbCity.CityId);

            return existingDbCitiesByKey.Concat(newDbCitiesByKey)
                .Select(entry => entry.Value)
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

                    newCitySitesByKey[key] = new CitySite { CityId = cityId, ServiceId = serviceId };
                }
            }

            if (newCitySitesByKey.Count > 0)
            {
                _db.CitySites.AddRange(newCitySitesByKey.Values);
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
            var existingUserSelections = await _db.UserCitySites
                .Where(userCitySite => userCitySite.UserId == userId)
                .ToListAsync(cancellationToken);

            var staleUserSelections = existingUserSelections.Where(ucs => !targetCitySiteIds.Contains(ucs.CitySiteId));
            _db.UserCitySites.RemoveRange(staleUserSelections);

            var existingCitySiteIds = existingUserSelections.Select(ucs => ucs.CitySiteId).ToHashSet();
            var now = DateTime.UtcNow;
            var newUserCitySites = targetCitySiteIds
                .Where(citySiteId => !existingCitySiteIds.Contains(citySiteId))
                .Select(citySiteId => new UserCitySite { UserId = userId, CitySiteId = citySiteId, AddedAt = now })
                .ToList();

            _db.UserCitySites.AddRange(newUserCitySites);
        }

        private static (string Name, string Country, decimal Latitude, decimal Longitude) CitySignature(City city) =>
            (city.Name, city.Country, city.Latitude, city.Longitude);

        private static (string Name, string Country, decimal Latitude, decimal Longitude) CitySignature(CityDto city) =>
            (city.Name, city.Country, city.Latitude, city.Longitude);
    }
}
