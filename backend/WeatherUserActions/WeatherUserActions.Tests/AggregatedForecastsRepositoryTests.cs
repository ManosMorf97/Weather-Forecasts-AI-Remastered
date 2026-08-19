using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using WeatherUserActions.Data;
using WeatherUserActions.Repositories;
using WeatherUserActions.Tests.Fixtures;
using Xunit;

namespace WeatherUserActions.Tests
{
    // Exercises AggregatedForecastsRepository's DB-failure handling directly against the real
    // Testcontainers SQL Server - no mocking of EF Core or ADO.NET exception types. The happy-path
    // aggregation logic is covered end-to-end via AggregatedForecastsControllerLogicTests.
    [Collection(TestCollections.SqlServer)]
    public class AggregatedForecastsRepositoryTests : IAsyncLifetime
    {
        private readonly SqlServerFixture _fixture;

        public AggregatedForecastsRepositoryTests(SqlServerFixture fixture)
        {
            _fixture = fixture;
        }

        public Task InitializeAsync() => _fixture.ResetDatabaseAsync();

        public Task DisposeAsync() => Task.CompletedTask;

        [Fact]
        public async Task TryGetAggregatedForecastsAsync_DatabaseUnreachable_ReturnsFailure()
        {
            await using var brokenDb = CreateUnreachableDbContext();
            var repository = new AggregatedForecastsRepository(brokenDb, NullLogger<AggregatedForecastsRepository>.Instance);

            var (succeeded, forecasts, serviceMetadata) = await repository.TryGetAggregatedForecastsAsync("uid-1");

            Assert.False(succeeded);
            Assert.Empty(forecasts);
            Assert.Empty(serviceMetadata);
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
