namespace WeatherUserActions.Services
{
    public interface IForecastsService
    {
        // Verifies the ID token, then returns every current/upcoming forecast for the user's
        // saved cities and selected services (UC6 main flow), ordered by Service name, City
        // name, then Timestamp.
        Task<GetForecastsResult> GetForecastsAsync(string idToken, CancellationToken cancellationToken = default);
    }
}
