namespace WeatherUserActions.Services
{
    public enum SaveServicesStatus
    {
        Unauthorized,
        InvalidServiceIds,
        Failed,
        Success,
    }

    public readonly record struct SaveServicesResult(SaveServicesStatus Status)
    {
        public static SaveServicesResult Unauthorized() => new(SaveServicesStatus.Unauthorized);

        public static SaveServicesResult InvalidServiceIds() => new(SaveServicesStatus.InvalidServiceIds);

        public static SaveServicesResult Failed() => new(SaveServicesStatus.Failed);

        public static SaveServicesResult Success() => new(SaveServicesStatus.Success);
    }

    public interface IUserServicesService
    {
        // Verifies the ID token, then replaces the user's pending (no city yet) service
        // selection with exactly the given set (see SaveServicesRequest - not a merge).
        Task<SaveServicesResult> SaveServicesAsync(
            string idToken, IReadOnlyCollection<int> serviceIds, CancellationToken cancellationToken = default);
    }
}
