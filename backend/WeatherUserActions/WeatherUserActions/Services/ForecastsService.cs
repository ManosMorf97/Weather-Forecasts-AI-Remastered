using WeatherUserActions.FirebaseServices;
using WeatherUserActions.Repositories;

namespace WeatherUserActions.Services
{
    public class ForecastsService : IForecastsService
    {
        private readonly IFirebaseAuthService _firebaseAuthService;
        private readonly IForecastsRepository _forecastsRepository;
        private readonly ILogger<ForecastsService> _logger;

        public ForecastsService(
            IFirebaseAuthService firebaseAuthService, IForecastsRepository forecastsRepository, ILogger<ForecastsService> logger)
        {
            _firebaseAuthService = firebaseAuthService;
            _forecastsRepository = forecastsRepository;
            _logger = logger;
        }

        public async Task<GetForecastsResult> GetForecastsAsync(string idToken, CancellationToken cancellationToken = default)
        {
            string userId;
            try
            {
                userId = await _firebaseAuthService.VerifyIdTokenAsync(idToken, cancellationToken);
            }
            catch (FirebaseTokenVerificationException ex)
            {
                _logger.LogWarning(ex, "Firebase ID token verification failed");
                return GetForecastsResult.Unauthorized();
            }

            var (succeeded, forecasts) = await _forecastsRepository.TryGetUserForecastsAsync(userId, cancellationToken);
            return succeeded ? GetForecastsResult.Success(forecasts) : GetForecastsResult.Failed();
        }
    }
}
