using WeatherUserActions.Dtos;
using WeatherUserActions.Repositories;

namespace WeatherUserActions.Tests.Fakes
{
    public class FakeSelectionsRepository : ISelectionsRepository
    {
        private readonly bool _validationSucceeds;
        private readonly bool _allServiceIdsExist;
        private readonly bool _replaceSucceeds;
        private readonly bool _getSelectionsSucceeds;
        private readonly List<ServiceSelectionDto> _services;
        private readonly List<SelectedCityDto> _cities;

        private FakeSelectionsRepository(
            bool validationSucceeds,
            bool allServiceIdsExist,
            bool replaceSucceeds,
            bool getSelectionsSucceeds = true,
            List<ServiceSelectionDto>? services = null,
            List<SelectedCityDto>? cities = null)
        {
            _validationSucceeds = validationSucceeds;
            _allServiceIdsExist = allServiceIdsExist;
            _replaceSucceeds = replaceSucceeds;
            _getSelectionsSucceeds = getSelectionsSucceeds;
            _services = services ?? [];
            _cities = cities ?? [];
        }

        public static FakeSelectionsRepository Succeeding() =>
            new(validationSucceeds: true, allServiceIdsExist: true, replaceSucceeds: true);

        public static FakeSelectionsRepository WithInvalidServiceIds() =>
            new(validationSucceeds: true, allServiceIdsExist: false, replaceSucceeds: true);

        public static FakeSelectionsRepository FailingToValidateServiceIds() =>
            new(validationSucceeds: false, allServiceIdsExist: false, replaceSucceeds: true);

        public static FakeSelectionsRepository FailingToReplaceSelection() =>
            new(validationSucceeds: true, allServiceIdsExist: true, replaceSucceeds: false);

        public static FakeSelectionsRepository ReturningSelections(List<ServiceSelectionDto> services, List<SelectedCityDto> cities) =>
            new(validationSucceeds: true, allServiceIdsExist: true, replaceSucceeds: true, services: services, cities: cities);

        public static FakeSelectionsRepository FailingToLoadSelections() =>
            new(validationSucceeds: true, allServiceIdsExist: true, replaceSucceeds: true, getSelectionsSucceeds: false);

        public Task<(bool Succeeded, bool AllExist)> TryValidateServiceIdsAsync(
            IReadOnlyCollection<int> serviceIds, CancellationToken cancellationToken = default) =>
            Task.FromResult((_validationSucceeds, _allServiceIdsExist));

        public Task<bool> ReplaceUserSelectionAsync(
            string userId,
            IReadOnlyCollection<CityDto> cities,
            IReadOnlyCollection<int> serviceIds,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_replaceSucceeds);

        public Task<(bool Succeeded, List<ServiceSelectionDto> Services, List<SelectedCityDto> Cities)> TryGetUserSelectionsAsync(
            string userId, CancellationToken cancellationToken = default) =>
            Task.FromResult((_getSelectionsSucceeds, _services, _cities));
    }
}
