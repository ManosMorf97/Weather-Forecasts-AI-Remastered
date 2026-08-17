namespace WeatherUserActions.Dtos
{
    public record AggregatedForecastItemDto(
        int ForecastId,
        string City,
        string Country,
        string Service,
        string Type,
        DateTime Timestamp,
        decimal Temperature,
        decimal Humidity,
        decimal WindSpeed,
        bool DangerFlag);

    // Explains why "Service" won the aggregation for this city (UC12 steps 3-6).
    public record ServiceAggregationMetadataDto(
        string City,
        string Service,
        decimal? AverageRating,
        int RatingCount,
        bool AggregationApplicable,
        bool IsTie,
        bool IsUnratedSelection);

    // UC10 (main flow) / UC12: for every one of the user's selected cities, every current/upcoming
    // forecast (Timestamp >= now) from the highest-ranked selected service that actually has one -
    // ranked by all-time average rating across all users (ties and no-rating cases resolved
    // alphabetically - see UC12 A1/A2), falling back to the next-ranked service if a higher-ranked
    // one has no upcoming forecast - plus per-city metadata about the winning service. Cities where
    // no selected service has any forecast data at all, or none has an upcoming forecast, are
    // omitted (UC12 E1).
    public record GetAggregatedForecastsResponse(
        List<AggregatedForecastItemDto> Forecasts,
        List<ServiceAggregationMetadataDto> ServiceMetadata);
}
