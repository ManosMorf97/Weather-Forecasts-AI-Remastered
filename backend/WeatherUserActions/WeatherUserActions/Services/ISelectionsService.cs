using WeatherUserActions.Dtos;
using WeatherUserActions.Services.Results;

namespace WeatherUserActions.Services
{
    public interface ISelectionsService
    {
        // Verifies the JWT, then replaces the user's city/service selection with exactly
        // the given set (see SaveSelectionsRequest - not a merge).
        Task<SaveSelectionsResult> SaveSelectionsAsync(
            string jwt,
            IReadOnlyCollection<CityDto> cities,
            IReadOnlyCollection<int> serviceIds,
            CancellationToken cancellationToken = default);

        // Verifies the JWT, then returns the user's current city/service selections.
        Task<GetSelectionsResult> GetSelectionsAsync(string jwt, CancellationToken cancellationToken = default);
    }
}
