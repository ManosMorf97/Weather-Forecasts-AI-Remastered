using WeatherUserActions.Services.Results;

namespace WeatherUserActions.Services
{
    public interface IUserServicesService
    {
        // Verifies the JWT, then replaces the user's pending (no city yet) service
        // selection with exactly the given set (see SaveServicesRequest - not a merge).
        Task<SaveServicesResult> SaveServicesAsync(
            string jwt, IReadOnlyCollection<int> serviceIds, CancellationToken cancellationToken = default);
    }
}
