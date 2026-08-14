namespace WeatherUserActions.Services
{
    public interface IUserServicesService
    {
        // Verifies the ID token, then replaces the user's pending (no city yet) service
        // selection with exactly the given set (see SaveServicesRequest - not a merge).
        Task<SaveServicesResult> SaveServicesAsync(
            string idToken, IReadOnlyCollection<int> serviceIds, CancellationToken cancellationToken = default);
    }
}
