using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using WeatherUserActions.Data;
using WeatherUserActions.Models;
using WeatherUserActions.Repositories;
using WeatherUserActions.Tests.Fixtures;
using Xunit;

namespace WeatherUserActions.Tests
{
    // Exercises RatingsRepository's DB-failure handling and concurrency behavior directly
    // against the real Testcontainers SQL Server - no mocking of EF Core or ADO.NET exception types.
    [Collection(TestCollections.SqlServer)]
    public class RatingsRepositoryTests : IAsyncLifetime
    {
        private readonly SqlServerFixture _fixture;

        public RatingsRepositoryTests(SqlServerFixture fixture)
        {
            _fixture = fixture;
        }

        public Task InitializeAsync() => _fixture.ResetDatabaseAsync();

        public Task DisposeAsync() => Task.CompletedTask;

        [Fact]
        public async Task UpsertRatingAsync_ForecastDoesNotExist_ReturnsForecastExistsFalseAndPersistsNothing()
        {
            await using var db = _fixture.CreateDbContext();
            var repository = new RatingsRepository(db, NullLogger<RatingsRepository>.Instance);

            var (succeeded, forecastExists) = await repository.UpsertRatingAsync("uid-1", forecastId: 999, value: 4);

            Assert.True(succeeded);
            Assert.False(forecastExists);

            await using var verifyDb = _fixture.CreateDbContext();
            Assert.Equal(0, await verifyDb.Ratings.CountAsync());
        }

        [Fact]
        public async Task UpsertRatingAsync_DatabaseUnreachable_ReturnsFailure()
        {
            await using var brokenDb = CreateUnreachableDbContext();
            var repository = new RatingsRepository(brokenDb, NullLogger<RatingsRepository>.Instance);

            var (succeeded, _) = await repository.UpsertRatingAsync("uid-1", forecastId: 1, value: 4);

            Assert.False(succeeded);
        }

        [Fact]
        public async Task RemoveRatingAsync_DatabaseUnreachable_ReturnsFalse()
        {
            await using var brokenDb = CreateUnreachableDbContext();
            var repository = new RatingsRepository(brokenDb, NullLogger<RatingsRepository>.Instance);

            var succeeded = await repository.RemoveRatingAsync("uid-1", forecastId: 1);

            Assert.False(succeeded);
        }

        [Fact]
        public async Task UpsertRatingAsync_ConcurrentCallsForSameUserAndForecast_OnlyOneRatingPersists()
        {
            var uid = await SeedUserAsync();
            var serviceId = await SeedServiceAsync();
            var cityId = await SeedCityAsync();
            var citySiteId = await SeedCitySiteAsync(cityId, serviceId);
            var forecastId = await SeedForecastAsync(citySiteId);

            // Task.WhenAll alone doesn't guarantee the two SaveChangesAsync calls actually collide -
            // one full check-then-insert sequence can finish before the other's check even runs, which
            // just becomes a normal update instead of a race. This barrier forces both contexts to reach
            // their INSERT at the same instant, so the DB's unique index is the one deciding the winner.
            var barrier = new ConcurrentSaveBarrier(participantCount: 2);
            await using var dbA = _fixture.CreateDbContext(barrier);
            await using var dbB = _fixture.CreateDbContext(barrier);
            var repositoryA = new RatingsRepository(dbA, NullLogger<RatingsRepository>.Instance);
            var repositoryB = new RatingsRepository(dbB, NullLogger<RatingsRepository>.Instance);

            var results = await Task.WhenAll(
                repositoryA.UpsertRatingAsync(uid, forecastId, value: 3),
                repositoryB.UpsertRatingAsync(uid, forecastId, value: 4));

            // No retry on a unique-index race (unlike SelectionsRepository's city upsert) - exactly
            // one of the two concurrent inserts wins, the other fails.
            Assert.Contains(results, r => r.Succeeded);
            Assert.Contains(results, r => !r.Succeeded);

            await using var verifyDb = _fixture.CreateDbContext();
            Assert.Equal(1, await verifyDb.Ratings.CountAsync());
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

        private async Task<int> SeedCityAsync()
        {
            await using var db = _fixture.CreateDbContext();
            var city = new City { Name = "Athens", Country = "Greece", Latitude = 37.98m, Longitude = 23.72m };
            db.Cities.Add(city);
            await db.SaveChangesAsync();
            return city.CityId;
        }

        private async Task<int> SeedCitySiteAsync(int cityId, int serviceId)
        {
            await using var db = _fixture.CreateDbContext();
            var citySite = new CitySite { CityId = cityId, ServiceId = serviceId };
            db.CitySites.Add(citySite);
            await db.SaveChangesAsync();
            return citySite.CitySiteId;
        }

        private async Task<int> SeedForecastAsync(int citySiteId)
        {
            await using var db = _fixture.CreateDbContext();
            var forecast = new Forecast
            {
                CitySiteId = citySiteId,
                Timestamp = DateTime.UtcNow.AddHours(1),
                Type = "CURRENT",
                Temperature = 20m,
                Humidity = 50m,
                WindSpeed = 10m,
                DangerFlag = false,
                RetrievedAt = DateTime.UtcNow,
            };
            db.Forecasts.Add(forecast);
            await db.SaveChangesAsync();
            return forecast.ForecastId;
        }
    }
}
