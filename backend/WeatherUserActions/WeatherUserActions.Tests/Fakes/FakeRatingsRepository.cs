using WeatherUserActions.Repositories;

namespace WeatherUserActions.Tests.Fakes
{
    public class FakeRatingsRepository : IRatingsRepository
    {
        private readonly bool _upsertSucceeds;
        private readonly bool _forecastExists;
        private readonly bool _removeSucceeds;

        private FakeRatingsRepository(bool upsertSucceeds, bool forecastExists, bool removeSucceeds)
        {
            _upsertSucceeds = upsertSucceeds;
            _forecastExists = forecastExists;
            _removeSucceeds = removeSucceeds;
        }

        public static FakeRatingsRepository Succeeding() =>
            new(upsertSucceeds: true, forecastExists: true, removeSucceeds: true);

        public static FakeRatingsRepository WithMissingForecast() =>
            new(upsertSucceeds: true, forecastExists: false, removeSucceeds: true);

        public static FakeRatingsRepository FailingToUpsert() =>
            new(upsertSucceeds: false, forecastExists: true, removeSucceeds: true);

        public static FakeRatingsRepository FailingToRemove() =>
            new(upsertSucceeds: true, forecastExists: true, removeSucceeds: false);

        public Task<(bool Succeeded, bool ForecastExists)> UpsertRatingAsync(
            string userId, int forecastId, int value, CancellationToken cancellationToken = default) =>
            Task.FromResult((_upsertSucceeds, _forecastExists));

        public Task<bool> RemoveRatingAsync(string userId, int forecastId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_removeSucceeds);
    }
}
