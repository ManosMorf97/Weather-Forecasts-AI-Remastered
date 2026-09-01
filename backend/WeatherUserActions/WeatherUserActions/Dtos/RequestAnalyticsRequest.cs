namespace WeatherUserActions.Dtos
{
    // UC8: the user picks a subset of their own selected cities and services, plus a date range.
    // All four metrics (temperature, humidity, wind speed, danger days) are always computed.
    // An empty or unknown city/service list is rejected by AnalyticsService (InvalidCities /
    // InvalidServices), so it is not constrained here - that keeps every 400 in one shape.
    public record RequestAnalyticsRequest(
        List<int> CityIds,
        List<int> ServiceIds,
        DateOnly DateRangeStart,
        DateOnly DateRangeEnd);
}
