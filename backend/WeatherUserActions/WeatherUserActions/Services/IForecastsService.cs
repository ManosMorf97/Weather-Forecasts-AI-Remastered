using WeatherUserActions.Services.Results;

namespace WeatherUserActions.Services
{
    public interface IForecastsService
    {
        // Verifies the JWT, then returns every current/upcoming forecast for the user's
        // saved cities and selected services (UC6 main flow), ordered by Service name, City
        // name, then Timestamp.
        Task<GetForecastsResult> GetForecastsAsync(string jwt, CancellationToken cancellationToken = default);
    }
}
