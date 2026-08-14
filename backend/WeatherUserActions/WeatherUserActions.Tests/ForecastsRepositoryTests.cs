using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using WeatherUserActions.Data;
using WeatherUserActions.Repositories;
using WeatherUserActions.Tests.Fixtures;
using Xunit;

namespace WeatherUserActions.Tests
{
    // Exercises ForecastsRepository's DB-failure handling directly against the real
    // Testcontainers SQL Server - no mocking of EF Core or ADO.NET exception types.
    [Collection(TestCollections.SqlServer)]
    public class ForecastsRepositoryTests : IAsyncLifetime
    {
        private readonly SqlServerFixture _fixture;

        public ForecastsRepositoryTests(SqlServerFixture fixture)
        {
            _fixture = fixture;
        }

        public Task InitializeAsync() => _fixture.ResetDatabaseAsync();

        public Task DisposeAsync() => Task.CompletedTask;

        [Fact]
        public async Task TryGetUserForecastsAsync_DatabaseUnreachable_ReturnsFailure()
        {
            await using var brokenDb = CreateUnreachableDbContext();
            var repository = new ForecastsRepository(brokenDb, NullLogger<ForecastsRepository>.Instance);

            var (succeeded, forecasts) = await repository.TryGetUserForecastsAsync("uid-1");

            Assert.False(succeeded);
            Assert.Empty(forecasts);
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
