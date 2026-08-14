using WeatherUserActions.Dtos;

namespace WeatherUserActions.Services
{
    public enum GetForecastsStatus
    {
        Unauthorized,
        Failed,
        Success,
    }

    public readonly record struct GetForecastsResult(GetForecastsStatus Status, List<ForecastItemDto>? Forecasts = null)
    {
        public static GetForecastsResult Unauthorized() => new(GetForecastsStatus.Unauthorized);

        public static GetForecastsResult Failed() => new(GetForecastsStatus.Failed);

        public static GetForecastsResult Success(List<ForecastItemDto> forecasts) =>
            new(GetForecastsStatus.Success, forecasts);
    }
}
