using WeatherUserActions.AppwriteServices;
using WeatherUserActions.Repositories;
using WeatherUserActions.Services.Results;

namespace WeatherUserActions.Services
{
    public class UserServicesService : IUserServicesService
    {
        private readonly IAppwriteAuthService _appwriteAuthService;
        private readonly IUserServicesRepository _userServicesRepository;
        private readonly ILogger<UserServicesService> _logger;

        public UserServicesService(
            IAppwriteAuthService appwriteAuthService, IUserServicesRepository userServicesRepository, ILogger<UserServicesService> logger)
        {
            _appwriteAuthService = appwriteAuthService;
            _userServicesRepository = userServicesRepository;
            _logger = logger;
        }

        public async Task<SaveServicesResult> SaveServicesAsync(
            string jwt, IReadOnlyCollection<int> serviceIds, CancellationToken cancellationToken = default)
        {
            string userId;
            try
            {
                userId = await _appwriteAuthService.VerifyJwtAsync(jwt, cancellationToken);
            }
            catch (AppwriteTokenVerificationException ex)
            {
                _logger.LogWarning(ex, "Appwrite JWT verification failed");
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
