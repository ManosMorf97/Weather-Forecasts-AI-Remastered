namespace WeatherUserActions.Dtos
{
    public record ForecastItemDto(
        int ForecastId,
        string City,
        string Country,
        string Service,
        string Type,
        DateTime Timestamp,
        decimal Temperature,
        decimal Humidity,
        decimal WindSpeed,
        bool DangerFlag,
        int? UserRating);

    // UC6 (main flow): every current/upcoming forecast (Timestamp >= now) for the user's saved
    // cities and selected services, ordered by Service name, City name, then Timestamp.
    public record GetForecastsResponse(List<ForecastItemDto> Forecasts);
}
