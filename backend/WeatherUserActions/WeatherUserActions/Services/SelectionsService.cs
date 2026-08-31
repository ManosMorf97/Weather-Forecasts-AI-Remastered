using WeatherUserActions.AppwriteServices;
using WeatherUserActions.Dtos;
using WeatherUserActions.Repositories;
using WeatherUserActions.Services.Results;

namespace WeatherUserActions.Services
{
    public class SelectionsService : ISelectionsService
    {
        private readonly IAppwriteAuthService _appwriteAuthService;
        private readonly ISelectionsRepository _selectionsRepository;
        private readonly ILogger<SelectionsService> _logger;

        public SelectionsService(
            IAppwriteAuthService appwriteAuthService, ISelectionsRepository selectionsRepository, ILogger<SelectionsService> logger)
        {
            _appwriteAuthService = appwriteAuthService;
            _selectionsRepository = selectionsRepository;
            _logger = logger;
        }

        public async Task<SaveSelectionsResult> SaveSelectionsAsync(
            string jwt,
            IReadOnlyCollection<CityDto> cities,
            IReadOnlyCollection<int> serviceIds,
            CancellationToken cancellationToken = default)
        {
            string userId;
            try
            {
                userId = await _appwriteAuthService.VerifyJwtAsync(jwt, cancellationToken);
            }
            catch (AppwriteTokenVerificationException ex)
            {
                _logger.LogWarning(ex, "Appwrite JWT verification failed");
                return SaveSelectionsResult.Unauthorized();
            }

            var (validated, allExist) = await _selectionsRepository.TryValidateServiceIdsAsync(serviceIds, cancellationToken);
            if (!validated)
            {
                return SaveSelectionsResult.Failed();
            }

            if (!allExist)
            {
                return SaveSelectionsResult.InvalidServiceIds();
            }

            var succeeded = await _selectionsRepository.ReplaceUserSelectionAsync(userId, cities, serviceIds, cancellationToken);
            return succeeded ? SaveSelectionsResult.Success() : SaveSelectionsResult.Failed();
        }

        public async Task<GetSelectionsResult> GetSelectionsAsync(string jwt, CancellationToken cancellationToken = default)
        {
            string userId;
            try
            {
                userId = await _appwriteAuthService.VerifyJwtAsync(jwt, cancellationToken);
            }
            catch (AppwriteTokenVerificationException ex)
            {
                _logger.LogWarning(ex, "Appwrite JWT verification failed");
                return GetSelectionsResult.Unauthorized();
            }

            var (succeeded, services, cities) = await _selectionsRepository.TryGetUserSelectionsAsync(userId, cancellationToken);
            return succeeded ? GetSelectionsResult.Success(services, cities) : GetSelectionsResult.Failed();
        }
    }
}
