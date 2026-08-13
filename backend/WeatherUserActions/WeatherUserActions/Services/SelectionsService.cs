using WeatherUserActions.Dtos;
using WeatherUserActions.FirebaseServices;
using WeatherUserActions.Repositories;

namespace WeatherUserActions.Services
{
    public class SelectionsService : ISelectionsService
    {
        private readonly IFirebaseAuthService _firebaseAuthService;
        private readonly ISelectionsRepository _selectionsRepository;
        private readonly ILogger<SelectionsService> _logger;

        public SelectionsService(
            IFirebaseAuthService firebaseAuthService, ISelectionsRepository selectionsRepository, ILogger<SelectionsService> logger)
        {
            _firebaseAuthService = firebaseAuthService;
            _selectionsRepository = selectionsRepository;
            _logger = logger;
        }

        public async Task<SaveSelectionsResult> SaveSelectionsAsync(
            string idToken,
            IReadOnlyCollection<CityDto> cities,
            IReadOnlyCollection<int> serviceIds,
            CancellationToken cancellationToken = default)
        {
            string userId;
            try
            {
                userId = await _firebaseAuthService.VerifyIdTokenAsync(idToken, cancellationToken);
            }
            catch (FirebaseTokenVerificationException ex)
            {
                _logger.LogWarning(ex, "Firebase ID token verification failed");
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

        public async Task<GetSelectionsResult> GetSelectionsAsync(string idToken, CancellationToken cancellationToken = default)
        {
            string userId;
            try
            {
                userId = await _firebaseAuthService.VerifyIdTokenAsync(idToken, cancellationToken);
            }
            catch (FirebaseTokenVerificationException ex)
            {
                _logger.LogWarning(ex, "Firebase ID token verification failed");
                return GetSelectionsResult.Unauthorized();
            }

            var (succeeded, services, cities) = await _selectionsRepository.TryGetUserSelectionsAsync(userId, cancellationToken);
            return succeeded ? GetSelectionsResult.Success(services, cities) : GetSelectionsResult.Failed();
        }
    }
}
