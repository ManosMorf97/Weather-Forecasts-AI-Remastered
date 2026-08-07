using WeatherUserActions.Dtos;
using WeatherUserActions.Repositories;

namespace WeatherUserActions.Tests.Fakes
{
    public class FakeSelectionsRepository : ISelectionsRepository
    {
        private readonly bool _validationSucceeds;
        private readonly bool _allServiceIdsExist;
        private readonly bool _replaceSucceeds;

        private FakeSelectionsRepository(bool validationSucceeds, bool allServiceIdsExist, bool replaceSucceeds)
        {
            _validationSucceeds = validationSucceeds;
            _allServiceIdsExist = allServiceIdsExist;
            _replaceSucceeds = replaceSucceeds;
        }

        public static FakeSelectionsRepository Succeeding() =>
            new(validationSucceeds: true, allServiceIdsExist: true, replaceSucceeds: true);

        public static FakeSelectionsRepository WithInvalidServiceIds() =>
            new(validationSucceeds: true, allServiceIdsExist: false, replaceSucceeds: true);

        public static FakeSelectionsRepository FailingToValidateServiceIds() =>
            new(validationSucceeds: false, allServiceIdsExist: false, replaceSucceeds: true);

        public static FakeSelectionsRepository FailingToReplaceSelection() =>
            new(validationSucceeds: true, allServiceIdsExist: true, replaceSucceeds: false);

        public Task<(bool Succeeded, bool AllExist)> TryValidateServiceIdsAsync(
            IReadOnlyCollection<int> serviceIds, CancellationToken cancellationToken = default) =>
            Task.FromResult((_validationSucceeds, _allServiceIdsExist));

        public Task<bool> ReplaceUserSelectionAsync(
            string userId,
            IReadOnlyCollection<CityDto> cities,
            IReadOnlyCollection<int> serviceIds,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_replaceSucceeds);
    }
}
