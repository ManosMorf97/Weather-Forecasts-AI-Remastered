using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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
    // UC10 (View Aggregated Forecast) / UC12 (Calculate Aggregates).
    [Collection(TestCollections.SqlServer)]
    public class AggregatedForecastsControllerLogicTests : IAsyncLifetime
    {
        private readonly SqlServerFixture _fixture;

        public AggregatedForecastsControllerLogicTests(SqlServerFixture fixture)
        {
            _fixture = fixture;
        }

        public Task InitializeAsync() => _fixture.ResetDatabaseAsync();

        public Task DisposeAsync() => Task.CompletedTask;

        [Fact]
        public async Task GetAggregatedForecasts_MissingAuthorizationHeader_ReturnsUnauthorized()
        {
            await using var db = _fixture.CreateDbContext();
            var controller = CreateController(db, FakeAppwriteAuthService.ReturningUid("irrelevant"), bearerToken: null);

            var result = await controller.GetAggregatedForecasts(CancellationToken.None);

            Assert.IsType<UnauthorizedResult>(result.Result);
        }

        [Fact]
        public async Task GetAggregatedForecasts_UserHasNoSelections_ReturnsEmpty()
        {
            var uid = UniqueUid();
            await SeedUserAsync(uid);
            await using var db = _fixture.CreateDbContext();
            var controller = CreateController(db, FakeAppwriteAuthService.ReturningUid(uid), bearerToken: "token");

            var result = await controller.GetAggregatedForecasts(CancellationToken.None);

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var body = Assert.IsType<GetAggregatedForecastsResponse>(ok.Value);
            Assert.Empty(body.Forecasts);
            Assert.Empty(body.ServiceMetadata);
        }

        // --- A1: single selected service for a city ---

        [Fact]
        public async Task GetAggregatedForecasts_SingleServiceSelected_ReturnsItsForecastWithAggregationNotApplicable()
        {
            var uid = UniqueUid();
            await SeedUserAsync(uid);
            var serviceId = await SeedServiceAsync();
            var cityId = await SeedCityAsync();
            var citySiteId = await SeedCitySiteAsync(cityId, serviceId);
            await SeedUserCitySiteAsync(uid, citySiteId);
            var timestamp = DateTime.UtcNow.AddHours(1);
            var forecastId = await SeedForecastAsync(citySiteId, timestamp, temperature: 25m, humidity: 50m, windSpeed: 10m, dangerFlag: false);

            await using var db = _fixture.CreateDbContext();
            var controller = CreateController(db, FakeAppwriteAuthService.ReturningUid(uid), bearerToken: "token");

            var result = await controller.GetAggregatedForecasts(CancellationToken.None);

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var body = Assert.IsType<GetAggregatedForecastsResponse>(ok.Value);
            var forecast = Assert.Single(body.Forecasts);
            Assert.Equal(forecastId, forecast.ForecastId);
            Assert.Equal("Athens", forecast.City);
            Assert.Equal("Greece", forecast.Country);
            Assert.Equal("OpenWeather", forecast.Service);
            Assert.Equal(timestamp, forecast.Timestamp, TimeSpan.FromSeconds(1));
            Assert.Equal(25m, forecast.Temperature);
            Assert.Equal(50m, forecast.Humidity);
            Assert.Equal(10m, forecast.WindSpeed);
            Assert.False(forecast.DangerFlag);

            var metadata = Assert.Single(body.ServiceMetadata);
            Assert.Equal("Athens", metadata.City);
            Assert.Equal("OpenWeather", metadata.Service);
            Assert.False(metadata.AggregationApplicable);
            Assert.False(metadata.IsTie);
            Assert.Null(metadata.IsUnratedSelection);
        }

        // --- Main flow: two services, one has the higher average rating ---

        [Fact]
        public async Task GetAggregatedForecasts_TwoServicesOneHigherRated_ReturnsHigherRatedServicesForecast()
        {
            var uid = UniqueUid();
            var raterA = UniqueUid();
            var raterB = UniqueUid();
            await SeedUserAsync(uid);
            await SeedUserAsync(raterA);
            await SeedUserAsync(raterB);
            var openWeather = await SeedServiceAsync(name: "OpenWeather");
            var weatherApi = await SeedServiceAsync(name: "WeatherAPI", apiEndpoint: "https://example2.test");
            var cityId = await SeedCityAsync();
            var openWeatherCitySite = await SeedCitySiteAsync(cityId, openWeather);
            var weatherApiCitySite = await SeedCitySiteAsync(cityId, weatherApi);
            await SeedUserCitySiteAsync(uid, openWeatherCitySite);
            await SeedUserCitySiteAsync(uid, weatherApiCitySite);

            var openWeatherTimestamp = DateTime.UtcNow.AddHours(1);
            var openWeatherForecastId = await SeedForecastAsync(openWeatherCitySite, openWeatherTimestamp);
            var weatherApiForecastId = await SeedForecastAsync(weatherApiCitySite, DateTime.UtcNow.AddHours(1));

            // OpenWeather: avg 5 (>= 2 ratings). WeatherAPI: avg 2 (>= 2 ratings). OpenWeather should win -
            // and the rating is a global signal, not the requesting user's own opinion (uid never rates).
            await SeedRatingAsync(raterA, openWeatherForecastId, value: 5);
            await SeedRatingAsync(raterB, openWeatherForecastId, value: 5);
            await SeedRatingAsync(raterA, weatherApiForecastId, value: 2);
            await SeedRatingAsync(raterB, weatherApiForecastId, value: 2);

            await using var db = _fixture.CreateDbContext();
            var controller = CreateController(db, FakeAppwriteAuthService.ReturningUid(uid), bearerToken: "token");

            var result = await controller.GetAggregatedForecasts(CancellationToken.None);

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var body = Assert.IsType<GetAggregatedForecastsResponse>(ok.Value);
            var forecast = Assert.Single(body.Forecasts);
            Assert.Equal(openWeatherForecastId, forecast.ForecastId);
            Assert.Equal("OpenWeather", forecast.Service);
            Assert.Equal(openWeatherTimestamp, forecast.Timestamp, TimeSpan.FromSeconds(1));

            var metadata = Assert.Single(body.ServiceMetadata);
            Assert.Equal("Athens", metadata.City);
            Assert.Equal("OpenWeather", metadata.Service);
            Assert.Equal(5m, metadata.AverageRating);
            Assert.Equal(2, metadata.RatingCount);
            Assert.True(metadata.AggregationApplicable);
            Assert.False(metadata.IsTie);
            Assert.False(metadata.IsUnratedSelection);
        }

        // --- A2: tie in ratings, resolved alphabetically ---

        [Fact]
        public async Task GetAggregatedForecasts_TiedAverageRatings_PicksServiceClosestToAAlphabetically()
        {
            var uid = UniqueUid();
            var raterA = UniqueUid();
            var raterB = UniqueUid();
            await SeedUserAsync(uid);
            await SeedUserAsync(raterA);
            await SeedUserAsync(raterB);
            var accuWeather = await SeedServiceAsync(name: "AccuWeather", apiEndpoint: "https://example3.test");
            var weatherApi = await SeedServiceAsync(name: "WeatherAPI", apiEndpoint: "https://example2.test");
            var cityId = await SeedCityAsync();
            var accuWeatherCitySite = await SeedCitySiteAsync(cityId, accuWeather);
            var weatherApiCitySite = await SeedCitySiteAsync(cityId, weatherApi);
            await SeedUserCitySiteAsync(uid, accuWeatherCitySite);
            await SeedUserCitySiteAsync(uid, weatherApiCitySite);

            var accuWeatherForecastId = await SeedForecastAsync(accuWeatherCitySite, DateTime.UtcNow.AddHours(1));
            var weatherApiForecastId = await SeedForecastAsync(weatherApiCitySite, DateTime.UtcNow.AddHours(1));

            // Both average exactly 4 with 2 ratings each - a genuine tie.
            await SeedRatingAsync(raterA, accuWeatherForecastId, value: 4);
            await SeedRatingAsync(raterB, accuWeatherForecastId, value: 4);
            await SeedRatingAsync(raterA, weatherApiForecastId, value: 4);
            await SeedRatingAsync(raterB, weatherApiForecastId, value: 4);

            await using var db = _fixture.CreateDbContext();
            var controller = CreateController(db, FakeAppwriteAuthService.ReturningUid(uid), bearerToken: "token");

            var result = await controller.GetAggregatedForecasts(CancellationToken.None);

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var body = Assert.IsType<GetAggregatedForecastsResponse>(ok.Value);
            var forecast = Assert.Single(body.Forecasts);
            Assert.Equal("AccuWeather", forecast.Service);
            Assert.Equal(accuWeatherForecastId, forecast.ForecastId);
            Assert.Equal("Athens", forecast.City);

            var metadata = Assert.Single(body.ServiceMetadata);
            Assert.Equal("AccuWeather", metadata.Service);
            Assert.Equal(4m, metadata.AverageRating);
            Assert.True(metadata.IsTie);
            Assert.False(metadata.IsUnratedSelection);
        }

        // --- A1: no service has enough ratings to be reliable, resolved alphabetically ---

        [Fact]
        public async Task GetAggregatedForecasts_NoServiceHasTwoRatings_PicksServiceClosestToAAndFlagsUnrated()
        {
            var uid = UniqueUid();
            var rater = UniqueUid();
            await SeedUserAsync(uid);
            await SeedUserAsync(rater);
            var accuWeather = await SeedServiceAsync(name: "AccuWeather", apiEndpoint: "https://example3.test");
            var weatherApi = await SeedServiceAsync(name: "WeatherAPI", apiEndpoint: "https://example2.test");
            var cityId = await SeedCityAsync();
            var accuWeatherSite = await SeedCitySiteAsync(cityId, accuWeather);
            var weatherApiSite = await SeedCitySiteAsync(cityId, weatherApi);
            await SeedUserCitySiteAsync(uid, accuWeatherSite);
            await SeedUserCitySiteAsync(uid, weatherApiSite);

            var accuWeatherForecastId = await SeedForecastAsync(accuWeatherSite, DateTime.UtcNow.AddHours(1));
            var weatherApiForecastId = await SeedForecastAsync(weatherApiSite, DateTime.UtcNow.AddHours(1));

            // WeatherAPI has a single 5-star rating (higher, but unreliable with < 2 ratings) -
            // AccuWeather has none. Neither qualifies, so the alphabetically-first wins.
            await SeedRatingAsync(rater, weatherApiForecastId, value: 5);

            await using var db = _fixture.CreateDbContext();
            var controller = CreateController(db, FakeAppwriteAuthService.ReturningUid(uid), bearerToken: "token");

            var result = await controller.GetAggregatedForecasts(CancellationToken.None);

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var body = Assert.IsType<GetAggregatedForecastsResponse>(ok.Value);
            var forecast = Assert.Single(body.Forecasts);
            Assert.Equal("AccuWeather", forecast.Service);
            Assert.Equal(accuWeatherForecastId, forecast.ForecastId);
            Assert.Equal("Athens", forecast.City);

            var metadata = Assert.Single(body.ServiceMetadata);
            Assert.Equal("AccuWeather", metadata.Service);
            Assert.Null(metadata.AverageRating);
            Assert.Equal(0, metadata.RatingCount);
            Assert.True(metadata.AggregationApplicable);
            Assert.False(metadata.IsTie);
            Assert.True(metadata.IsUnratedSelection);
        }

        // --- Fallback: highest-rated service has no upcoming forecast ---

        [Fact]
        public async Task GetAggregatedForecasts_TopRatedServiceHasNoUpcomingForecast_FallsBackToNextRatedService()
        {
            var uid = UniqueUid();
            var raterA = UniqueUid();
            var raterB = UniqueUid();
            await SeedUserAsync(uid);
            await SeedUserAsync(raterA);
            await SeedUserAsync(raterB);
            var openWeather = await SeedServiceAsync(name: "OpenWeather");
            var weatherApi = await SeedServiceAsync(name: "WeatherAPI", apiEndpoint: "https://example2.test");
            var cityId = await SeedCityAsync();
            var openWeatherCitySite = await SeedCitySiteAsync(cityId, openWeather);
            var weatherApiCitySite = await SeedCitySiteAsync(cityId, weatherApi);
            await SeedUserCitySiteAsync(uid, openWeatherCitySite);
            await SeedUserCitySiteAsync(uid, weatherApiCitySite);

            // OpenWeather is rated higher (avg 5) but only has a past forecast - nothing to show.
            // WeatherAPI is rated lower (avg 3) but has an upcoming forecast, so it should win.
            var openWeatherPastForecastId = await SeedForecastAsync(openWeatherCitySite, DateTime.UtcNow.AddHours(-1));
            var weatherApiTimestamp = DateTime.UtcNow.AddHours(1);
            var weatherApiForecastId = await SeedForecastAsync(weatherApiCitySite, weatherApiTimestamp);

            await SeedRatingAsync(raterA, openWeatherPastForecastId, value: 5);
            await SeedRatingAsync(raterB, openWeatherPastForecastId, value: 5);
            await SeedRatingAsync(raterA, weatherApiForecastId, value: 3);
            await SeedRatingAsync(raterB, weatherApiForecastId, value: 3);

            await using var db = _fixture.CreateDbContext();
            var controller = CreateController(db, FakeAppwriteAuthService.ReturningUid(uid), bearerToken: "token");

            var result = await controller.GetAggregatedForecasts(CancellationToken.None);

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var body = Assert.IsType<GetAggregatedForecastsResponse>(ok.Value);
            var forecast = Assert.Single(body.Forecasts);
            Assert.Equal("WeatherAPI", forecast.Service);
            Assert.Equal(weatherApiForecastId, forecast.ForecastId);
            Assert.Equal(weatherApiTimestamp, forecast.Timestamp, TimeSpan.FromSeconds(1));

            var metadata = Assert.Single(body.ServiceMetadata);
            Assert.Equal("WeatherAPI", metadata.Service);
            Assert.Equal(3m, metadata.AverageRating);
            Assert.True(metadata.AggregationApplicable);
            Assert.False(metadata.IsTie);
            Assert.False(metadata.IsUnratedSelection);
        }

        [Fact]
        public async Task GetAggregatedForecasts_TiedServiceHasNoUpcomingForecast_IsNotCountedTowardIsTie()
        {
            var uid = UniqueUid();
            var raterA = UniqueUid();
            var raterB = UniqueUid();
            await SeedUserAsync(uid);
            await SeedUserAsync(raterA);
            await SeedUserAsync(raterB);
            var accuWeather = await SeedServiceAsync(name: "AccuWeather", apiEndpoint: "https://example3.test");
            var openWeather = await SeedServiceAsync(name: "OpenWeather");
            var cityId = await SeedCityAsync();
            var accuWeatherCitySite = await SeedCitySiteAsync(cityId, accuWeather);
            var openWeatherCitySite = await SeedCitySiteAsync(cityId, openWeather);
            await SeedUserCitySiteAsync(uid, accuWeatherCitySite);
            await SeedUserCitySiteAsync(uid, openWeatherCitySite);

            // AccuWeather and OpenWeather are both rated avg 5 (a genuine tie) - but AccuWeather's
            // only forecast is in the past, so it's excluded from the running before ties are even
            // evaluated. IsTie must reflect only currently-displayable candidates, so it should come
            // back false here, even though a tie exists in the underlying all-time rating data.
            var accuWeatherPastForecastId = await SeedForecastAsync(accuWeatherCitySite, DateTime.UtcNow.AddHours(-1));
            var openWeatherTimestamp = DateTime.UtcNow.AddHours(1);
            var openWeatherForecastId = await SeedForecastAsync(openWeatherCitySite, openWeatherTimestamp);

            await SeedRatingAsync(raterA, accuWeatherPastForecastId, value: 5);
            await SeedRatingAsync(raterB, accuWeatherPastForecastId, value: 5);
            await SeedRatingAsync(raterA, openWeatherForecastId, value: 5);
            await SeedRatingAsync(raterB, openWeatherForecastId, value: 5);

            await using var db = _fixture.CreateDbContext();
            var controller = CreateController(db, FakeAppwriteAuthService.ReturningUid(uid), bearerToken: "token");

            var result = await controller.GetAggregatedForecasts(CancellationToken.None);

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var body = Assert.IsType<GetAggregatedForecastsResponse>(ok.Value);
            var forecast = Assert.Single(body.Forecasts);
            Assert.Equal("OpenWeather", forecast.Service);
            Assert.Equal(openWeatherForecastId, forecast.ForecastId);
            Assert.Equal(openWeatherTimestamp, forecast.Timestamp, TimeSpan.FromSeconds(1));

            var metadata = Assert.Single(body.ServiceMetadata);
            Assert.Equal("OpenWeather", metadata.Service);
            Assert.Equal(5m, metadata.AverageRating);
            Assert.True(metadata.AggregationApplicable);
            Assert.False(metadata.IsTie);
            Assert.False(metadata.IsUnratedSelection);
        }

        [Fact]
        public async Task GetAggregatedForecasts_NoRatedServiceHasUpcomingForecast_FallsBackToUnratedServiceWithData()
        {
            var uid = UniqueUid();
            var raterA = UniqueUid();
            var raterB = UniqueUid();
            await SeedUserAsync(uid);
            await SeedUserAsync(raterA);
            await SeedUserAsync(raterB);
            var openWeather = await SeedServiceAsync(name: "OpenWeather");
            var weatherApi = await SeedServiceAsync(name: "WeatherAPI", apiEndpoint: "https://example2.test");
            var cityId = await SeedCityAsync();
            var openWeatherCitySite = await SeedCitySiteAsync(cityId, openWeather);
            var weatherApiCitySite = await SeedCitySiteAsync(cityId, weatherApi);
            await SeedUserCitySiteAsync(uid, openWeatherCitySite);
            await SeedUserCitySiteAsync(uid, weatherApiCitySite);

            // OpenWeather is the only rated (and reliably so) service, but its only forecast is past.
            // WeatherAPI has no ratings at all but does have an upcoming forecast - it should win.
            var openWeatherPastForecastId = await SeedForecastAsync(openWeatherCitySite, DateTime.UtcNow.AddHours(-1));
            var weatherApiTimestamp = DateTime.UtcNow.AddHours(1);
            var weatherApiForecastId = await SeedForecastAsync(weatherApiCitySite, weatherApiTimestamp);

            await SeedRatingAsync(raterA, openWeatherPastForecastId, value: 5);
            await SeedRatingAsync(raterB, openWeatherPastForecastId, value: 5);

            await using var db = _fixture.CreateDbContext();
            var controller = CreateController(db, FakeAppwriteAuthService.ReturningUid(uid), bearerToken: "token");

            var result = await controller.GetAggregatedForecasts(CancellationToken.None);

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var body = Assert.IsType<GetAggregatedForecastsResponse>(ok.Value);
            var forecast = Assert.Single(body.Forecasts);
            Assert.Equal("WeatherAPI", forecast.Service);
            Assert.Equal(weatherApiForecastId, forecast.ForecastId);

            var metadata = Assert.Single(body.ServiceMetadata);
            Assert.Equal("WeatherAPI", metadata.Service);
            Assert.Null(metadata.AverageRating);
            Assert.Equal(0, metadata.RatingCount);
            Assert.True(metadata.AggregationApplicable);
            Assert.False(metadata.IsTie);
            Assert.True(metadata.IsUnratedSelection);
        }

        [Fact]
        public async Task GetAggregatedForecasts_NoSelectedServiceHasUpcomingForecast_IsOmittedEvenWithRatingHistory()
        {
            var uid = UniqueUid();
            var rater = UniqueUid();
            await SeedUserAsync(uid);
            await SeedUserAsync(rater);
            var serviceId = await SeedServiceAsync();
            var cityId = await SeedCityAsync();
            var citySiteId = await SeedCitySiteAsync(cityId, serviceId);
            await SeedUserCitySiteAsync(uid, citySiteId);

            // Rating history exists, but every forecast is in the past - nothing upcoming to show.
            var pastForecastId = await SeedForecastAsync(citySiteId, DateTime.UtcNow.AddHours(-1));
            await SeedRatingAsync(rater, pastForecastId, value: 5);

            await using var db = _fixture.CreateDbContext();
            var controller = CreateController(db, FakeAppwriteAuthService.ReturningUid(uid), bearerToken: "token");

            var result = await controller.GetAggregatedForecasts(CancellationToken.None);

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var body = Assert.IsType<GetAggregatedForecastsResponse>(ok.Value);
            Assert.Empty(body.Forecasts);
            Assert.Empty(body.ServiceMetadata);
        }

        // --- E1: a selected city has no forecast data from any selected service ---

        [Fact]
        public async Task GetAggregatedForecasts_CityHasNoForecastData_IsOmittedButOtherCitiesStillReturn()
        {
            var uid = UniqueUid();
            await SeedUserAsync(uid);
            var serviceId = await SeedServiceAsync();
            var athens = await SeedCityAsync(name: "Athens", country: "Greece");
            var paris = await SeedCityAsync(name: "Paris", country: "France", latitude: 48.85m, longitude: 2.35m);
            var athensSite = await SeedCitySiteAsync(athens, serviceId);
            var parisSite = await SeedCitySiteAsync(paris, serviceId);
            await SeedUserCitySiteAsync(uid, athensSite);
            await SeedUserCitySiteAsync(uid, parisSite);

            // Only Paris gets a forecast - Athens has none at all.
            var parisForecastId = await SeedForecastAsync(parisSite, DateTime.UtcNow.AddHours(1));

            await using var db = _fixture.CreateDbContext();
            var controller = CreateController(db, FakeAppwriteAuthService.ReturningUid(uid), bearerToken: "token");

            var result = await controller.GetAggregatedForecasts(CancellationToken.None);

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var body = Assert.IsType<GetAggregatedForecastsResponse>(ok.Value);
            var forecast = Assert.Single(body.Forecasts);
            Assert.Equal("Paris", forecast.City);
            Assert.Equal(parisForecastId, forecast.ForecastId);
            Assert.Single(body.ServiceMetadata);
            Assert.DoesNotContain(body.ServiceMetadata, metadata => metadata.City == "Athens");
            Assert.DoesNotContain(body.Forecasts, forecast => forecast.City == "Athens");
        }

        [Fact]
        public async Task GetAggregatedForecasts_PastForecast_IsExcluded()
        {
            var uid = UniqueUid();
            await SeedUserAsync(uid);
            var serviceId = await SeedServiceAsync();
            var cityId = await SeedCityAsync();
            var citySiteId = await SeedCitySiteAsync(cityId, serviceId);
            await SeedUserCitySiteAsync(uid, citySiteId);
            await SeedForecastAsync(citySiteId, DateTime.UtcNow.AddHours(-1));

            await using var db = _fixture.CreateDbContext();
            var controller = CreateController(db, FakeAppwriteAuthService.ReturningUid(uid), bearerToken: "token");

            var result = await controller.GetAggregatedForecasts(CancellationToken.None);

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var body = Assert.IsType<GetAggregatedForecastsResponse>(ok.Value);
            Assert.Empty(body.Forecasts);
            Assert.Empty(body.ServiceMetadata);
        }

        [Fact]
        public async Task GetAggregatedForecasts_NotSelectedCitySite_IsExcluded()
        {
            var uid = UniqueUid();
            await SeedUserAsync(uid);
            var serviceId = await SeedServiceAsync();
            var cityId = await SeedCityAsync();
            var citySiteId = await SeedCitySiteAsync(cityId, serviceId);
            // No UserCitySite link for this user - forecast exists but isn't part of their selection.
            await SeedForecastAsync(citySiteId, DateTime.UtcNow.AddHours(1));

            await using var db = _fixture.CreateDbContext();
            var controller = CreateController(db, FakeAppwriteAuthService.ReturningUid(uid), bearerToken: "token");

            var result = await controller.GetAggregatedForecasts(CancellationToken.None);

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var body = Assert.IsType<GetAggregatedForecastsResponse>(ok.Value);
            Assert.Empty(body.Forecasts);
            Assert.Empty(body.ServiceMetadata);
        }

        [Fact]
        public async Task GetAggregatedForecasts_WinningServiceHasMultipleUpcomingForecasts_ReturnsAllOrderedByTimestamp()
        {
            var uid = UniqueUid();
            await SeedUserAsync(uid);
            var serviceId = await SeedServiceAsync();
            var cityId = await SeedCityAsync();
            var citySiteId = await SeedCitySiteAsync(cityId, serviceId);
            await SeedUserCitySiteAsync(uid, citySiteId);
            var now = DateTime.UtcNow;
            var laterForecastId = await SeedForecastAsync(citySiteId, now.AddHours(3));
            var soonerForecastId = await SeedForecastAsync(citySiteId, now.AddHours(1));

            await using var db = _fixture.CreateDbContext();
            var controller = CreateController(db, FakeAppwriteAuthService.ReturningUid(uid), bearerToken: "token");

            var result = await controller.GetAggregatedForecasts(CancellationToken.None);

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var body = Assert.IsType<GetAggregatedForecastsResponse>(ok.Value);
            Assert.Equal(2, body.Forecasts.Count);
            Assert.Equal("Athens", body.Forecasts[0].City);
            Assert.Equal("OpenWeather", body.Forecasts[0].Service);
            Assert.Equal(soonerForecastId, body.Forecasts[0].ForecastId);
            Assert.Equal(now.AddHours(1), body.Forecasts[0].Timestamp, TimeSpan.FromSeconds(1));
            Assert.Equal("Athens", body.Forecasts[1].City);
            Assert.Equal("OpenWeather", body.Forecasts[1].Service);
            Assert.Equal(laterForecastId, body.Forecasts[1].ForecastId);
            Assert.Equal(now.AddHours(3), body.Forecasts[1].Timestamp, TimeSpan.FromSeconds(1));
            Assert.Single(body.ServiceMetadata);
        }

        [Fact]
        public async Task GetAggregatedForecasts_HigherRatedServiceHasMultipleUpcomingForecasts_ReturnsAllOrderedByTimestamp()
        {
            var uid = UniqueUid();
            var raterA = UniqueUid();
            var raterB = UniqueUid();
            await SeedUserAsync(uid);
            await SeedUserAsync(raterA);
            await SeedUserAsync(raterB);
            var openWeather = await SeedServiceAsync(name: "OpenWeather");
            var weatherApi = await SeedServiceAsync(name: "WeatherAPI", apiEndpoint: "https://example2.test");
            var cityId = await SeedCityAsync();
            var openWeatherCitySite = await SeedCitySiteAsync(cityId, openWeather);
            var weatherApiCitySite = await SeedCitySiteAsync(cityId, weatherApi);
            await SeedUserCitySiteAsync(uid, openWeatherCitySite);
            await SeedUserCitySiteAsync(uid, weatherApiCitySite);

            var now = DateTime.UtcNow;
            var laterForecastId = await SeedForecastAsync(openWeatherCitySite, now.AddHours(3));
            var soonerForecastId = await SeedForecastAsync(openWeatherCitySite, now.AddHours(1));
            var weatherApiForecastId = await SeedForecastAsync(weatherApiCitySite, now.AddHours(1));

            // OpenWeather is rated higher and wins - both of its upcoming forecasts should be
            // returned, ordered by timestamp, while WeatherAPI's is excluded entirely.
            await SeedRatingAsync(raterA, soonerForecastId, value: 5);
            await SeedRatingAsync(raterB, soonerForecastId, value: 5);
            await SeedRatingAsync(raterA, weatherApiForecastId, value: 2);
            await SeedRatingAsync(raterB, weatherApiForecastId, value: 2);

            await using var db = _fixture.CreateDbContext();
            var controller = CreateController(db, FakeAppwriteAuthService.ReturningUid(uid), bearerToken: "token");

            var result = await controller.GetAggregatedForecasts(CancellationToken.None);

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var body = Assert.IsType<GetAggregatedForecastsResponse>(ok.Value);
            Assert.Equal(2, body.Forecasts.Count);
            Assert.Equal("Athens", body.Forecasts[0].City);
            Assert.Equal("OpenWeather", body.Forecasts[0].Service);
            Assert.Equal(soonerForecastId, body.Forecasts[0].ForecastId);
            Assert.Equal(now.AddHours(1), body.Forecasts[0].Timestamp, TimeSpan.FromSeconds(1));
            Assert.Equal("Athens", body.Forecasts[1].City);
            Assert.Equal("OpenWeather", body.Forecasts[1].Service);
            Assert.Equal(laterForecastId, body.Forecasts[1].ForecastId);
            Assert.Equal(now.AddHours(3), body.Forecasts[1].Timestamp, TimeSpan.FromSeconds(1));

            var metadata = Assert.Single(body.ServiceMetadata);
            Assert.Equal("OpenWeather", metadata.Service);
            Assert.True(metadata.AggregationApplicable);
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
                Type = "CURRENT",
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
