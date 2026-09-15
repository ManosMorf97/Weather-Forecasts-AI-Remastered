using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using WeatherUserActions.Data;
using WeatherUserActions.Dtos;
using WeatherUserActions.Models;
using WeatherUserActions.Repositories;
using WeatherUserActions.Tests.Fixtures;
using Xunit;

namespace WeatherUserActions.Tests
{
    // Exercises SelectionsRepository's DB-failure handling directly against the real
    // Testcontainers SQL Server - no mocking of EF Core or ADO.NET exception types.
    [Collection(TestCollections.SqlServer)]
    public class SelectionsRepositoryTests : IAsyncLifetime
    {
        private readonly SqlServerFixture _fixture;

        public SelectionsRepositoryTests(SqlServerFixture fixture)
        {
            _fixture = fixture;
        }

        public Task InitializeAsync() => _fixture.ResetDatabaseAsync();

        public Task DisposeAsync() => Task.CompletedTask;

        [Fact]
        public async Task ReplaceUserSelectionAsync_ConcurrentCallsForSameNewCity_BothSucceedAndOnlyOneCityRowPersists()
        {
            var uidA = await SeedUserAsync();
            var uidB = await SeedUserAsync();
            var serviceId = await SeedServiceAsync();

            var athens = new CityDto("Athens", "Greece", 37.98m, 23.72m);

            // Only the first SaveChangesAsync per context is synchronized (the city insert) - the
            // rest of ReplaceUserSelectionAsync's multi-save transaction is left to resolve on its
            // own, so the loser's real DB lock wait can't deadlock against this thread-level barrier.
            var barrier = new ConcurrentSaveBarrier(participantCount: 2);
            await using var dbA = _fixture.CreateDbContext(barrier);
            await using var dbB = _fixture.CreateDbContext(barrier);
            var repositoryA = new SelectionsRepository(dbA, NullLogger<SelectionsRepository>.Instance);
            var repositoryB = new SelectionsRepository(dbB, NullLogger<SelectionsRepository>.Instance);

            var results = await Task.WhenAll(
                repositoryA.ReplaceUserSelectionAsync(uidA, [athens], [serviceId]),
                repositoryB.ReplaceUserSelectionAsync(uidB, [athens], [serviceId]));

            Assert.All(results, Assert.True);

            await using var verifyDb = _fixture.CreateDbContext();
            Assert.Equal(1, await verifyDb.Cities.CountAsync());
        }

        [Fact]
        public async Task ReplaceUserSelectionAsync_PersistentInsertFailure_ReturnsFalse()
        {
            var uid = await SeedUserAsync();
            var serviceId = await SeedServiceAsync();

            // Exceeds the CK_Cities_Latitude check constraint (-90..90), so SQL Server rejects
            // the insert with no chance of a "someone else already inserted it" recovery.
            var invalidCity = new CityDto("Nowhere", "Nowhere", 999m, 0m);

            await using var db = _fixture.CreateDbContext();
            var repository = new SelectionsRepository(db, NullLogger<SelectionsRepository>.Instance);

            var succeeded = await repository.ReplaceUserSelectionAsync(uid, [invalidCity], [serviceId]);

            Assert.False(succeeded);
        }

        [Fact]
        public async Task TryValidateServiceIdsAsync_DatabaseUnreachable_ReturnsFailure()
        {
            await using var brokenDb = CreateUnreachableDbContext();
            var repository = new SelectionsRepository(brokenDb, NullLogger<SelectionsRepository>.Instance);

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

        private async Task<int> SeedServiceAsync()
        {
            await using var db = _fixture.CreateDbContext();
            var service = new ForecastingService { Name = "OpenWeather", ApiEndpoint = "https://example.test" };
            db.ForecastingServices.Add(service);
            await db.SaveChangesAsync();
            return service.ServiceId;
        }
    }
}
