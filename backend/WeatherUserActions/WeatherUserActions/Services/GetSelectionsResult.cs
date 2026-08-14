using WeatherUserActions.Dtos;

namespace WeatherUserActions.Services
{
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
}
