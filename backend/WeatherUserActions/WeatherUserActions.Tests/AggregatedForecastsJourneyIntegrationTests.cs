using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using WeatherUserActions.Controllers;
using WeatherUserActions.Data;
using WeatherUserActions.Dtos;
using WeatherUserActions.AppwriteServices;
using WeatherUserActions.Models;
using WeatherUserActions.Repositories;
using WeatherUserActions.Services;
using WeatherUserActions.Tests.Fakes;
using WeatherUserActions.Tests.Fixtures;
using Xunit;

namespace WeatherUserActions.Tests
{
    // End-to-end journeys spanning SelectionsController, ForecastsController's rating endpoint, and
    // the aggregated forecasts endpoint together - AggregatedForecastsControllerLogicTests only
    // exercises the aggregation logic in isolation, against a fixed selection/rating snapshot.
    [Collection(TestCollections.SqlServer)]
    public class AggregatedForecastsJourneyIntegrationTests : IAsyncLifetime
    {
        private readonly SqlServerFixture _fixture;

        public AggregatedForecastsJourneyIntegrationTests(SqlServerFixture fixture)
        {
            _fixture = fixture;
        }

        public Task InitializeAsync() => _fixture.ResetDatabaseAsync();

        public Task DisposeAsync() => Task.CompletedTask;

        [Fact]
        public async Task AggregatedForecasts_RaterUpdatesRating_FlipsOneCitysWinnerButLeavesTheOtherUnaffected()
        {
            var uid = UniqueUid();
            var raterA = UniqueUid();
            var raterB = UniqueUid();
            await SeedUserAsync(uid);
            await SeedUserAsync(raterA);
            await SeedUserAsync(raterB);
            var openWeather = await SeedServiceAsync(name: "OpenWeather");
            var weatherApi = await SeedServiceAsync(name: "WeatherAPI", apiEndpoint: "https://example2.test");
            var athens = new CityDto("Athens", "Greece", 37.98m, 23.72m);
            var paris = new CityDto("Paris", "France", 48.85m, 2.35m);

            // Viewer selects both cities, both services - so both cities have a real comparison.
            await using (var db = _fixture.CreateDbContext())
            {
                var controller = CreateSelectionsController(db, FakeAppwriteAuthService.ReturningUid(uid), bearerToken: "token");
                var result = await controller.SaveSelections(
                    new SaveSelectionsRequest([athens, paris], [openWeather, weatherApi]), CancellationToken.None);
                Assert.IsType<OkResult>(result);
            }

            var athensOpenWeatherSite = await GetCitySiteIdAsync(athens.Name, "OpenWeather");
            var athensWeatherApiSite = await GetCitySiteIdAsync(athens.Name, "WeatherAPI");
            var parisOpenWeatherSite = await GetCitySiteIdAsync(paris.Name, "OpenWeather");
            var parisWeatherApiSite = await GetCitySiteIdAsync(paris.Name, "WeatherAPI");

            var athensTimestamp = DateTime.UtcNow.AddHours(1);
            var parisTimestamp = DateTime.UtcNow.AddHours(2);
            var athensOpenWeatherForecastId = await SeedForecastAsync(athensOpenWeatherSite, athensTimestamp);
            var athensWeatherApiForecastId = await SeedForecastAsync(athensWeatherApiSite, athensTimestamp);
            var parisOpenWeatherForecastId = await SeedForecastAsync(parisOpenWeatherSite, parisTimestamp);
            var parisWeatherApiForecastId = await SeedForecastAsync(parisWeatherApiSite, parisTimestamp);

            // Baseline: OpenWeather rated higher in both cities, so it wins both.
            await SeedRatingAsync(raterA, athensOpenWeatherForecastId, value: 4);
            await SeedRatingAsync(raterB, athensOpenWeatherForecastId, value: 4);
            await SeedRatingAsync(raterA, athensWeatherApiForecastId, value: 2);
            await SeedRatingAsync(raterB, athensWeatherApiForecastId, value: 2);
            await SeedRatingAsync(raterA, parisOpenWeatherForecastId, value: 4);
            await SeedRatingAsync(raterB, parisOpenWeatherForecastId, value: 4);
            await SeedRatingAsync(raterA, parisWeatherApiForecastId, value: 2);
            await SeedRatingAsync(raterB, parisWeatherApiForecastId, value: 2);

            // Step 1: viewer sees OpenWeather winning both cities.
            await using (var db = _fixture.CreateDbContext())
            {
                var controller = CreateForecastsController(db, FakeAppwriteAuthService.ReturningUid(uid), bearerToken: "token");
                var result = await controller.GetAggregatedForecasts(CancellationToken.None);
                var ok = Assert.IsType<OkObjectResult>(result.Result);
                var body = Assert.IsType<GetAggregatedForecastsResponse>(ok.Value);
                Assert.Equal(2, body.Forecasts.Count);
                Assert.Equal("Athens", body.Forecasts[0].City);
                Assert.Equal("OpenWeather", body.Forecasts[0].Service);
                Assert.Equal(athensOpenWeatherForecastId, body.Forecasts[0].ForecastId);
                Assert.Equal("Paris", body.Forecasts[1].City);
                Assert.Equal("OpenWeather", body.Forecasts[1].Service);
                Assert.Equal(parisOpenWeatherForecastId, body.Forecasts[1].ForecastId);
                Assert.All(body.ServiceMetadata, metadata => Assert.Equal("OpenWeather", metadata.Service));
            }

            // Step 2: both raters bump their WeatherAPI rating for Athens above OpenWeather's -
            // through the real rating endpoint (an upsert), not a direct DB write.
            await using (var db = _fixture.CreateDbContext())
            {
                var controller = CreateForecastsController(db, FakeAppwriteAuthService.ReturningUid(raterA), bearerToken: "token");
                var result = await controller.RateForecast(athensWeatherApiForecastId, new RateForecastRequest(5), CancellationToken.None);
                Assert.IsType<OkResult>(result);
            }
            await using (var db = _fixture.CreateDbContext())
            {
                var controller = CreateForecastsController(db, FakeAppwriteAuthService.ReturningUid(raterB), bearerToken: "token");
                var result = await controller.RateForecast(athensWeatherApiForecastId, new RateForecastRequest(5), CancellationToken.None);
                Assert.IsType<OkResult>(result);
            }

            // Step 3: Athens' winner flips to WeatherAPI, Paris is untouched and still OpenWeather.
            await using (var db = _fixture.CreateDbContext())
            {
                var controller = CreateForecastsController(db, FakeAppwriteAuthService.ReturningUid(uid), bearerToken: "token");
                var result = await controller.GetAggregatedForecasts(CancellationToken.None);
                var ok = Assert.IsType<OkObjectResult>(result.Result);
                var body = Assert.IsType<GetAggregatedForecastsResponse>(ok.Value);
                Assert.Equal(2, body.Forecasts.Count);
                Assert.Equal("Athens", body.Forecasts[0].City);
                Assert.Equal("WeatherAPI", body.Forecasts[0].Service);
                Assert.Equal(athensWeatherApiForecastId, body.Forecasts[0].ForecastId);
                Assert.Equal("Paris", body.Forecasts[1].City);
                Assert.Equal("OpenWeather", body.Forecasts[1].Service);
                Assert.Equal(parisOpenWeatherForecastId, body.Forecasts[1].ForecastId);

                var athensMetadata = body.ServiceMetadata.Single(metadata => metadata.City == "Athens");
                Assert.Equal("WeatherAPI", athensMetadata.Service);
                Assert.Equal(5m, athensMetadata.AverageRating);
                Assert.Equal(2, athensMetadata.RatingCount);

                var parisMetadata = body.ServiceMetadata.Single(metadata => metadata.City == "Paris");
                Assert.Equal("OpenWeather", parisMetadata.Service);
                Assert.Equal(4m, parisMetadata.AverageRating);
            }

            // Final DB state: both raters' Athens/WeatherAPI ratings were updated in place (still 2
            // rows, not 4), and Paris's ratings were never touched.
            await using var verifyDb = _fixture.CreateDbContext();
            var athensWeatherApiRatings = await verifyDb.Ratings
                .Where(rating => rating.ForecastId == athensWeatherApiForecastId)
                .OrderBy(rating => rating.UserId)
                .ToListAsync();
            Assert.Equal(2, athensWeatherApiRatings.Count);
            Assert.All(athensWeatherApiRatings, rating => Assert.Equal(5, rating.Value));

            var parisOpenWeatherRatings = await verifyDb.Ratings
                .Where(rating => rating.ForecastId == parisOpenWeatherForecastId)
                .ToListAsync();
            Assert.Equal(2, parisOpenWeatherRatings.Count);
            Assert.All(parisOpenWeatherRatings, rating => Assert.Equal(4, rating.Value));
        }

        [Fact]
        public async Task AggregatedForecasts_ViewerChangesInterests_DropsOldCityAndOnboardsNewCityByRating()
        {
            var uid = UniqueUid();
            var raterA = UniqueUid();
            var raterB = UniqueUid();
            await SeedUserAsync(uid);
            await SeedUserAsync(raterA);
            await SeedUserAsync(raterB);
            var openWeather = await SeedServiceAsync(name: "OpenWeather");
            var weatherApi = await SeedServiceAsync(name: "WeatherAPI", apiEndpoint: "https://example2.test");
            var athens = new CityDto("Athens", "Greece", 37.98m, 23.72m);
            var paris = new CityDto("Paris", "France", 48.85m, 2.35m);
            var rome = new CityDto("Rome", "Italy", 41.90m, 12.49m);

            // Step 1: viewer starts out interested in Athens and Paris, both services.
            await using (var db = _fixture.CreateDbContext())
            {
                var controller = CreateSelectionsController(db, FakeAppwriteAuthService.ReturningUid(uid), bearerToken: "token");
                var result = await controller.SaveSelections(
                    new SaveSelectionsRequest([athens, paris], [openWeather, weatherApi]), CancellationToken.None);
                Assert.IsType<OkResult>(result);
            }

            var athensOpenWeatherSite = await GetCitySiteIdAsync(athens.Name, "OpenWeather");
            var athensWeatherApiSite = await GetCitySiteIdAsync(athens.Name, "WeatherAPI");
            var parisOpenWeatherSite = await GetCitySiteIdAsync(paris.Name, "OpenWeather");
            var parisWeatherApiSite = await GetCitySiteIdAsync(paris.Name, "WeatherAPI");

            var athensTimestamp = DateTime.UtcNow.AddHours(1);
            var parisTimestamp = DateTime.UtcNow.AddHours(2);
            var athensOpenWeatherForecastId = await SeedForecastAsync(athensOpenWeatherSite, athensTimestamp);
            await SeedForecastAsync(athensWeatherApiSite, athensTimestamp);
            var parisOpenWeatherForecastId = await SeedForecastAsync(parisOpenWeatherSite, parisTimestamp);
            await SeedForecastAsync(parisWeatherApiSite, parisTimestamp);

            // OpenWeather wins both Athens and Paris.
            await SeedRatingAsync(raterA, athensOpenWeatherForecastId, value: 5);
            await SeedRatingAsync(raterB, athensOpenWeatherForecastId, value: 5);
            await SeedRatingAsync(raterA, parisOpenWeatherForecastId, value: 5);
            await SeedRatingAsync(raterB, parisOpenWeatherForecastId, value: 5);

            // Step 2: viewer sees Athens and Paris, both won by OpenWeather.
            await using (var db = _fixture.CreateDbContext())
            {
                var controller = CreateForecastsController(db, FakeAppwriteAuthService.ReturningUid(uid), bearerToken: "token");
                var result = await controller.GetAggregatedForecasts(CancellationToken.None);
                var ok = Assert.IsType<OkObjectResult>(result.Result);
                var body = Assert.IsType<GetAggregatedForecastsResponse>(ok.Value);
                Assert.Equal(2, body.Forecasts.Count);
                Assert.Equal("Athens", body.Forecasts[0].City);
                Assert.Equal("Paris", body.Forecasts[1].City);
            }

            // Step 3: viewer changes interests - drops Athens, keeps Paris, adds Rome.
            await using (var db = _fixture.CreateDbContext())
            {
                var controller = CreateSelectionsController(db, FakeAppwriteAuthService.ReturningUid(uid), bearerToken: "token");
                var result = await controller.SaveSelections(
                    new SaveSelectionsRequest([paris, rome], [openWeather, weatherApi]), CancellationToken.None);
                Assert.IsType<OkResult>(result);
            }

            var romeOpenWeatherSite = await GetCitySiteIdAsync(rome.Name, "OpenWeather");
            var romeWeatherApiSite = await GetCitySiteIdAsync(rome.Name, "WeatherAPI");
            var romeTimestamp = DateTime.UtcNow.AddHours(3);
            await SeedForecastAsync(romeOpenWeatherSite, romeTimestamp);
            var romeWeatherApiForecastId = await SeedForecastAsync(romeWeatherApiSite, romeTimestamp);

            // Rome is won by WeatherAPI this time, to prove the winner is computed fresh for the
            // newly-added city rather than defaulting to whichever service won elsewhere.
            await SeedRatingAsync(raterA, romeWeatherApiForecastId, value: 5);
            await SeedRatingAsync(raterB, romeWeatherApiForecastId, value: 5);

            // Step 4: viewer now sees Paris (unchanged winner) and Rome (newly onboarded) -
            // Athens is gone from the response even though its forecast/rating data still exists.
            await using (var db = _fixture.CreateDbContext())
            {
                var controller = CreateForecastsController(db, FakeAppwriteAuthService.ReturningUid(uid), bearerToken: "token");
                var result = await controller.GetAggregatedForecasts(CancellationToken.None);
                var ok = Assert.IsType<OkObjectResult>(result.Result);
                var body = Assert.IsType<GetAggregatedForecastsResponse>(ok.Value);
                Assert.Equal(2, body.Forecasts.Count);
                Assert.DoesNotContain(body.Forecasts, forecast => forecast.City == "Athens");
                Assert.Equal("Paris", body.Forecasts[0].City);
                Assert.Equal("OpenWeather", body.Forecasts[0].Service);
                Assert.Equal(parisOpenWeatherForecastId, body.Forecasts[0].ForecastId);
                Assert.Equal("Rome", body.Forecasts[1].City);
                Assert.Equal("WeatherAPI", body.Forecasts[1].Service);
                Assert.Equal(romeWeatherApiForecastId, body.Forecasts[1].ForecastId);
            }

            // Final DB state: exactly the four Paris/Rome selections remain, Athens is gone.
            await using var verifyDb = _fixture.CreateDbContext();
            var userCitySites = await verifyDb.UserCitySites
                .Where(userCitySite => userCitySite.UserId == uid)
                .Include(userCitySite => userCitySite.CitySite).ThenInclude(citySite => citySite.City)
                .Include(userCitySite => userCitySite.CitySite).ThenInclude(citySite => citySite.Service)
                .OrderBy(userCitySite => userCitySite.CitySite.City.Name)
                .ThenBy(userCitySite => userCitySite.CitySite.Service.Name)
                .ToListAsync();
            Assert.Equal(4, userCitySites.Count);
            Assert.All(userCitySites, userCitySite => Assert.NotEqual("Athens", userCitySite.CitySite.City.Name));
            Assert.Equal(["Paris", "Paris", "Rome", "Rome"], userCitySites.Select(userCitySite => userCitySite.CitySite.City.Name));
            Assert.Equal(
                ["OpenWeather", "WeatherAPI", "OpenWeather", "WeatherAPI"],
                userCitySites.Select(userCitySite => userCitySite.CitySite.Service.Name));
        }

        private static string UniqueUid() => $"uid-{Guid.NewGuid():N}";

        private async Task SeedUserAsync(string uid)
        {
            await using var db = _fixture.CreateDbContext();
            db.Users.Add(new User { UserId = uid, CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }

        private async Task<int> SeedServiceAsync(string name = "OpenWeather", string apiEndpoint = "https://example.test")
        {
            await using var db = _fixture.CreateDbContext();
            var service = new ForecastingService { Name = name, ApiEndpoint = apiEndpoint };
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

        private async Task SeedRatingAsync(string uid, int forecastId, int value)
        {
            await using var db = _fixture.CreateDbContext();
            db.Ratings.Add(new Rating
            {
                UserId = uid,
                ForecastId = forecastId,
                Value = value,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
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
            WeatherUserActionsDbContext db, IAppwriteAuthService authService, string? bearerToken)
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
            WeatherUserActionsDbContext db, IAppwriteAuthService authService, string? bearerToken)
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
