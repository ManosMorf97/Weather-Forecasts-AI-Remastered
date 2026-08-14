using WeatherUserActions.FirebaseServices;
using WeatherUserActions.Repositories;
using WeatherUserActions.Services.Results;

namespace WeatherUserActions.Services
{
    public class UserServicesService : IUserServicesService
    {
        private readonly IFirebaseAuthService _firebaseAuthService;
        private readonly IUserServicesRepository _userServicesRepository;
        private readonly ILogger<UserServicesService> _logger;

        public UserServicesService(
            IFirebaseAuthService firebaseAuthService, IUserServicesRepository userServicesRepository, ILogger<UserServicesService> logger)
        {
            _firebaseAuthService = firebaseAuthService;
            _userServicesRepository = userServicesRepository;
            _logger = logger;
        }

        public async Task<SaveServicesResult> SaveServicesAsync(
            string idToken, IReadOnlyCollection<int> serviceIds, CancellationToken cancellationToken = default)
        {
            string userId;
            try
            {
                userId = await _firebaseAuthService.VerifyIdTokenAsync(idToken, cancellationToken);
            }
            catch (FirebaseTokenVerificationException ex)
            {
                _logger.LogWarning(ex, "Firebase ID token verification failed");
                return SaveServicesResult.Unauthorized();
            }

            var (validated, allExist) = await _userServicesRepository.TryValidateServiceIdsAsync(serviceIds, cancellationToken);
            if (!validated)
            {
                return SaveServicesResult.Failed();
            }

            if (!allExist)
            {
                return SaveServicesResult.InvalidServiceIds();
            }

            var succeeded = await _userServicesRepository.ReplaceUserServicesAsync(userId, serviceIds, cancellationToken);
            return succeeded ? SaveServicesResult.Success() : SaveServicesResult.Failed();
        }
    }
}
