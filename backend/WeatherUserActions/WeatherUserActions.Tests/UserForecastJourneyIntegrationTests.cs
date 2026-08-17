using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using WeatherUserActions.Controllers;
using WeatherUserActions.Data;
using WeatherUserActions.Dtos;
using WeatherUserActions.FirebaseServices;
using WeatherUserActions.Models;
using WeatherUserActions.Repositories;
using WeatherUserActions.Services;
using WeatherUserActions.Tests.Fakes;
using WeatherUserActions.Tests.Fixtures;
using Xunit;

namespace WeatherUserActions.Tests
{
    // End-to-end journeys spanning SelectionsController and ForecastsController together -
    // each individual LogicTests file only exercises its own controller in isolation.
    [Collection(TestCollections.SqlServer)]
    public class UserForecastJourneyIntegrationTests : IAsyncLifetime
    {
        private readonly SqlServerFixture _fixture;

        public UserForecastJourneyIntegrationTests(SqlServerFixture fixture)
        {
            _fixture = fixture;
        }

        public Task InitializeAsync() => _fixture.ResetDatabaseAsync();

        public Task DisposeAsync() => Task.CompletedTask;

        [Fact]
        public async Task UserJourney_ChangesInterestThenRatesForecasts_ReflectsCurrentSelectionAndRatings()
        {
            var uid = UniqueUid();
            await SeedUserAsync(uid);
            var serviceId = await SeedServiceAsync();
            var athens = new CityDto("Athens", "Greece", 37.98m, 23.72m);
            var paris = new CityDto("Paris", "France", 48.85m, 2.35m);

            // Step 1: user selects Athens.
            await using (var db = _fixture.CreateDbContext())
            {
                var controller = CreateSelectionsController(db, FakeFirebaseAuthService.ReturningUid(uid), bearerToken: "token");
                var result = await controller.SaveSelections(new SaveSelectionsRequest([athens], [serviceId]), CancellationToken.None);
                Assert.IsType<OkResult>(result);
            }

            var athensCitySiteId = await GetCitySiteIdAsync(athens.Name, "OpenWeather");
            var athensTimestamp = DateTime.UtcNow.AddHours(1);
            var athensForecastId = await SeedForecastAsync(athensCitySiteId, athensTimestamp);

            // Step 2: user views forecasts - sees only Athens, unrated.
            await using (var db = _fixture.CreateDbContext())
            {
                var controller = CreateForecastsController(db, FakeFirebaseAuthService.ReturningUid(uid), bearerToken: "token");
                var result = await controller.GetForecasts(CancellationToken.None);
                var ok = Assert.IsType<OkObjectResult>(result.Result);
                var body = Assert.IsType<GetForecastsResponse>(ok.Value);
                var forecast = Assert.Single(body.Forecasts);
                Assert.Equal("Athens", forecast.City);
                Assert.Equal("Greece", forecast.Country);
                Assert.Equal("OpenWeather", forecast.Service);
                Assert.Equal(athensForecastId, forecast.ForecastId);
                Assert.Equal(athensTimestamp, forecast.Timestamp, TimeSpan.FromSeconds(1));
                Assert.Null(forecast.UserRating);
            }

            // Step 3: user changes interest to Paris - Athens is dropped from the selection.
            await using (var db = _fixture.CreateDbContext())
            {
                var controller = CreateSelectionsController(db, FakeFirebaseAuthService.ReturningUid(uid), bearerToken: "token");
                var result = await controller.SaveSelections(new SaveSelectionsRequest([paris], [serviceId]), CancellationToken.None);
                Assert.IsType<OkResult>(result);
            }

            var parisCitySiteId = await GetCitySiteIdAsync(paris.Name, "OpenWeather");
            var parisTimestamp = DateTime.UtcNow.AddHours(5);
            var parisForecastId = await SeedForecastAsync(parisCitySiteId, parisTimestamp);

            // Step 4: user views forecasts again - now sees only Paris, Athens no longer appears.
            await using (var db = _fixture.CreateDbContext())
            {
                var controller = CreateForecastsController(db, FakeFirebaseAuthService.ReturningUid(uid), bearerToken: "token");
                var result = await controller.GetForecasts(CancellationToken.None);
                var ok = Assert.IsType<OkObjectResult>(result.Result);
                var body = Assert.IsType<GetForecastsResponse>(ok.Value);
                var forecast = Assert.Single(body.Forecasts);
                Assert.Equal("Paris", forecast.City);
                Assert.Equal("France", forecast.Country);
                Assert.Equal("OpenWeather", forecast.Service);
                Assert.Equal(parisForecastId, forecast.ForecastId);
                Assert.Equal(parisTimestamp, forecast.Timestamp, TimeSpan.FromSeconds(1));
                Assert.Null(forecast.UserRating);
            }

            // Step 5: user rates the Paris forecast.
            await using (var db = _fixture.CreateDbContext())
            {
                var controller = CreateForecastsController(db, FakeFirebaseAuthService.ReturningUid(uid), bearerToken: "token");
                var result = await controller.RateForecast(parisForecastId, new RateForecastRequest(5), CancellationToken.None);
                Assert.IsType<OkResult>(result);
            }

            // Step 6: user views forecasts once more - the rating now shows up.
            await using (var db = _fixture.CreateDbContext())
            {
                var controller = CreateForecastsController(db, FakeFirebaseAuthService.ReturningUid(uid), bearerToken: "token");
                var result = await controller.GetForecasts(CancellationToken.None);
                var ok = Assert.IsType<OkObjectResult>(result.Result);
                var body = Assert.IsType<GetForecastsResponse>(ok.Value);
                var forecast = Assert.Single(body.Forecasts);
                Assert.Equal("Paris", forecast.City);
                Assert.Equal("France", forecast.Country);
                Assert.Equal("OpenWeather", forecast.Service);
                Assert.Equal(parisForecastId, forecast.ForecastId);
                Assert.Equal(parisTimestamp, forecast.Timestamp, TimeSpan.FromSeconds(1));
                Assert.Equal(5, forecast.UserRating);
            }

            // Final DB state: exactly one active selection (Paris) and one rating (on Paris's forecast).
            await using var verifyDb = _fixture.CreateDbContext();
            var userCitySites = await verifyDb.UserCitySites
                .Where(userCitySite => userCitySite.UserId == uid)
                .Include(userCitySite => userCitySite.CitySite).ThenInclude(citySite => citySite.City)
                .Include(userCitySite => userCitySite.CitySite).ThenInclude(citySite => citySite.Service)
                .ToListAsync();
            var userCitySite = Assert.Single(userCitySites);
            Assert.Equal("Paris", userCitySite.CitySite.City.Name);
            Assert.Equal("OpenWeather", userCitySite.CitySite.Service.Name);

            var ratings = await verifyDb.Ratings
                .Where(rating => rating.UserId == uid)
                .Include(rating => rating.Forecast).ThenInclude(forecast => forecast.CitySite).ThenInclude(citySite => citySite.City)
                .Include(rating => rating.Forecast).ThenInclude(forecast => forecast.CitySite).ThenInclude(citySite => citySite.Service)
                .ToListAsync();
            var persistedRating = Assert.Single(ratings);
            Assert.Equal(parisForecastId, persistedRating.ForecastId);
            Assert.Equal("Paris", persistedRating.Forecast.CitySite.City.Name);
            Assert.Equal("OpenWeather", persistedRating.Forecast.CitySite.Service.Name);
            Assert.Equal(5, persistedRating.Value);
        }

        private static string UniqueUid() => $"uid-{Guid.NewGuid():N}";

        private async Task SeedUserAsync(string uid)
        {
            await using var db = _fixture.CreateDbContext();
            db.Users.Add(new User { UserId = uid, CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }

        private async Task<int> SeedServiceAsync()
        {
            await using var db = _fixture.CreateDbContext();
            var service = new ForecastingService { Name = "OpenWeather", ApiEndpoint = "https://example.test" };
            db.ForecastingServices.Add(service);
            await db.SaveChangesAsync();
            return service.ServiceId;
        }

        private async Task<int> SeedForecastAsync(int citySiteId, DateTime timestamp)
        {
            await using var db = _fixture.CreateDbContext();
            var forecast = new Forecast
            {
                CitySiteId = citySiteId,
                Timestamp = timestamp,
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

        private async Task<int> GetCitySiteIdAsync(string cityName, string serviceName)
        {
            await using var db = _fixture.CreateDbContext();
            return await db.CitySites
                .Where(citySite => citySite.Service.Name == serviceName && citySite.City.Name == cityName)
                .Select(citySite => citySite.CitySiteId)
                .SingleAsync();
        }

        private static SelectionsController CreateSelectionsController(
            WeatherUserActionsDbContext db, IFirebaseAuthService authService, string? bearerToken)
        {
            var repository = new SelectionsRepository(db, NullLogger<SelectionsRepository>.Instance);
            var service = new SelectionsService(authService, repository, NullLogger<SelectionsService>.Instance);
            var controller = new SelectionsController(service)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext(),
                },
            };

            if (bearerToken is not null)
            {
                controller.ControllerContext.HttpContext.Request.Headers.Authorization = $"Bearer {bearerToken}";
            }

            return controller;
        }

        private static ForecastsController CreateForecastsController(
            WeatherUserActionsDbContext db, IFirebaseAuthService authService, string? bearerToken)
        {
            var forecastsRepository = new ForecastsRepository(db, NullLogger<ForecastsRepository>.Instance);
            var forecastsService = new ForecastsService(authService, forecastsRepository, NullLogger<ForecastsService>.Instance);
            var ratingsRepository = new RatingsRepository(db, NullLogger<RatingsRepository>.Instance);
            var ratingsService = new RatingsService(authService, ratingsRepository, NullLogger<RatingsService>.Instance);
            var aggregatedForecastsRepository = new AggregatedForecastsRepository(db, NullLogger<AggregatedForecastsRepository>.Instance);
            var aggregatedForecastsService = new AggregatedForecastsService(
                authService, aggregatedForecastsRepository, NullLogger<AggregatedForecastsService>.Instance);
            var controller = new ForecastsController(forecastsService, ratingsService, aggregatedForecastsService)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext(),
                },
            };

            if (bearerToken is not null)
            {
                controller.ControllerContext.HttpContext.Request.Headers.Authorization = $"Bearer {bearerToken}";
            }

            return controller;
        }
    }
}
