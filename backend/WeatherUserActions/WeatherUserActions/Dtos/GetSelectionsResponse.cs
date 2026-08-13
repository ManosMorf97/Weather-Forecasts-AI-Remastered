namespace WeatherUserActions.Dtos
{
    public record ServiceSelectionDto(int ServiceId, string Name, bool Selected);

    // UC3: the user's current selections - every forecasting service flagged with whether the
    // user has selected it (via a real city selection or a pending no-city-yet pick), plus the
    // cities from the user's real selections.
    public record GetSelectionsResponse(List<ServiceSelectionDto> Services, List<CityDto> Cities);
}
