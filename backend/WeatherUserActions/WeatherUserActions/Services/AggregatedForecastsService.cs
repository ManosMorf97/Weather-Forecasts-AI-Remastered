using WeatherUserActions.AppwriteServices;
using WeatherUserActions.Repositories;
using WeatherUserActions.Services.Results;

namespace WeatherUserActions.Services
{
    public class AggregatedForecastsService : IAggregatedForecastsService
    {
        private readonly IAppwriteAuthService _appwriteAuthService;
        private readonly IAggregatedForecastsRepository _aggregatedForecastsRepository;
        private readonly ILogger<AggregatedForecastsService> _logger;

        public AggregatedForecastsService(
            IAppwriteAuthService appwriteAuthService,
            IAggregatedForecastsRepository aggregatedForecastsRepository,
            ILogger<AggregatedForecastsService> logger)
        {
            _appwriteAuthService = appwriteAuthService;
            _aggregatedForecastsRepository = aggregatedForecastsRepository;
            _logger = logger;
        }

        public async Task<GetAggregatedForecastsResult> GetAggregatedForecastsAsync(
            string jwt, CancellationToken cancellationToken = default)
        {
            string userId;
            try
            {
                userId = await _appwriteAuthService.VerifyJwtAsync(jwt, cancellationToken);
            }
            catch (AppwriteTokenVerificationException ex)
            {
                _logger.LogWarning(ex, "Appwrite JWT verification failed");
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
