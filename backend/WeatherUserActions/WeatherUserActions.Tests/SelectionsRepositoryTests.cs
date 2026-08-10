using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using WeatherUserActions.Dtos;
using WeatherUserActions.Models;
using WeatherUserActions.Repositories;
using WeatherUserActions.Tests.Fixtures;
using Xunit;

namespace WeatherUserActions.Tests
{
    // Exercises SelectionsRepository's city matching directly against the real
    // Testcontainers SQL Server.
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
        public async Task ReplaceUserSelectionAsync_NameMatchesOneRowAndCountryMatchesAnother_DoesNotReuseEitherRow()
        {
            var uid = await SeedUserAsync();
            var serviceId = await SeedServiceAsync();

            // Rows that share a Name with one requested city and a Country with the other,
            // but neither row is an actual Name+Country match for what's being requested.
            await using (var seedDb = _fixture.CreateDbContext())
            {
                seedDb.Cities.AddRange(
                    new City { Name = "Athens", Country = "Germany", Latitude = 1m, Longitude = 1m },
                    new City { Name = "Berlin", Country = "Greece", Latitude = 2m, Longitude = 2m });
                await seedDb.SaveChangesAsync();
            }

            var athensGreece = new CityDto("Athens", "Greece", 37.98m, 23.72m);
            var berlinGermany = new CityDto("Berlin", "Germany", 52.52m, 13.40m);

            await using var db = _fixture.CreateDbContext();
            var repository = new SelectionsRepository(db, NullLogger<SelectionsRepository>.Instance);

            var succeeded = await repository.ReplaceUserSelectionAsync(
                uid, [athensGreece, berlinGermany], [serviceId]);

            Assert.True(succeeded);

            await using var verifyDb = _fixture.CreateDbContext();
            // The two mismatched seed rows must still exist untouched, plus two brand-new
            // rows for the actual Athens/Greece and Berlin/Germany pairs - never reused.
            Assert.Equal(4, await verifyDb.Cities.CountAsync());

            var userCities = await verifyDb.UserCitySites
                .Include(ucs => ucs.CitySite).ThenInclude(cs => cs.City)
                .Where(ucs => ucs.UserId == uid)
                .Select(ucs => ucs.CitySite.City)
                .ToListAsync();

            Assert.Equal(2, userCities.Count);
            Assert.Contains(userCities, c => c.Name == "Athens" && c.Country == "Greece");
            Assert.Contains(userCities, c => c.Name == "Berlin" && c.Country == "Germany");
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
