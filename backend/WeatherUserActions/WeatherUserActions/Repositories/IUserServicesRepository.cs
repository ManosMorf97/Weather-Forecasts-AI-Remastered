namespace WeatherUserActions.Repositories
{
    public interface IUserServicesRepository
    {
        // Returns (true, allExist) on success, or (false, false) if the check could not be performed.
        Task<(bool Succeeded, bool AllExist)> TryValidateServiceIdsAsync(
            IReadOnlyCollection<int> serviceIds, CancellationToken cancellationToken = default);

        // Replaces the user's pending (no city yet) UserService rows with exactly the given set.
        // Rows already matching the target set are left untouched (AddedAt is preserved).
        // Returns false if persistence failed - nothing is partially applied.
        Task<bool> ReplaceUserServicesAsync(
            string userId, IReadOnlyCollection<int> serviceIds, CancellationToken cancellationToken = default);
    }
}
