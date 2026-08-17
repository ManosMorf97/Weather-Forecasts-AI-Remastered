using WeatherUserActions.FirebaseServices;
using WeatherUserActions.Repositories;
using WeatherUserActions.Services.Results;

namespace WeatherUserActions.Services
{
    public class AggregatedForecastsService : IAggregatedForecastsService
    {
        private readonly IFirebaseAuthService _firebaseAuthService;
        private readonly IAggregatedForecastsRepository _aggregatedForecastsRepository;
        private readonly ILogger<AggregatedForecastsService> _logger;

        public AggregatedForecastsService(
            IFirebaseAuthService firebaseAuthService,
            IAggregatedForecastsRepository aggregatedForecastsRepository,
            ILogger<AggregatedForecastsService> logger)
        {
            _firebaseAuthService = firebaseAuthService;
            _aggregatedForecastsRepository = aggregatedForecastsRepository;
            _logger = logger;
        }

        public async Task<GetAggregatedForecastsResult> GetAggregatedForecastsAsync(
            string idToken, CancellationToken cancellationToken = default)
        {
            string userId;
            try
            {
                userId = await _firebaseAuthService.VerifyIdTokenAsync(idToken, cancellationToken);
            }
            catch (FirebaseTokenVerificationException ex)
            {
                _logger.LogWarning(ex, "Firebase ID token verification failed");
                return GetAggregatedForecastsResult.Unauthorized();
            }

            var (succeeded, forecasts, serviceMetadata) =
                await _aggregatedForecastsRepository.TryGetAggregatedForecastsAsync(userId, cancellationToken);

            return succeeded
                ? GetAggregatedForecastsResult.Success(forecasts, serviceMetadata)
                : GetAggregatedForecastsResult.Failed();
        }
    }
}
