using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using WeatherUserActions.Data;
using WeatherUserActions.Models;
using WeatherUserActions.Repositories;
using WeatherUserActions.Tests.Fixtures;
using Xunit;

namespace WeatherUserActions.Tests
{
    // Exercises UserServicesRepository's DB-failure handling directly against the real
    // Testcontainers SQL Server - no mocking of EF Core or ADO.NET exception types.
    [Collection(TestCollections.SqlServer)]
    public class UserServicesRepositoryTests : IAsyncLifetime
    {
        private readonly SqlServerFixture _fixture;

        public UserServicesRepositoryTests(SqlServerFixture fixture)
        {
            _fixture = fixture;
        }

        public Task InitializeAsync() => _fixture.ResetDatabaseAsync();

        public Task DisposeAsync() => Task.CompletedTask;

        [Fact]
        public async Task ReplaceUserServicesAsync_UnknownServiceId_ReturnsFalse()
        {
            var uid = await SeedUserAsync();

            // No ForecastingService row for this id, so the FK constraint rejects the insert
            // with no chance of a "someone else already inserted it" recovery.
            await using var db = _fixture.CreateDbContext();
            var repository = new UserServicesRepository(db, NullLogger<UserServicesRepository>.Instance);
            //CLAUDE. Where it will hit exception
            var succeeded = await repository.ReplaceUserServicesAsync(uid, [12345]);

            Assert.False(succeeded);
        }

        [Fact]
        public async Task TryValidateServiceIdsAsync_DatabaseUnreachable_ReturnsFailure()
        {
            await using var brokenDb = CreateUnreachableDbContext();
            var repository = new UserServicesRepository(brokenDb, NullLogger<UserServicesRepository>.Instance);

            var (succeeded, allExist) = await repository.TryValidateServiceIdsAsync([1, 2]);

            Assert.False(succeeded);
            Assert.False(allExist);
        }

        private static WeatherUserActionsDbContext CreateUnreachableDbContext()
        {
            var options = new DbContextOptionsBuilder<WeatherUserActionsDbContext>()
                .UseSqlServer("Server=127.0.0.1,1;Database=doesnotexist;User Id=sa;Password=wrong;Connect Timeout=1;TrustServerCertificate=true")
                .Options;

            return new WeatherUserActionsDbContext(options);
        }

        private async Task<string> SeedUserAsync()
        {
            var uid = $"uid-{Guid.NewGuid():N}";
            await using var db = _fixture.CreateDbContext();
            db.Users.Add(new User { UserId = uid, CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
            return uid;
        }
    }
}
