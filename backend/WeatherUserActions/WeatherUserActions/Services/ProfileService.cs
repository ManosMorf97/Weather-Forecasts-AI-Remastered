using WeatherUserActions.AppwriteServices;
using WeatherUserActions.Repositories;
using WeatherUserActions.Services.Results;

namespace WeatherUserActions.Services
{
    public class ProfileService : IProfileService
    {
        private readonly IAppwriteAuthService _appwriteAuthService;
        private readonly IProfileRepository _profileRepository;
        private readonly ILogger<ProfileService> _logger;

        public ProfileService(
            IAppwriteAuthService appwriteAuthService, IProfileRepository profileRepository, ILogger<ProfileService> logger)
        {
            _appwriteAuthService = appwriteAuthService;
            _profileRepository = profileRepository;
            _logger = logger;
        }

        public async Task<ProfileCreationResult> CreateProfileAsync(string jwt, CancellationToken cancellationToken = default)
        {
            string userId;
            try
            {
                userId = await _appwriteAuthService.VerifyJwtAsync(jwt, cancellationToken);
            }
            catch (AppwriteTokenVerificationException ex)
            {
                _logger.LogWarning(ex, "Appwrite JWT verification failed");
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
