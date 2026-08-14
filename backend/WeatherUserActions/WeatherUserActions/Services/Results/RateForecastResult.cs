namespace WeatherUserActions.Services.Results
{
    public enum RateForecastStatus
    {
        Unauthorized,
        ForecastNotFound,
        Failed,
        Success,
    }

    public readonly record struct RateForecastResult(RateForecastStatus Status)
    {
        public static RateForecastResult Unauthorized() => new(RateForecastStatus.Unauthorized);

        public static RateForecastResult ForecastNotFound() => new(RateForecastStatus.ForecastNotFound);

        public static RateForecastResult Failed() => new(RateForecastStatus.Failed);

        public static RateForecastResult Success() => new(RateForecastStatus.Success);
    }
}
