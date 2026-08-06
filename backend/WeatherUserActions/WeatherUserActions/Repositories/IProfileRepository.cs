namespace WeatherUserActions.Repositories
{
    public interface IProfileRepository
    {
        // Idempotent upsert: creates the user row if absent. Returns false if persistence failed.
        Task<bool> EnsureUserProvisionedAsync(string userId, CancellationToken cancellationToken = default);

        // Returns (true, selection) on success, or (false, false) if the check could not be performed.
        Task<(bool Succeeded, bool HasCitySiteSelection)> TryGetHasCitySiteSelectionAsync(
            string userId, CancellationToken cancellationToken = default);
    }
}
