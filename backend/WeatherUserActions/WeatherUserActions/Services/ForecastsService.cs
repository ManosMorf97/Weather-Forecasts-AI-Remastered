using WeatherUserActions.AppwriteServices;
using WeatherUserActions.Repositories;
using WeatherUserActions.Services.Results;

namespace WeatherUserActions.Services
{
    public class ForecastsService : IForecastsService
    {
        private readonly IAppwriteAuthService _appwriteAuthService;
        private readonly IForecastsRepository _forecastsRepository;
        private readonly ILogger<ForecastsService> _logger;

        public ForecastsService(
            IAppwriteAuthService appwriteAuthService, IForecastsRepository forecastsRepository, ILogger<ForecastsService> logger)
        {
            _appwriteAuthService = appwriteAuthService;
            _forecastsRepository = forecastsRepository;
            _logger = logger;
        }

        public async Task<GetForecastsResult> GetForecastsAsync(string jwt, CancellationToken cancellationToken = default)
        {
            string userId;
            try
            {
                userId = await _appwriteAuthService.VerifyJwtAsync(jwt, cancellationToken);
            }
            catch (AppwriteTokenVerificationException ex)
            {
                _logger.LogWarning(ex, "Appwrite JWT verification failed");
                return GetForecastsResult.Unauthorized();
            }

            var (succeeded, forecasts) = await _forecastsRepository.TryGetUserForecastsAsync(userId, cancellationToken);
            return succeeded ? GetForecastsResult.Success(forecasts) : GetForecastsResult.Failed();
        }
    }
}
