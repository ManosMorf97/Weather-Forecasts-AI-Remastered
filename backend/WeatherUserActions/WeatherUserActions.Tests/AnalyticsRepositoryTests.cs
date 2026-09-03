using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using WeatherUserActions.Analytics;
using WeatherUserActions.Data;
using WeatherUserActions.Models;
using WeatherUserActions.Repositories;
using WeatherUserActions.Tests.Fixtures;
using Xunit;

namespace WeatherUserActions.Tests
{
    // Exercises AnalyticsRepository's DB-failure handling directly against the real Testcontainers
    // SQL Server. The happy-path enqueue/compute logic is covered via AnalyticsControllerLogicTests.
    [Collection(TestCollections.SqlServer)]
    public class AnalyticsRepositoryTests : IAsyncLifetime
    {
        private readonly SqlServerFixture _fixture;

        public AnalyticsRepositoryTests(SqlServerFixture fixture)
        {
            _fixture = fixture;
        }

        public Task InitializeAsync() => _fixture.ResetDatabaseAsync();

        public Task DisposeAsync() => Task.CompletedTask;

        [Fact]
        public async Task TryGetUserSelectionScopeAsync_DatabaseUnreachable_ReturnsFailure()
        {
            await using var brokenDb = CreateUnreachableDbContext();
            var repository = new AnalyticsRepository(brokenDb, NullLogger<AnalyticsRepository>.Instance);

            var (succeeded, cityIds, serviceIds) = await repository.TryGetUserSelectionScopeAsync("uid-1");

            Assert.False(succeeded);
            Assert.Empty(cityIds);
            Assert.Empty(serviceIds);
        }

        [Fact]
        public async Task TryGetQueuedReportsAsync_DatabaseUnreachable_ReturnsFailure()
        {
            await using var brokenDb = CreateUnreachableDbContext();
            var repository = new AnalyticsRepository(brokenDb, NullLogger<AnalyticsRepository>.Instance);

            var (succeeded, reports) = await repository.TryGetQueuedReportsAsync();

            Assert.False(succeeded);
            Assert.Empty(reports);
        }

        [Fact]
        public async Task TryEnqueueReportsAsync_DatabaseUnreachable_ReturnsFalse()
        {
            await using var brokenDb = CreateUnreachableDbContext();
            var repository = new AnalyticsRepository(brokenDb, NullLogger<AnalyticsRepository>.Instance);

            var enqueued = await repository.TryEnqueueReportsAsync(
                "uid-1", Guid.NewGuid(), [1], [1], new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31));

            Assert.False(enqueued);
        }

        [Fact]
        public async Task TryGetForecastSamplesAsync_DatabaseUnreachable_ReturnsFailure()
        {
            await using var brokenDb = CreateUnreachableDbContext();
            var repository = new AnalyticsRepository(brokenDb, NullLogger<AnalyticsRepository>.Instance);

            var (succeeded, samples) = await repository.TryGetForecastSamplesAsync(
                1, [1], new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31));

            Assert.False(succeeded);
            Assert.Empty(samples);
        }

        [Fact]
        public async Task TrySaveReportResultAsync_DatabaseUnreachable_ReturnsFalse()
        {
            await using var brokenDb = CreateUnreachableDbContext();
            var repository = new AnalyticsRepository(brokenDb, NullLogger<AnalyticsRepository>.Instance);

            var saved = await repository.TrySaveReportResultAsync(1, [], AnalyticsReportStatus.Completed);

            Assert.False(saved);
        }

        [Fact]
        public async Task TryGetUndeliveredBatchesAsync_DatabaseUnreachable_ReturnsFailure()
        {
            await using var brokenDb = CreateUnreachableDbContext();
            var repository = new AnalyticsRepository(brokenDb, NullLogger<AnalyticsRepository>.Instance);

            var (succeeded, batches) = await repository.TryGetUndeliveredBatchesAsync();

            Assert.False(succeeded);
            Assert.Empty(batches);
        }

        [Fact]
        public async Task TryMarkBatchDeliveredAsync_DatabaseUnreachable_ReturnsFalse()
        {
            await using var brokenDb = CreateUnreachableDbContext();
            var repository = new AnalyticsRepository(brokenDb, NullLogger<AnalyticsRepository>.Instance);

            var marked = await repository.TryMarkBatchDeliveredAsync(Guid.NewGuid());

            Assert.False(marked);
        }

        private static WeatherUserActionsDbContext CreateUnreachableDbContext()
        {
            var options = new DbContextOptionsBuilder<WeatherUserActionsDbContext>()
                .UseSqlServer("Server=127.0.0.1,1;Database=doesnotexist;User Id=sa;Password=wrong;Connect Timeout=1;TrustServerCertificate=true")
                .Options;

            return new WeatherUserActionsDbContext(options);
        }
    }
}
