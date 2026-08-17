using WeatherUserActions.Dtos;

namespace WeatherUserActions.Services.Results
{
    public enum GetAggregatedForecastsStatus
    {
        Unauthorized,
        Failed,
        Success,
    }

    public readonly record struct GetAggregatedForecastsResult(
        GetAggregatedForecastsStatus Status,
        List<AggregatedForecastItemDto>? Forecasts = null,
        List<ServiceAggregationMetadataDto>? ServiceMetadata = null)
    {
        public static GetAggregatedForecastsResult Unauthorized() => new(GetAggregatedForecastsStatus.Unauthorized);

        public static GetAggregatedForecastsResult Failed() => new(GetAggregatedForecastsStatus.Failed);

        public static GetAggregatedForecastsResult Success(
            List<AggregatedForecastItemDto> forecasts, List<ServiceAggregationMetadataDto> serviceMetadata) =>
            new(GetAggregatedForecastsStatus.Success, forecasts, serviceMetadata);
    }
}
