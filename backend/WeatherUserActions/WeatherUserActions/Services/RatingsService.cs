using WeatherUserActions.FirebaseServices;
using WeatherUserActions.Repositories;
using WeatherUserActions.Services.Results;

namespace WeatherUserActions.Services
{
    public class RatingsService : IRatingsService
    {
        private readonly IFirebaseAuthService _firebaseAuthService;
        private readonly IRatingsRepository _ratingsRepository;
        private readonly ILogger<RatingsService> _logger;

        public RatingsService(
            IFirebaseAuthService firebaseAuthService, IRatingsRepository ratingsRepository, ILogger<RatingsService> logger)
        {
            _firebaseAuthService = firebaseAuthService;
            _ratingsRepository = ratingsRepository;
            _logger = logger;
        }

        public async Task<RateForecastResult> RateForecastAsync(
            string idToken, int forecastId, int value, CancellationToken cancellationToken = default)
        {
            string userId;
            try
            {
                userId = await _firebaseAuthService.VerifyIdTokenAsync(idToken, cancellationToken);
            }
            catch (FirebaseTokenVerificationException ex)
            {
                _logger.LogWarning(ex, "Firebase ID token verification failed");
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
            string idToken, int forecastId, CancellationToken cancellationToken = default)
        {
            string userId;
            try
            {
                userId = await _firebaseAuthService.VerifyIdTokenAsync(idToken, cancellationToken);
            }
            catch (FirebaseTokenVerificationException ex)
            {
                _logger.LogWarning(ex, "Firebase ID token verification failed");
                return RemoveRatingResult.Unauthorized();
            }

            var succeeded = await _ratingsRepository.RemoveRatingAsync(userId, forecastId, cancellationToken);
            return succeeded ? RemoveRatingResult.Success() : RemoveRatingResult.Failed();
        }
    }
}
