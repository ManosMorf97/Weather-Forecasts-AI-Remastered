using WeatherUserActions.AppwriteServices;
using WeatherUserActions.Repositories;
using WeatherUserActions.Services.Results;

namespace WeatherUserActions.Services
{
    public class RatingsService : IRatingsService
    {
        private readonly IAppwriteAuthService _appwriteAuthService;
        private readonly IRatingsRepository _ratingsRepository;
        private readonly ILogger<RatingsService> _logger;

        public RatingsService(
            IAppwriteAuthService appwriteAuthService, IRatingsRepository ratingsRepository, ILogger<RatingsService> logger)
        {
            _appwriteAuthService = appwriteAuthService;
            _ratingsRepository = ratingsRepository;
            _logger = logger;
        }

        public async Task<RateForecastResult> RateForecastAsync(
            string jwt, int forecastId, int value, CancellationToken cancellationToken = default)
        {
            string userId;
            try
            {
                userId = await _appwriteAuthService.VerifyJwtAsync(jwt, cancellationToken);
            }
            catch (AppwriteTokenVerificationException ex)
            {
                _logger.LogWarning(ex, "Appwrite JWT verification failed");
                return RateForecastResult.Unauthorized();
            }

            var (succeeded, forecastExists) = await _ratingsRepository.UpsertRatingAsync(userId, forecastId, value, cancellationToken);
            if (!succeeded)
            {
                return RateForecastResult.Failed();
            }

            return forecastExists ? RateForecastResult.Success() : RateForecastResult.ForecastNotFound();
        }

        public async Task<RemoveRatingResult> RemoveRatingAsync(
            string jwt, int forecastId, CancellationToken cancellationToken = default)
        {
            string userId;
            try
            {
                userId = await _appwriteAuthService.VerifyJwtAsync(jwt, cancellationToken);
            }
            catch (AppwriteTokenVerificationException ex)
            {
                _logger.LogWarning(ex, "Appwrite JWT verification failed");
                return RemoveRatingResult.Unauthorized();
            }

            var succeeded = await _ratingsRepository.RemoveRatingAsync(userId, forecastId, cancellationToken);
            return succeeded ? RemoveRatingResult.Success() : RemoveRatingResult.Failed();
        }
    }
}
