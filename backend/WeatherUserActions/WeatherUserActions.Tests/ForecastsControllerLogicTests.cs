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
    [Collection(TestCollections.SqlServer)]
    public class ForecastsControllerLogicTests : IAsyncLifetime
    {
        private readonly SqlServerFixture _fixture;

        public ForecastsControllerLogicTests(SqlServerFixture fixture)
        {
            _fixture = fixture;
        }

        public Task InitializeAsync() => _fixture.ResetDatabaseAsync();

        public Task DisposeAsync() => Task.CompletedTask;

        [Fact]
        public async Task GetForecasts_MissingAuthorizationHeader_ReturnsUnauthorized()
        {
            await using var db = _fixture.CreateDbContext();
            var controller = CreateController(db, FakeFirebaseAuthService.ReturningUid("irrelevant"), bearerToken: null);

            var result = await controller.GetForecasts(CancellationToken.None);

            Assert.IsType<UnauthorizedResult>(result.Result);
        }

        [Fact]
        public async Task GetForecasts_UserHasNoSelections_ReturnsEmptyList()
        {
            var uid = UniqueUid();
            await SeedUserAsync(uid);
            await using var db = _fixture.CreateDbContext();
            var controller = CreateController(db, FakeFirebaseAuthService.ReturningUid(uid), bearerToken: "token");

            var result = await controller.GetForecasts(CancellationToken.None);

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var body = Assert.IsType<GetForecastsResponse>(ok.Value);
            Assert.Empty(body.Forecasts);
        }

        [Fact]
        public async Task GetForecasts_UserHasSelection_ReturnsMatchingForecast()
        {
            var uid = UniqueUid();
            await SeedUserAsync(uid);
            var serviceId = await SeedServiceAsync();
            var cityId = await SeedCityAsync();
            var citySiteId = await SeedCitySiteAsync(cityId, serviceId);
            await SeedUserCitySiteAsync(uid, citySiteId);
            var timestamp = DateTime.UtcNow.AddHours(1);
            await SeedForecastAsync(citySiteId, timestamp, "CURRENT", temperature: 25m, humidity: 50m, windSpeed: 10m, dangerFlag: false);

            await using var db = _fixture.CreateDbContext();
            var controller = CreateController(db, FakeFirebaseAuthService.ReturningUid(uid), bearerToken: "token");

            var result = await controller.GetForecasts(CancellationToken.None);

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var body = Assert.IsType<GetForecastsResponse>(ok.Value);
            var forecast = Assert.Single(body.Forecasts);
            Assert.Equal("Athens", forecast.City);
            Assert.Equal("Greece", forecast.Country);
            Assert.Equal("OpenWeather", forecast.Service);
            Assert.Equal("CURRENT", forecast.Type);
            Assert.Equal(timestamp, forecast.Timestamp, TimeSpan.FromSeconds(1));
            Assert.Equal(25m, forecast.Temperature);
            Assert.Equal(50m, forecast.Humidity);
            Assert.Equal(10m, forecast.WindSpeed);
            Assert.False(forecast.DangerFlag);
            Assert.Null(forecast.UserRating);
        }

        [Fact]
        public async Task GetForecasts_UserHasRatedForecast_ReturnsUserRating()
        {
            var uid = UniqueUid();
            await SeedUserAsync(uid);
            var serviceId = await SeedServiceAsync();
            var cityId = await SeedCityAsync();
            var citySiteId = await SeedCitySiteAsync(cityId, serviceId);
            await SeedUserCitySiteAsync(uid, citySiteId);
            var forecastId = await SeedForecastAsync(citySiteId, DateTime.UtcNow.AddHours(1), "CURRENT");
            await SeedRatingAsync(uid, forecastId, value: 4);

            await using var db = _fixture.CreateDbContext();
            var controller = CreateController(db, FakeFirebaseAuthService.ReturningUid(uid), bearerToken: "token");

            var result = await controller.GetForecasts(CancellationToken.None);

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var body = Assert.IsType<GetForecastsResponse>(ok.Value);
            var forecast = Assert.Single(body.Forecasts);
            Assert.Equal(forecastId, forecast.ForecastId);
            Assert.Equal(4, forecast.UserRating);
        }

        [Fact]
        public async Task GetForecasts_OtherUsersRating_DoesNotAppear()
        {
            var uid = UniqueUid();
            var otherUid = UniqueUid();
            await SeedUserAsync(uid);
            await SeedUserAsync(otherUid);
            var serviceId = await SeedServiceAsync();
            var cityId = await SeedCityAsync();
            var citySiteId = await SeedCitySiteAsync(cityId, serviceId);
            await SeedUserCitySiteAsync(uid, citySiteId);
            await SeedUserCitySiteAsync(otherUid, citySiteId);
            var forecastId = await SeedForecastAsync(citySiteId, DateTime.UtcNow.AddHours(1), "CURRENT");
            await SeedRatingAsync(otherUid, forecastId, value: 5);

            await using var db = _fixture.CreateDbContext();
            var controller = CreateController(db, FakeFirebaseAuthService.ReturningUid(uid), bearerToken: "token");

            var result = await controller.GetForecasts(CancellationToken.None);

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var body = Assert.IsType<GetForecastsResponse>(ok.Value);
            var forecast = Assert.Single(body.Forecasts);
            Assert.Null(forecast.UserRating);
        }

        [Fact]
        public async Task GetForecasts_PastForecast_IsExcluded()
        {
            var uid = UniqueUid();
            await SeedUserAsync(uid);
            var serviceId = await SeedServiceAsync();
            var cityId = await SeedCityAsync();
            var citySiteId = await SeedCitySiteAsync(cityId, serviceId);
            await SeedUserCitySiteAsync(uid, citySiteId);
            await SeedForecastAsync(citySiteId, DateTime.UtcNow.AddHours(-1), "CURRENT");

            await using var db = _fixture.CreateDbContext();
            var controller = CreateController(db, FakeFirebaseAuthService.ReturningUid(uid), bearerToken: "token");

            var result = await controller.GetForecasts(CancellationToken.None);

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var body = Assert.IsType<GetForecastsResponse>(ok.Value);
            Assert.Empty(body.Forecasts);
        }

        [Fact]
        public async Task GetForecasts_NotSelectedCitySite_IsExcluded()
        {
            var uid = UniqueUid();
            await SeedUserAsync(uid);
            var serviceId = await SeedServiceAsync();
            var cityId = await SeedCityAsync();
            var citySiteId = await SeedCitySiteAsync(cityId, serviceId);
            // No UserCitySite link for this user - forecast exists but isn't part of their selection.
            await SeedForecastAsync(citySiteId, DateTime.UtcNow.AddHours(1), "CURRENT");

            await using var db = _fixture.CreateDbContext();
            var controller = CreateController(db, FakeFirebaseAuthService.ReturningUid(uid), bearerToken: "token");

            var result = await controller.GetForecasts(CancellationToken.None);

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var body = Assert.IsType<GetForecastsResponse>(ok.Value);
            Assert.Empty(body.Forecasts);
        }

        [Fact]
        public async Task GetForecasts_MultipleForecasts_OrderedByServiceThenCityThenTime()
        {
            var uid = UniqueUid();
            await SeedUserAsync(uid);
            var serviceA = await SeedServiceAsync(name: "OpenWeather");
            var serviceB = await SeedServiceAsync(name: "WeatherAPI", apiEndpoint: "https://example2.test");
            var athens = await SeedCityAsync(name: "Athens", country: "Greece");
            var berlin = await SeedCityAsync(name: "Berlin", country: "Germany", latitude: 52.52m, longitude: 13.40m);

            var athensOpenWeather = await SeedCitySiteAsync(athens, serviceA);
            var berlinOpenWeather = await SeedCitySiteAsync(berlin, serviceA);
            var athensWeatherApi = await SeedCitySiteAsync(athens, serviceB);

            await SeedUserCitySiteAsync(uid, athensOpenWeather);
            await SeedUserCitySiteAsync(uid, berlinOpenWeather);
            await SeedUserCitySiteAsync(uid, athensWeatherApi);

            var now = DateTime.UtcNow;
            await SeedForecastAsync(athensOpenWeather, now.AddHours(2), "CURRENT");
            await SeedForecastAsync(athensOpenWeather, now.AddHours(1), "CURRENT");
            await SeedForecastAsync(berlinOpenWeather, now.AddHours(1), "CURRENT");
            await SeedForecastAsync(athensWeatherApi, now.AddHours(1), "CURRENT");

            await using var db = _fixture.CreateDbContext();
            var controller = CreateController(db, FakeFirebaseAuthService.ReturningUid(uid), bearerToken: "token");

            var result = await controller.GetForecasts(CancellationToken.None);

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var body = Assert.IsType<GetForecastsResponse>(ok.Value);
            Assert.Equal(4, body.Forecasts.Count);
            // Ordered by Service name (OpenWeather < WeatherAPI), then City name (Athens < Berlin), then Timestamp.
            Assert.Equal("OpenWeather", body.Forecasts[0].Service);
            Assert.Equal("Athens", body.Forecasts[0].City);
            Assert.Equal(now.AddHours(1), body.Forecasts[0].Timestamp, TimeSpan.FromSeconds(1));

            Assert.Equal("OpenWeather", body.Forecasts[1].Service);
            Assert.Equal("Athens", body.Forecasts[1].City);
            Assert.Equal(now.AddHours(2), body.Forecasts[1].Timestamp, TimeSpan.FromSeconds(1));

            Assert.Equal("OpenWeather", body.Forecasts[2].Service);
            Assert.Equal("Berlin", body.Forecasts[2].City);

            Assert.Equal("WeatherAPI", body.Forecasts[3].Service);
            Assert.Equal("Athens", body.Forecasts[3].City);
        }

        [Fact]
        public async Task GetForecasts_TwoUsers_EachOnlySeesOwnSelections()
        {
            var uidA = UniqueUid();
            var uidB = UniqueUid();
            await SeedUserAsync(uidA);
            await SeedUserAsync(uidB);
            var serviceId = await SeedServiceAsync();
            var athens = await SeedCityAsync(name: "Athens", country: "Greece");
            var paris = await SeedCityAsync(name: "Paris", country: "France", latitude: 48.85m, longitude: 2.35m);

            var athensSite = await SeedCitySiteAsync(athens, serviceId);
            var parisSite = await SeedCitySiteAsync(paris, serviceId);

            await SeedUserCitySiteAsync(uidA, athensSite);
            await SeedUserCitySiteAsync(uidB, parisSite);

            await SeedForecastAsync(athensSite, DateTime.UtcNow.AddHours(1), "CURRENT");
            await SeedForecastAsync(parisSite, DateTime.UtcNow.AddHours(1), "CURRENT");

            await using (var dbA = _fixture.CreateDbContext())
            {
                var controllerA = CreateController(dbA, FakeFirebaseAuthService.ReturningUid(uidA), bearerToken: "token");
                var resultA = await controllerA.GetForecasts(CancellationToken.None);
                var okA = Assert.IsType<OkObjectResult>(resultA.Result);
                var bodyA = Assert.IsType<GetForecastsResponse>(okA.Value);
                var forecastA = Assert.Single(bodyA.Forecasts);
                Assert.Equal("Athens", forecastA.City);
            }

            await using (var dbB = _fixture.CreateDbContext())
            {
                var controllerB = CreateController(dbB, FakeFirebaseAuthService.ReturningUid(uidB), bearerToken: "token");
                var resultB = await controllerB.GetForecasts(CancellationToken.None);
                var okB = Assert.IsType<OkObjectResult>(resultB.Result);
                var bodyB = Assert.IsType<GetForecastsResponse>(okB.Value);
                var forecastB = Assert.Single(bodyB.Forecasts);
                Assert.Equal("Paris", forecastB.City);
            }
        }
        [Fact]
        public async Task GetForecasts_TwoUsersThreeServicesMixedTimestampsAndRatings_ReturnsOnlyOwnFutureRatedAndUnratedForecasts()
        {
            var uidA = UniqueUid();
            var uidB = UniqueUid();
            await SeedUserAsync(uidA);
            await SeedUserAsync(uidB);
            var cityId = await SeedCityAsync();

            var openWeather = await SeedServiceAsync(name: "OpenWeather");
            var weatherApi = await SeedServiceAsync(name: "WeatherAPI", apiEndpoint: "https://example2.test");
            var accuWeather = await SeedServiceAsync(name: "AccuWeather", apiEndpoint: "https://example3.test");

            var openWeatherSite = await SeedCitySiteAsync(cityId, openWeather);
            var weatherApiSite = await SeedCitySiteAsync(cityId, weatherApi);
            var accuWeatherSite = await SeedCitySiteAsync(cityId, accuWeather);

            // User A watches OpenWeather and WeatherAPI for this city; User B watches AccuWeather only.
            await SeedUserCitySiteAsync(uidA, openWeatherSite);
            await SeedUserCitySiteAsync(uidA, weatherApiSite);
            await SeedUserCitySiteAsync(uidB, accuWeatherSite);

            var openWeatherTimestamp = DateTime.UtcNow.AddHours(1);
            var weatherApiTimestamp = DateTime.UtcNow.AddHours(2);
            var accuWeatherTimestamp = DateTime.UtcNow.AddHours(1);

            var openWeatherFuture = await SeedForecastAsync(
                openWeatherSite, openWeatherTimestamp, "CURRENT", temperature: 22m, humidity: 55m, windSpeed: 8m, dangerFlag: false);
            await SeedForecastAsync(openWeatherSite, DateTime.UtcNow.AddHours(-1), "CURRENT"); // past - must be excluded
            var weatherApiFuture = await SeedForecastAsync(
                weatherApiSite, weatherApiTimestamp, "CURRENT", temperature: 18m, humidity: 70m, windSpeed: 15m, dangerFlag: false);
            var accuWeatherFuture = await SeedForecastAsync(
                accuWeatherSite, accuWeatherTimestamp, "CURRENT", temperature: 30m, humidity: 40m, windSpeed: 5m, dangerFlag: true);

            await SeedRatingAsync(uidA, openWeatherFuture, value: 5); // OpenWeather rated by A, WeatherAPI left unrated by A
            await SeedRatingAsync(uidB, accuWeatherFuture, value: 3);

            await using (var dbA = _fixture.CreateDbContext())
            {
                var controllerA = CreateController(dbA, FakeFirebaseAuthService.ReturningUid(uidA), bearerToken: "token");
                var resultA = await controllerA.GetForecasts(CancellationToken.None);
                var okA = Assert.IsType<OkObjectResult>(resultA.Result);
                var bodyA = Assert.IsType<GetForecastsResponse>(okA.Value);

                Assert.Equal(2, bodyA.Forecasts.Count);
                var openWeatherForA = Assert.Single(bodyA.Forecasts, f => f.Service == "OpenWeather");
                Assert.Equal(openWeatherFuture, openWeatherForA.ForecastId);
                Assert.Equal(openWeatherTimestamp, openWeatherForA.Timestamp, TimeSpan.FromSeconds(1));
                Assert.Equal(22m, openWeatherForA.Temperature);
                Assert.Equal(55m, openWeatherForA.Humidity);
                Assert.Equal(8m, openWeatherForA.WindSpeed);
                Assert.False(openWeatherForA.DangerFlag);
                Assert.Equal(5, openWeatherForA.UserRating);

                var weatherApiForA = Assert.Single(bodyA.Forecasts, f => f.Service == "WeatherAPI");
                Assert.Equal(weatherApiFuture, weatherApiForA.ForecastId);
                Assert.Equal(weatherApiTimestamp, weatherApiForA.Timestamp, TimeSpan.FromSeconds(1));
                Assert.Equal(18m, weatherApiForA.Temperature);
                Assert.Equal(70m, weatherApiForA.Humidity);
                Assert.Equal(15m, weatherApiForA.WindSpeed);
                Assert.False(weatherApiForA.DangerFlag);
                Assert.Null(weatherApiForA.UserRating);
            }

            await using (var dbB = _fixture.CreateDbContext())
            {
                var controllerB = CreateController(dbB, FakeFirebaseAuthService.ReturningUid(uidB), bearerToken: "token");
                var resultB = await controllerB.GetForecasts(CancellationToken.None);
                var okB = Assert.IsType<OkObjectResult>(resultB.Result);
                var bodyB = Assert.IsType<GetForecastsResponse>(okB.Value);

                var accuWeatherForB = Assert.Single(bodyB.Forecasts);
                Assert.Equal("AccuWeather", accuWeatherForB.Service);
                Assert.Equal(accuWeatherFuture, accuWeatherForB.ForecastId);
                Assert.Equal(accuWeatherTimestamp, accuWeatherForB.Timestamp, TimeSpan.FromSeconds(1));
                Assert.Equal(30m, accuWeatherForB.Temperature);
                Assert.Equal(40m, accuWeatherForB.Humidity);
                Assert.Equal(5m, accuWeatherForB.WindSpeed);
                Assert.True(accuWeatherForB.DangerFlag);
                Assert.Equal(3, accuWeatherForB.UserRating);
            }
        }

        // --- RateForecast (UC7 main flow / A1) ---

        [Fact]
        public async Task RateForecast_MissingAuthorizationHeader_ReturnsUnauthorized()
        {
            await using var db = _fixture.CreateDbContext();
            var controller = CreateController(db, FakeFirebaseAuthService.ReturningUid("irrelevant"), bearerToken: null);

            var result = await controller.RateForecast(1, new RateForecastRequest(4), CancellationToken.None);

            Assert.IsType<UnauthorizedResult>(result);
        }

        [Fact]
        public async Task RateForecast_ForecastDoesNotExist_ReturnsNotFoundProblem()
        {
            var uid = UniqueUid();
            await SeedUserAsync(uid);
            await using var db = _fixture.CreateDbContext();
            var controller = CreateController(db, FakeFirebaseAuthService.ReturningUid(uid), bearerToken: "token");

            var result = await controller.RateForecast(999, new RateForecastRequest(4), CancellationToken.None);

            var problem = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status404NotFound, problem.StatusCode);

            await using var verifyDb = _fixture.CreateDbContext();
            Assert.Equal(0, await verifyDb.Ratings.CountAsync());
        }

        [Fact]
        public async Task RateForecast_NewRating_CreatesRatingRow()
        {
            var uid = UniqueUid();
            await SeedUserAsync(uid);
            var serviceId = await SeedServiceAsync();
            var cityId = await SeedCityAsync();
            var citySiteId = await SeedCitySiteAsync(cityId, serviceId);
            var forecastId = await SeedForecastAsync(citySiteId, DateTime.UtcNow.AddHours(1), "CURRENT");

            await using var db = _fixture.CreateDbContext();
            var controller = CreateController(db, FakeFirebaseAuthService.ReturningUid(uid), bearerToken: "token");

            var result = await controller.RateForecast(forecastId, new RateForecastRequest(4), CancellationToken.None);

            Assert.IsType<OkResult>(result);
            
            await using var verifyDb = _fixture.CreateDbContext();
            var rating = await verifyDb.Ratings.SingleAsync(r => r.ForecastId == forecastId);
            Assert.Equal(uid, rating.UserId);
            Assert.Equal(4, rating.Value);
            Assert.Equal(rating.CreatedAt, rating.UpdatedAt);
            Assert.Equal(rating.ForecastId, forecastId);
        }

        [Fact]
        public async Task RateForecast_CalledAgainWithDifferentValue_UpdatesExistingRow()
        {
            var uid = UniqueUid();
            await SeedUserAsync(uid);
            var serviceId = await SeedServiceAsync();
            var cityId = await SeedCityAsync();
            var citySiteId = await SeedCitySiteAsync(cityId, serviceId);
            var forecastId = await SeedForecastAsync(citySiteId, DateTime.UtcNow.AddHours(1), "CURRENT");

            await using (var firstCallDb = _fixture.CreateDbContext())
            {
                var controller = CreateController(firstCallDb, FakeFirebaseAuthService.ReturningUid(uid), bearerToken: "token");
                await controller.RateForecast(forecastId, new RateForecastRequest(3), CancellationToken.None);
            }

            DateTime originalCreatedAt;
            await using (var readDb = _fixture.CreateDbContext())
            {
                originalCreatedAt = (await readDb.Ratings.SingleAsync(r => r.ForecastId == forecastId)).CreatedAt;
            }

            await using (var secondCallDb = _fixture.CreateDbContext())
            {
                var controller = CreateController(secondCallDb, FakeFirebaseAuthService.ReturningUid(uid), bearerToken: "token");
                var result = await controller.RateForecast(forecastId, new RateForecastRequest(5), CancellationToken.None);
                Assert.IsType<OkResult>(result);
            }

            await using var verifyDb = _fixture.CreateDbContext();
            Assert.Equal(1, await verifyDb.Ratings.CountAsync());
            var rating = await verifyDb.Ratings.SingleAsync(r => r.ForecastId == forecastId);
            Assert.Equal(uid, rating.UserId);
            Assert.Equal(5, rating.Value);
            Assert.Equal(originalCreatedAt, rating.CreatedAt);
            Assert.True(rating.UpdatedAt > rating.CreatedAt);
            Assert.Equal(rating.ForecastId, forecastId);
        }

        // --- RemoveRating (UC7 A2) ---

        [Fact]
        public async Task RemoveRating_MissingAuthorizationHeader_ReturnsUnauthorized()
        {
            await using var db = _fixture.CreateDbContext();
            var controller = CreateController(db, FakeFirebaseAuthService.ReturningUid("irrelevant"), bearerToken: null);

            var result = await controller.RemoveRating(1, CancellationToken.None);

            Assert.IsType<UnauthorizedResult>(result);
        }

        [Fact]
        public async Task RemoveRating_ExistingRating_DeletesRow()
        {
            var uid = UniqueUid();
            await SeedUserAsync(uid);
            var serviceId = await SeedServiceAsync();
            var cityId = await SeedCityAsync();
            var citySiteId = await SeedCitySiteAsync(cityId, serviceId);
            var forecastId = await SeedForecastAsync(citySiteId, DateTime.UtcNow.AddHours(1), "CURRENT");
            await SeedRatingAsync(uid, forecastId, value: 3);

            await using var db = _fixture.CreateDbContext();
            var controller = CreateController(db, FakeFirebaseAuthService.ReturningUid(uid), bearerToken: "token");

            var result = await controller.RemoveRating(forecastId, CancellationToken.None);

            Assert.IsType<OkResult>(result);

            await using var verifyDb = _fixture.CreateDbContext();
            Assert.Equal(0, await verifyDb.Ratings.CountAsync());
        }

        [Fact]
        public async Task RemoveRating_NoExistingRating_IsIdempotent()
        {
            var uid = UniqueUid();
            await SeedUserAsync(uid);
            await using var db = _fixture.CreateDbContext();
            var controller = CreateController(db, FakeFirebaseAuthService.ReturningUid(uid), bearerToken: "token");

            var result = await controller.RemoveRating(999, CancellationToken.None);

            Assert.IsType<OkResult>(result);
        }

        [Fact]
        public async Task RemoveRating_OtherUsersRating_IsNotAffected()
        {
            var uid = UniqueUid();
            var otherUid = UniqueUid();
            await SeedUserAsync(uid);
            await SeedUserAsync(otherUid);
            var serviceId = await SeedServiceAsync();
            var cityId = await SeedCityAsync();
            var citySiteId = await SeedCitySiteAsync(cityId, serviceId);
            var forecastId = await SeedForecastAsync(citySiteId, DateTime.UtcNow.AddHours(1), "CURRENT");
            await SeedRatingAsync(otherUid, forecastId, value: 2);

            await using var db = _fixture.CreateDbContext();
            var controller = CreateController(db, FakeFirebaseAuthService.ReturningUid(uid), bearerToken: "token");

            var result = await controller.RemoveRating(forecastId, CancellationToken.None);

            Assert.IsType<OkResult>(result);

            await using var verifyDb = _fixture.CreateDbContext();
            var remainingRating = await verifyDb.Ratings.SingleAsync(r => r.ForecastId == forecastId);
            Assert.Equal(otherUid, remainingRating.UserId);
            Assert.Single(verifyDb.Ratings);
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

        private async Task<int> SeedCityAsync(
            string name = "Athens", string country = "Greece", decimal latitude = 37.98m, decimal longitude = 23.72m)
        {
            await using var db = _fixture.CreateDbContext();
            var city = new City { Name = name, Country = country, Latitude = latitude, Longitude = longitude };
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

        private async Task SeedUserCitySiteAsync(string uid, int citySiteId)
        {
            await using var db = _fixture.CreateDbContext();
            db.UserCitySites.Add(new UserCitySite { UserId = uid, CitySiteId = citySiteId, AddedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }

        private async Task<int> SeedForecastAsync(
            int citySiteId,
            DateTime timestamp,
            string type,
            decimal temperature = 20m,
            decimal humidity = 50m,
            decimal windSpeed = 10m,
            bool dangerFlag = false)
        {
            await using var db = _fixture.CreateDbContext();
            var forecast = new Forecast
            {
                CitySiteId = citySiteId,
                Timestamp = timestamp,
                Type = type,
                Temperature = temperature,
                Humidity = humidity,
                WindSpeed = windSpeed,
                DangerFlag = dangerFlag,
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

        private static ForecastsController CreateController(
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
