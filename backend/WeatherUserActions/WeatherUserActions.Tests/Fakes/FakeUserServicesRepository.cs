using WeatherUserActions.Repositories;

namespace WeatherUserActions.Tests.Fakes
{
    public class FakeUserServicesRepository : IUserServicesRepository
    {
        private readonly bool _validationSucceeds;
        private readonly bool _allServiceIdsExist;
        private readonly bool _replaceSucceeds;

        private FakeUserServicesRepository(bool validationSucceeds, bool allServiceIdsExist, bool replaceSucceeds)
        {
            _validationSucceeds = validationSucceeds;
            _allServiceIdsExist = allServiceIdsExist;
            _replaceSucceeds = replaceSucceeds;
        }

        public static FakeUserServicesRepository Succeeding() =>
            new(validationSucceeds: true, allServiceIdsExist: true, replaceSucceeds: true);

        public static FakeUserServicesRepository WithInvalidServiceIds() =>
            new(validationSucceeds: true, allServiceIdsExist: false, replaceSucceeds: true);

        public static FakeUserServicesRepository FailingToValidateServiceIds() =>
            new(validationSucceeds: false, allServiceIdsExist: false, replaceSucceeds: true);

        public static FakeUserServicesRepository FailingToReplaceServices() =>
            new(validationSucceeds: true, allServiceIdsExist: true, replaceSucceeds: false);

        public Task<(bool Succeeded, bool AllExist)> TryValidateServiceIdsAsync(
            IReadOnlyCollection<int> serviceIds, CancellationToken cancellationToken = default) =>
            Task.FromResult((_validationSucceeds, _allServiceIdsExist));

        public Task<bool> ReplaceUserServicesAsync(
            string userId, IReadOnlyCollection<int> serviceIds, CancellationToken cancellationToken = default) =>
            Task.FromResult(_replaceSucceeds);
    }
}
