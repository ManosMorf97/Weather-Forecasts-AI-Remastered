using WeatherUserActions.Dtos;

namespace WeatherUserActions.Services
{
    public enum SaveSelectionsStatus
    {
        Unauthorized,
        InvalidServiceIds,
        Failed,
        Success,
    }

    public readonly record struct SaveSelectionsResult(SaveSelectionsStatus Status)
    {
        public static SaveSelectionsResult Unauthorized() => new(SaveSelectionsStatus.Unauthorized);

        public static SaveSelectionsResult InvalidServiceIds() => new(SaveSelectionsStatus.InvalidServiceIds);

        public static SaveSelectionsResult Failed() => new(SaveSelectionsStatus.Failed);

        public static SaveSelectionsResult Success() => new(SaveSelectionsStatus.Success);
    }

    public enum GetSelectionsStatus
    {
        Unauthorized,
        Failed,
        Success,
    }

    public readonly record struct GetSelectionsResult(
        GetSelectionsStatus Status, List<ServiceSelectionDto>? Services = null, List<CityDto>? Cities = null)
    {
        public static GetSelectionsResult Unauthorized() => new(GetSelectionsStatus.Unauthorized);

        public static GetSelectionsResult Failed() => new(GetSelectionsStatus.Failed);

        public static GetSelectionsResult Success(List<ServiceSelectionDto> services, List<CityDto> cities) =>
            new(GetSelectionsStatus.Success, services, cities);
    }

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
