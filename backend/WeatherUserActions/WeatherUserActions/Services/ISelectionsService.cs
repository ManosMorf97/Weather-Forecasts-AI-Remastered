using WeatherUserActions.Dtos;
using WeatherUserActions.Services.Results;

namespace WeatherUserActions.Services
{
    public interface ISelectionsService
    {
        // Verifies the ID token, then replaces the user's city/service selection with exactly
        // the given set (see SaveSelectionsRequest - not a merge).
        Task<SaveSelectionsResult> SaveSelectionsAsync(
            string idToken,
            IReadOnlyCollection<CityDto> cities,
            IReadOnlyCollection<int> serviceIds,
            CancellationToken cancellationToken = default);

        // Verifies the ID token, then returns the user's current city/service selections.
        Task<GetSelectionsResult> GetSelectionsAsync(string idToken, CancellationToken cancellationToken = default);
    }
}
