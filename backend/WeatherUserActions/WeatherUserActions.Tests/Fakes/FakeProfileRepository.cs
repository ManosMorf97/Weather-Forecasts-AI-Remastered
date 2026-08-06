using WeatherUserActions.Repositories;

namespace WeatherUserActions.Tests.Fakes
{
    public class FakeProfileRepository : IProfileRepository
    {
        private readonly bool _provisioningSucceeds;
        private readonly bool _selectionCheckSucceeds;
        private readonly bool _hasCitySiteSelection;

        private FakeProfileRepository(bool provisioningSucceeds, bool selectionCheckSucceeds, bool hasCitySiteSelection)
        {
            _provisioningSucceeds = provisioningSucceeds;
            _selectionCheckSucceeds = selectionCheckSucceeds;
            _hasCitySiteSelection = hasCitySiteSelection;
        }

        public static FakeProfileRepository ReturningSelection(bool hasCitySiteSelection) =>
            new(provisioningSucceeds: true, selectionCheckSucceeds: true, hasCitySiteSelection);

        public static FakeProfileRepository FailingToProvision() =>
            new(provisioningSucceeds: false, selectionCheckSucceeds: true, hasCitySiteSelection: false);

        public static FakeProfileRepository FailingToCheckSelection() =>
            new(provisioningSucceeds: true, selectionCheckSucceeds: false, hasCitySiteSelection: false);

        public Task<bool> EnsureUserProvisionedAsync(string userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_provisioningSucceeds);

        public Task<(bool Succeeded, bool HasCitySiteSelection)> TryGetHasCitySiteSelectionAsync(
            string userId, CancellationToken cancellationToken = default) =>
            Task.FromResult((_selectionCheckSucceeds, _hasCitySiteSelection));
    }
}
