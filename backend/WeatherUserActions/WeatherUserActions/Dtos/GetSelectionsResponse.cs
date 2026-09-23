namespace WeatherUserActions.Dtos
{
    public record ServiceSelectionDto(int ServiceId, string Name, bool Selected);

    // Same shape as CityDto, plus the CityId - UC8 (Request Analytics) needs it to build
    // RequestAnalyticsRequest.CityIds. Kept separate from CityDto so SaveSelectionsRequest
    // doesn't have to carry an id for newly-searched cities that don't have one yet.
    public record SelectedCityDto(int CityId, string Name, string Country, decimal Latitude, decimal Longitude);

    // UC3: the user's current selections - every forecasting service flagged with whether the
    // user has selected it (via a real city selection or a pending no-city-yet pick), plus the
    // cities from the user's real selections.
    public record GetSelectionsResponse(List<ServiceSelectionDto> Services, List<SelectedCityDto> Cities);
}
