using WeatherUserActions.FirebaseServices;
using WeatherUserActions.Repositories;

namespace WeatherUserActions.Services
{
    public class ProfileService : IProfileService
    {
        private readonly IFirebaseAuthService _firebaseAuthService;
        private readonly IProfileRepository _profileRepository;
        private readonly ILogger<ProfileService> _logger;

        public ProfileService(
            IFirebaseAuthService firebaseAuthService, IProfileRepository profileRepository, ILogger<ProfileService> logger)
        {
            _firebaseAuthService = firebaseAuthService;
            _profileRepository = profileRepository;
            _logger = logger;
        }

        public async Task<ProfileCreationResult> CreateProfileAsync(string idToken, CancellationToken cancellationToken = default)
        {
            string userId;
            try
            {
                userId = await _firebaseAuthService.VerifyIdTokenAsync(idToken, cancellationToken);
            }
            catch (FirebaseTokenVerificationException ex)
            {
                _logger.LogWarning(ex, "Firebase ID token verification failed");
                return ProfileCreationResult.Unauthorized();
            }

            if (!await _profileRepository.EnsureUserProvisionedAsync(userId, cancellationToken))
            {
                return ProfileCreationResult.ProvisioningFailed();
            }

            var (succeeded, hasCitySiteSelection) =
                await _profileRepository.TryGetHasCitySiteSelectionAsync(userId, cancellationToken);

            return succeeded
                ? ProfileCreationResult.Success(hasCitySiteSelection)
                : ProfileCreationResult.SelectionCheckFailed();
        }
    }
}
