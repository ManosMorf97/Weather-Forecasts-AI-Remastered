using WeatherUserActions.Dtos;

namespace WeatherUserActions.Repositories
{
    public interface ISelectionsRepository
    {
        // Returns (true, allExist) on success, or (false, false) if the check could not be performed.
        Task<(bool Succeeded, bool AllExist)> TryValidateServiceIdsAsync(
            IReadOnlyCollection<int> serviceIds, CancellationToken cancellationToken = default);

        // Upserts the given cities (matched by exact Name+Country+Latitude+Longitude), links each city
        // to each service (cross product) via CitySite, and replaces the user's UserCitySite rows with
        // exactly that set. Returns false if persistence failed - nothing is partially applied.
        Task<bool> ReplaceUserSelectionAsync(
            string userId,
            IReadOnlyCollection<CityDto> cities,
            IReadOnlyCollection<int> serviceIds,
            CancellationToken cancellationToken = default);

        // Returns every ForecastingService flagged with whether the user has selected it (via a
        // real city selection or a pending no-city-yet pick), plus the cities from the user's
        // real selections. Returns (false, ..., ...) if the read could not be performed.
        Task<(bool Succeeded, List<ServiceSelectionDto> Services, List<SelectedCityDto> Cities)> TryGetUserSelectionsAsync(
            string userId, CancellationToken cancellationToken = default);
    }
}
