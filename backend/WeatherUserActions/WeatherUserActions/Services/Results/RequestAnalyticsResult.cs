namespace WeatherUserActions.Services.Results
{
    public enum RequestAnalyticsStatus
    {
        Unauthorized,
        InvalidCities,
        InvalidServices,
        InvalidDateRange,
        Failed,
        Accepted,
    }

    public readonly record struct RequestAnalyticsResult(RequestAnalyticsStatus Status, Guid BatchId = default)
    {
        public static RequestAnalyticsResult Unauthorized() => new(RequestAnalyticsStatus.Unauthorized);

        public static RequestAnalyticsResult InvalidCities() => new(RequestAnalyticsStatus.InvalidCities);

        public static RequestAnalyticsResult InvalidServices() => new(RequestAnalyticsStatus.InvalidServices);

        public static RequestAnalyticsResult InvalidDateRange() => new(RequestAnalyticsStatus.InvalidDateRange);

        public static RequestAnalyticsResult Failed() => new(RequestAnalyticsStatus.Failed);

        public static RequestAnalyticsResult Accepted(Guid batchId) => new(RequestAnalyticsStatus.Accepted, batchId);
    }
}
