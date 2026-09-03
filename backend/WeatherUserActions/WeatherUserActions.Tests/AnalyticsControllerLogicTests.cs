using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using WeatherUserActions.Analytics;
using WeatherUserActions.AppwriteServices;
using WeatherUserActions.Controllers;
using WeatherUserActions.Data;
using WeatherUserActions.Dtos;
using WeatherUserActions.Email;
using WeatherUserActions.Models;
using WeatherUserActions.Repositories;
using WeatherUserActions.Services;
using WeatherUserActions.Tests.Fakes;
using WeatherUserActions.Tests.Fixtures;
using Xunit;

namespace WeatherUserActions.Tests
{
    // UC8 (Request Analytics). Real AnalyticsRepository against Testcontainers SQL Server; only the
    // Appwrite auth/user-lookup and the email transport are faked.
    [Collection(TestCollections.SqlServer)]
    public class AnalyticsControllerLogicTests : IAsyncLifetime
    {
        private static readonly DateOnly RangeStart = new(2026, 8, 1);
        private static readonly DateOnly RangeEnd = new(2026, 8, 31);
        private const string UserEmail = "user@example.test";

        private readonly SqlServerFixture _fixture;

        public AnalyticsControllerLogicTests(SqlServerFixture fixture)
        {
            _fixture = fixture;
        }

        public Task InitializeAsync() => _fixture.ResetDatabaseAsync();

        public Task DisposeAsync() => Task.CompletedTask;

        [Fact]
        public async Task RequestAnalytics_MissingAuthorizationHeader_ReturnsUnauthorized()
        {
            await using var db = _fixture.CreateDbContext();
            var controller = CreateController(db, FakeAppwriteAuthService.ReturningUid("irrelevant"), bearerToken: null);

            var result = await controller.RequestAnalytics(
                new RequestAnalyticsRequest([1], [1], RangeStart, RangeEnd), CancellationToken.None);

            Assert.IsType<UnauthorizedResult>(result);
        }

        [Fact]
        public async Task RequestAnalytics_CityNotInUsersSelection_ReturnsBadRequestAndQueuesNothing()
        {
            var uid = UniqueUid();
            await SeedUserAsync(uid);
            var serviceId = await SeedServiceAsync();
            var selectedCityId = await SeedCityAsync("Athens", "Greece");
            await SeedSelectionAsync(uid, selectedCityId, serviceId);
            var unselectedCityId = await SeedCityAsync("Paris", "France", 48.85m, 2.35m);

            await using var db = _fixture.CreateDbContext();
            var controller = CreateController(db, FakeAppwriteAuthService.ReturningUid(uid), bearerToken: "token");

            var result = await controller.RequestAnalytics(
                new RequestAnalyticsRequest([unselectedCityId], [serviceId], RangeStart, RangeEnd), CancellationToken.None);

            var problem = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);

            await using var verifyDb = _fixture.CreateDbContext();
            Assert.Equal(0, await verifyDb.AnalyticsReportBatches.CountAsync());
            Assert.Equal(0, await verifyDb.AnalyticsReports.CountAsync());
            Assert.Equal(0, await verifyDb.AnalyticsReportCityMetrics.CountAsync());
        }

        [Fact]
        public async Task RequestAnalytics_ServiceNotInUsersSelection_ReturnsBadRequestAndQueuesNothing()
        {
            var uid = UniqueUid();
            await SeedUserAsync(uid);
            var selectedServiceId = await SeedServiceAsync("OpenWeather");
            var cityId = await SeedCityAsync("Athens", "Greece");
            await SeedSelectionAsync(uid, cityId, selectedServiceId);
            var unselectedServiceId = await SeedServiceAsync("WeatherAPI", "https://example2.test");

            await using var db = _fixture.CreateDbContext();
            var controller = CreateController(db, FakeAppwriteAuthService.ReturningUid(uid), bearerToken: "token");

            var result = await controller.RequestAnalytics(
                new RequestAnalyticsRequest([cityId], [unselectedServiceId], RangeStart, RangeEnd), CancellationToken.None);

            var problem = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);

            await using var verifyDb = _fixture.CreateDbContext();
            Assert.Equal(0, await verifyDb.AnalyticsReportBatches.CountAsync());
            Assert.Equal(0, await verifyDb.AnalyticsReports.CountAsync());
            Assert.Equal(0, await verifyDb.AnalyticsReportCityMetrics.CountAsync());
        }

        [Fact]
        public async Task RequestAnalytics_StartAfterEnd_ReturnsBadRequest()
        {
            var uid = UniqueUid();
            await SeedUserAsync(uid);
            var serviceId = await SeedServiceAsync();
            var cityId = await SeedCityAsync("Athens", "Greece");
            await SeedSelectionAsync(uid, cityId, serviceId);

            await using var db = _fixture.CreateDbContext();
            var controller = CreateController(db, FakeAppwriteAuthService.ReturningUid(uid), bearerToken: "token");

            var result = await controller.RequestAnalytics(
                new RequestAnalyticsRequest([cityId], [serviceId], RangeEnd, RangeStart), CancellationToken.None);

            var problem = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);

            await using var verifyDb = _fixture.CreateDbContext();
            Assert.Equal(0, await verifyDb.AnalyticsReportBatches.CountAsync());
            Assert.Equal(0, await verifyDb.AnalyticsReports.CountAsync());
            Assert.Equal(0, await verifyDb.AnalyticsReportCityMetrics.CountAsync());
        }

        [Fact]
        public async Task RequestAnalytics_DateRangeExceeds366Days_ReturnsBadRequest()
        {
            var uid = UniqueUid();
            await SeedUserAsync(uid);
            var serviceId = await SeedServiceAsync();
            var cityId = await SeedCityAsync("Athens", "Greece");
            await SeedSelectionAsync(uid, cityId, serviceId);

            await using var db = _fixture.CreateDbContext();
            var controller = CreateController(db, FakeAppwriteAuthService.ReturningUid(uid), bearerToken: "token");

            var result = await controller.RequestAnalytics(
                new RequestAnalyticsRequest([cityId], [serviceId], new DateOnly(2026, 1, 1), new DateOnly(2027, 6, 1)),
                CancellationToken.None);

            var problem = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        }

        [Fact]
        public async Task RequestAnalytics_TwoSelectedServices_CreatesOneBatchWithOneReportPerServiceAndCityMetricRows()
        {
            var uid = UniqueUid();
            await SeedUserAsync(uid);
            var openWeather = await SeedServiceAsync("OpenWeather");
            var weatherApi = await SeedServiceAsync("WeatherAPI", "https://example2.test");
            var cityId = await SeedCityAsync("Athens", "Greece");
            await SeedSelectionAsync(uid, cityId, openWeather);
            await SeedSelectionAsync(uid, cityId, weatherApi);

            await using var db = _fixture.CreateDbContext();
            var controller = CreateController(db, FakeAppwriteAuthService.ReturningUid(uid), bearerToken: "token");

            var result = await controller.RequestAnalytics(
                new RequestAnalyticsRequest([cityId], [openWeather, weatherApi], RangeStart, RangeEnd), CancellationToken.None);

            var accepted = Assert.IsType<AcceptedResult>(result);
            var body = Assert.IsType<RequestAnalyticsResponse>(accepted.Value);
            Assert.NotEqual(Guid.Empty, body.BatchId);

            await using var verifyDb = _fixture.CreateDbContext();
            var batch = await verifyDb.AnalyticsReportBatches
                .Include(batch => batch.Reports)
                    .ThenInclude(report => report.CityMetrics)
                .SingleAsync();

            Assert.Equal(body.BatchId, batch.BatchId);
            Assert.Equal(uid, batch.UserId);
            Assert.Equal("JSON", batch.Format);
            Assert.Equal(RangeStart, batch.DateRangeStart);
            Assert.Equal(RangeEnd, batch.DateRangeEnd);
            Assert.Null(batch.DeliveredAt);
            //
            var reports = batch.Reports.OrderBy(report => report.ServiceId).ToList();
            Assert.Equal(2, reports.Count);
            Assert.Equal(new[] { openWeather, weatherApi }, reports.Select(report => report.ServiceId).ToArray());
            Assert.All(reports, report =>
            {
                Assert.Equal(batch.BatchId, report.BatchId);
                Assert.Equal(AnalyticsReportStatus.Queued, report.Status);

                var metric = Assert.Single(report.CityMetrics);
                Assert.Equal(cityId, metric.CityId);
                Assert.Null(metric.AvgTemperature);
                Assert.Null(metric.MaxWindSpeed);
                Assert.Equal(0, metric.DangerDayCount);
                Assert.Equal(0, metric.SampleCount);
            });
        }

        [Fact]
        public async Task ProcessQueuedReports_ComputesPerCityStatisticsCompletesReportAndEmailsUser()
        {
            var uid = UniqueUid();
            await SeedUserAsync(uid);
            var serviceId = await SeedServiceAsync("OpenWeather");
            var cityId = await SeedCityAsync("Athens", "Greece");
            var citySiteId = await SeedSelectionAsync(uid, cityId, serviceId);

            // Two forecasts inside the range, one danger day; one forecast outside the range.
            await SeedForecastAsync(citySiteId, new DateTime(2026, 8, 10, 12, 0, 0), temperature: 10m, humidity: 40m, windSpeed: 4m, dangerFlag: true);
            await SeedForecastAsync(citySiteId, new DateTime(2026, 8, 20, 12, 0, 0), temperature: 20m, humidity: 60m, windSpeed: 10m, dangerFlag: false);
            await SeedForecastAsync(citySiteId, new DateTime(2026, 7, 15, 12, 0, 0), temperature: 55m, humidity: 90m, windSpeed: 50m, dangerFlag: true);

            var batchId = await RequestAnalyticsAsync(uid, [cityId], [serviceId]);
            var emailSender = new FakeEmailSender();
            await ProcessQueuedReportsAsync(emailSender);

            await using var verifyDb = _fixture.CreateDbContext();
            var report = await verifyDb.AnalyticsReports
                .Include(analyticsReport => analyticsReport.CityMetrics)
                .SingleAsync(analyticsReport => analyticsReport.BatchId == batchId);

            Assert.Equal(AnalyticsReportStatus.Completed, report.Status);

            var metric = Assert.Single(report.CityMetrics);
            Assert.Equal(cityId, metric.CityId);
            Assert.Equal(2, metric.SampleCount);
            Assert.Equal(1, metric.DangerDayCount);
            Assert.Equal(15.00m, metric.AvgTemperature);
            Assert.Equal(5.00m, metric.StdDevTemperature);
            Assert.Equal(10.00m, metric.MinTemperature);
            Assert.Equal(20.00m, metric.MaxTemperature);
            Assert.Equal(50.00m, metric.AvgHumidity);
            Assert.Equal(10.00m, metric.StdDevHumidity);
            Assert.Equal(40.00m, metric.MinHumidity);
            Assert.Equal(60.00m, metric.MaxHumidity);
            Assert.Equal(7.00m, metric.AvgWindSpeed);
            Assert.Equal(3.00m, metric.StdDevWindSpeed);
            Assert.Equal(4.00m, metric.MinWindSpeed);
            Assert.Equal(10.00m, metric.MaxWindSpeed);
            var email = Assert.Single(emailSender.Sent);
            Assert.Equal(UserEmail, email.ToEmail);
            Assert.Contains("ready", email.Subject);

            var pdf = Assert.Single(email.Attachments);
            Assert.Equal("application/pdf", pdf.ContentType);
            Assert.Equal($"analytics-{batchId}.pdf", pdf.FileName);
            Assert.StartsWith("%PDF-", System.Text.Encoding.ASCII.GetString(pdf.Content, 0, 5));
        }

        [Fact]
        public async Task ProcessQueuedReports_SelectedCityWithNoForecastData_StoresNullMetricsAndZeroCounts()
        {
            var uid = UniqueUid();
            await SeedUserAsync(uid);
            var serviceId = await SeedServiceAsync("OpenWeather");
            var athensId = await SeedCityAsync("Athens", "Greece");
            var parisId = await SeedCityAsync("Paris", "France", 48.85m, 2.35m);
            var athensSiteId = await SeedSelectionAsync(uid, athensId, serviceId);
            await SeedSelectionAsync(uid, parisId, serviceId);

            await SeedForecastAsync(athensSiteId, new DateTime(2026, 8, 10, 12, 0, 0), temperature: 12m, humidity: 44m, windSpeed: 6m, dangerFlag: false);

            var batchId = await RequestAnalyticsAsync(uid, [athensId, parisId], [serviceId]);
            var emailSender = new FakeEmailSender();
            await ProcessQueuedReportsAsync(emailSender);

            await using var verifyDb = _fixture.CreateDbContext();
            var report = await verifyDb.AnalyticsReports
                .Include(analyticsReport => analyticsReport.CityMetrics)
                .SingleAsync(analyticsReport => analyticsReport.BatchId == batchId);

            Assert.Equal(AnalyticsReportStatus.Completed, report.Status);

            var parisMetric = Assert.Single(report.CityMetrics, metric => metric.CityId == parisId);
            Assert.Null(parisMetric.AvgTemperature);
            Assert.Null(parisMetric.StdDevHumidity);
            Assert.Null(parisMetric.MaxWindSpeed);
            Assert.Equal(0, parisMetric.DangerDayCount);
            Assert.Equal(0, parisMetric.SampleCount);

            var athensMetric = Assert.Single(report.CityMetrics, metric => metric.CityId == athensId);
            Assert.Equal(1, athensMetric.SampleCount);
            Assert.Equal(12.00m, athensMetric.AvgTemperature);

            Assert.Single(emailSender.Sent);
        }

        [Fact]
        public async Task ProcessQueuedReports_DangerDayCount_CountsDistinctCalendarDates()
        {
            var uid = UniqueUid();
            await SeedUserAsync(uid);
            var serviceId = await SeedServiceAsync("OpenWeather");
            var cityId = await SeedCityAsync("Athens", "Greece");
            var citySiteId = await SeedSelectionAsync(uid, cityId, serviceId);

            // Two danger forecasts on Aug 10, one on Aug 12, one non-danger on Aug 15 -> 2 danger days.
            await SeedForecastAsync(citySiteId, new DateTime(2026, 8, 10, 6, 0, 0), dangerFlag: true);
            await SeedForecastAsync(citySiteId, new DateTime(2026, 8, 10, 18, 0, 0), dangerFlag: true);
            await SeedForecastAsync(citySiteId, new DateTime(2026, 8, 12, 12, 0, 0), dangerFlag: true);
            await SeedForecastAsync(citySiteId, new DateTime(2026, 8, 15, 12, 0, 0), dangerFlag: false);

            var batchId = await RequestAnalyticsAsync(uid, [cityId], [serviceId]);
            await ProcessQueuedReportsAsync(new FakeEmailSender());

            await using var verifyDb = _fixture.CreateDbContext();
            var report = await verifyDb.AnalyticsReports
                .Include(analyticsReport => analyticsReport.CityMetrics)
                .SingleAsync(analyticsReport => analyticsReport.BatchId == batchId);

            var metric = Assert.Single(report.CityMetrics);
            Assert.Equal(4, metric.SampleCount);
            Assert.Equal(2, metric.DangerDayCount);
        }

        [Fact]
        public async Task ProcessQueuedReports_EmailTransportFailsThenRecovers_DeliversOnceWithNoDuplicate()
        {
            var uid = UniqueUid();
            await SeedUserAsync(uid);
            var serviceId = await SeedServiceAsync("OpenWeather");
            var cityId = await SeedCityAsync("Athens", "Greece");
            var citySiteId = await SeedSelectionAsync(uid, cityId, serviceId);
            await SeedForecastAsync(citySiteId, new DateTime(2026, 8, 10, 12, 0, 0), temperature: 15m);

            var batchId = await RequestAnalyticsAsync(uid, [cityId], [serviceId]);

            // First tick: the report generates, but the mail transport is down.
            var failingSender = new FakeEmailSender(transientFailures: 1);
            await ProcessQueuedReportsAsync(failingSender);
            Assert.Empty(failingSender.Sent);

            await using (var afterFailure = _fixture.CreateDbContext())
            {
                var report = await afterFailure.AnalyticsReports.SingleAsync(r => r.BatchId == batchId);
                Assert.Equal(AnalyticsReportStatus.Completed, report.Status);
                var batch = await afterFailure.AnalyticsReportBatches.SingleAsync(b => b.BatchId == batchId);
                Assert.Null(batch.DeliveredAt);
            }

            // Second tick: transport recovered - the batch is delivered.
            var workingSender = new FakeEmailSender();
            await ProcessQueuedReportsAsync(workingSender);
            Assert.Single(workingSender.Sent);

            await using (var afterDelivery = _fixture.CreateDbContext())
            {
                var batch = await afterDelivery.AnalyticsReportBatches.SingleAsync(b => b.BatchId == batchId);
                Assert.NotNull(batch.DeliveredAt);
            }

            // Third tick: nothing left to generate or deliver.
            var thirdSender = new FakeEmailSender();
            await ProcessQueuedReportsAsync(thirdSender);
            Assert.Empty(thirdSender.Sent);
        }

        [Fact]
        public async Task ProcessQueuedReports_OneServiceFailedToGenerate_EmailsPartialReportNamingTheFailedService()
        {
            var uid = UniqueUid();
            await SeedUserAsync(uid);
            var openWeather = await SeedServiceAsync("OpenWeather");
            var weatherApi = await SeedServiceAsync("WeatherAPI", "https://example2.test");
            var cityId = await SeedCityAsync("Athens", "Greece");
            var openWeatherSiteId = await SeedSelectionAsync(uid, cityId, openWeather);
            await SeedSelectionAsync(uid, cityId, weatherApi);
            await SeedForecastAsync(openWeatherSiteId, new DateTime(2026, 8, 10, 12, 0, 0), temperature: 15m);

            var batchId = await RequestAnalyticsAsync(uid, [cityId], [openWeather, weatherApi]);

            // Simulate the WeatherAPI report having failed generation on an earlier tick.
            await using (var setup = _fixture.CreateDbContext())
            {
                var failing = await setup.AnalyticsReports
                    .SingleAsync(r => r.BatchId == batchId && r.ServiceId == weatherApi);
                failing.Status = AnalyticsReportStatus.Failed;
                await setup.SaveChangesAsync();
            }

            var emailSender = new FakeEmailSender();
            await ProcessQueuedReportsAsync(emailSender);

            await using var verifyDb = _fixture.CreateDbContext();
            var reports = await verifyDb.AnalyticsReports
                .Where(r => r.BatchId == batchId)
                .OrderBy(r => r.ServiceId)
                .ToListAsync();

            Assert.Equal(AnalyticsReportStatus.Completed, reports.Single(r => r.ServiceId == openWeather).Status);
            Assert.Equal(AnalyticsReportStatus.Failed, reports.Single(r => r.ServiceId == weatherApi).Status);

            var batch = await verifyDb.AnalyticsReportBatches.SingleAsync(b => b.BatchId == batchId);
            Assert.NotNull(batch.DeliveredAt);

            var email = Assert.Single(emailSender.Sent);
            Assert.Contains("ready", email.Subject);
            Assert.Contains("WeatherAPI", email.Body);
            Assert.Single(email.Attachments);
        }

        [Fact]
        public async Task ProcessQueuedReports_TwoCitiesTwoServices_EachReportCoversOnlyItsOwnServiceAndCity()
        {
            var uid = UniqueUid();
            await SeedUserAsync(uid);
            var openWeather = await SeedServiceAsync("OpenWeather");
            var weatherApi = await SeedServiceAsync("WeatherAPI", "https://example2.test");
            var athensId = await SeedCityAsync("Athens", "Greece");
            var parisId = await SeedCityAsync("Paris", "France", 48.85m, 2.35m);

            // Four distinct (city, service) selections = four CitySites, each with its own forecasts.
            var owAthens = await SeedSelectionAsync(uid, athensId, openWeather);
            var owParis = await SeedSelectionAsync(uid, parisId, openWeather);
            var waAthens = await SeedSelectionAsync(uid, athensId, weatherApi);
            var waParis = await SeedSelectionAsync(uid, parisId, weatherApi);

            // Disjoint averages per (service, city): OW/Athens 15, OW/Paris 30, WA/Athens 45, WA/Paris 10.
            await SeedForecastAsync(owAthens, new DateTime(2026, 8, 10, 12, 0, 0), temperature: 10m);
            await SeedForecastAsync(owAthens, new DateTime(2026, 8, 20, 12, 0, 0), temperature: 20m);
            await SeedForecastAsync(owParis, new DateTime(2026, 8, 10, 12, 0, 0), temperature: 25m);
            await SeedForecastAsync(owParis, new DateTime(2026, 8, 20, 12, 0, 0), temperature: 35m);
            await SeedForecastAsync(waAthens, new DateTime(2026, 8, 10, 12, 0, 0), temperature: 40m);
            await SeedForecastAsync(waAthens, new DateTime(2026, 8, 20, 12, 0, 0), temperature: 50m);
            await SeedForecastAsync(waParis, new DateTime(2026, 8, 10, 12, 0, 0), temperature: 5m);
            await SeedForecastAsync(waParis, new DateTime(2026, 8, 20, 12, 0, 0), temperature: 15m);

            var batchId = await RequestAnalyticsAsync(uid, [athensId, parisId], [openWeather, weatherApi]);
            var emailSender = new FakeEmailSender();
            await ProcessQueuedReportsAsync(emailSender);

            await using var verifyDb = _fixture.CreateDbContext();
            var reports = await verifyDb.AnalyticsReports
                .Include(report => report.CityMetrics)
                .Where(report => report.BatchId == batchId)
                .ToListAsync();

            Assert.Equal(2, reports.Count);
            Assert.All(reports, report => Assert.Equal(AnalyticsReportStatus.Completed, report.Status));
            Assert.All(reports, report => Assert.All(report.CityMetrics, metric => Assert.Equal(2, metric.SampleCount)));

            var openWeatherReport = reports.Single(report => report.ServiceId == openWeather);
            Assert.Equal(15.00m, Assert.Single(openWeatherReport.CityMetrics, metric => metric.CityId == athensId).AvgTemperature);
            Assert.Equal(30.00m, Assert.Single(openWeatherReport.CityMetrics, metric => metric.CityId == parisId).AvgTemperature);

            var weatherApiReport = reports.Single(report => report.ServiceId == weatherApi);
            Assert.Equal(45.00m, Assert.Single(weatherApiReport.CityMetrics, metric => metric.CityId == athensId).AvgTemperature);
            Assert.Equal(10.00m, Assert.Single(weatherApiReport.CityMetrics, metric => metric.CityId == parisId).AvgTemperature);

            // Two reports, still one batch -> exactly one delivery email.
            var email = Assert.Single(emailSender.Sent);
            Assert.Equal(UserEmail, email.ToEmail);
        }

        [Fact]
        public async Task ProcessQueuedReports_TwoUsersShareTheSameCitySites_RequestersBatchUsesTheSharedForecasts()
        {
            var requester = UniqueUid();
            var otherUser = UniqueUid();
            await SeedUserAsync(requester);
            await SeedUserAsync(otherUser);
            var openWeather = await SeedServiceAsync("OpenWeather");
            var weatherApi = await SeedServiceAsync("WeatherAPI", "https://example2.test");
            var cityId = await SeedCityAsync("Athens", "Greece");

            // Both users select the same two (city, service) pairs; SeedSelectionAsync reuses each CitySite.
            var openWeatherSite = await SeedSelectionAsync(requester, cityId, openWeather);
            var weatherApiSite = await SeedSelectionAsync(requester, cityId, weatherApi);
            await SeedSelectionAsync(otherUser, cityId, openWeather);
            await SeedSelectionAsync(otherUser, cityId, weatherApi);

            await SeedForecastAsync(openWeatherSite, new DateTime(2026, 8, 10, 12, 0, 0), temperature: 12m);
            await SeedForecastAsync(openWeatherSite, new DateTime(2026, 8, 20, 12, 0, 0), temperature: 18m);
            await SeedForecastAsync(weatherApiSite, new DateTime(2026, 8, 10, 12, 0, 0), temperature: 40m);
            await SeedForecastAsync(weatherApiSite, new DateTime(2026, 8, 20, 12, 0, 0), temperature: 50m);

            await RequestAnalyticsAsync(requester, [cityId], [openWeather, weatherApi]);
            var emailSender = new FakeEmailSender();
            await ProcessQueuedReportsAsync(emailSender);

            await using var verifyDb = _fixture.CreateDbContext();
            var batch = await verifyDb.AnalyticsReportBatches
                .Include(analyticsBatch => analyticsBatch.Reports)
                    .ThenInclude(report => report.CityMetrics)
                .SingleAsync();

            Assert.Equal(requester, batch.UserId);
            Assert.Equal(2, batch.Reports.Count);

            var openWeatherMetric = Assert.Single(batch.Reports.Single(report => report.ServiceId == openWeather).CityMetrics);
            Assert.Equal(2, openWeatherMetric.SampleCount);
            Assert.Equal(15.00m, openWeatherMetric.AvgTemperature);

            var weatherApiMetric = Assert.Single(batch.Reports.Single(report => report.ServiceId == weatherApi).CityMetrics);
            Assert.Equal(2, weatherApiMetric.SampleCount);
            Assert.Equal(45.00m, weatherApiMetric.AvgTemperature);

            // The shared selection does not fan the mail out - one batch, one email, to the requester.
            var email = Assert.Single(emailSender.Sent);
            Assert.Equal(UserEmail, email.ToEmail);
        }

        [Fact]
        public async Task RequestAnalytics_EnforcesPerUserScope_RejectsAnotherUsersCityOrServiceButAcceptsTheUsersOwn()
        {
            var requester = UniqueUid();
            var otherUser = UniqueUid();
            await SeedUserAsync(requester);
            await SeedUserAsync(otherUser);
            var sharedService = await SeedServiceAsync("OpenWeather");
            var otherOnlyService = await SeedServiceAsync("WeatherAPI", "https://example2.test");
            var sharedCity = await SeedCityAsync("Paris", "France", 48.85m, 2.35m);
            var otherOnlyCity = await SeedCityAsync("Rome", "Italy", 41.90m, 12.50m);

            // requester selects only (Paris, OpenWeather). otherUser also has (Rome, OpenWeather)
            // and (Paris, WeatherAPI) - so Rome and WeatherAPI are both outside the requester's scope.
            await SeedSelectionAsync(requester, sharedCity, sharedService);
            await SeedSelectionAsync(otherUser, sharedCity, sharedService);
            await SeedSelectionAsync(otherUser, otherOnlyCity, sharedService);
            await SeedSelectionAsync(otherUser, sharedCity, otherOnlyService);

            await using var db = _fixture.CreateDbContext();
            var controller = CreateController(db, FakeAppwriteAuthService.ReturningUid(requester), bearerToken: "token");

            var cityResult = await controller.RequestAnalytics(
                new RequestAnalyticsRequest([otherOnlyCity], [sharedService], RangeStart, RangeEnd), CancellationToken.None);
            Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsType<ObjectResult>(cityResult).StatusCode);

            var serviceResult = await controller.RequestAnalytics(
                new RequestAnalyticsRequest([sharedCity], [otherOnlyService], RangeStart, RangeEnd), CancellationToken.None);
            Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsType<ObjectResult>(serviceResult).StatusCode);

            // The one (city, service) pair that is in the requester's own selection is accepted.
            var acceptedResult = await controller.RequestAnalytics(
                new RequestAnalyticsRequest([sharedCity], [sharedService], RangeStart, RangeEnd), CancellationToken.None);
            var batchId = Assert.IsType<RequestAnalyticsResponse>(Assert.IsType<AcceptedResult>(acceptedResult).Value).BatchId;

            await using var verifyDb = _fixture.CreateDbContext();
            var batch = await verifyDb.AnalyticsReportBatches
                .Include(analyticsBatch => analyticsBatch.Reports)
                .SingleAsync();
            Assert.Equal(batchId, batch.BatchId);
            Assert.Equal(requester, batch.UserId);
            Assert.Equal(sharedService, Assert.Single(batch.Reports).ServiceId);
        }

        [Fact]
        public async Task ProcessQueuedReports_BothUsersRequestOverTheSharedCitySite_EachGetsTheirOwnBatchAndEmail()
        {
            var userA = UniqueUid();
            var userB = UniqueUid();
            await SeedUserAsync(userA);
            await SeedUserAsync(userB);
            var serviceId = await SeedServiceAsync("OpenWeather");
            var cityId = await SeedCityAsync("Athens", "Greece");

            // One CitySite, both users selecting it.
            var sharedCitySiteId = await SeedSelectionAsync(userA, cityId, serviceId);
            await SeedSelectionAsync(userB, cityId, serviceId);
            await SeedForecastAsync(sharedCitySiteId, new DateTime(2026, 8, 10, 12, 0, 0), temperature: 12m);
            await SeedForecastAsync(sharedCitySiteId, new DateTime(2026, 8, 20, 12, 0, 0), temperature: 18m);

            // Both users request analytics.
            var batchA = await RequestAnalyticsAsync(userA, [cityId], [serviceId]);
            var batchB = await RequestAnalyticsAsync(userB, [cityId], [serviceId]);
            Assert.NotEqual(batchA, batchB);

            var emailSender = new FakeEmailSender();
            await ProcessQueuedReportsAsync(emailSender);

            await using var verifyDb = _fixture.CreateDbContext();
            var batches = await verifyDb.AnalyticsReportBatches
                .Include(batch => batch.Reports)
                    .ThenInclude(report => report.CityMetrics)
                .ToListAsync();

            Assert.Equal(2, batches.Count);
            Assert.Equal(userA, batches.Single(batch => batch.BatchId == batchA).UserId);
            Assert.Equal(userB, batches.Single(batch => batch.BatchId == batchB).UserId);

            // Two requests -> two report rows and two city-metric rows total, nothing shared or duplicated.
            Assert.Equal(2, await verifyDb.AnalyticsReports.CountAsync());
            Assert.Equal(2, await verifyDb.AnalyticsReportCityMetrics.CountAsync());

            // One email per requester.
            Assert.Equal(2, emailSender.Sent.Count);
        }

        [Fact]
        public async Task ProcessQueuedReports_TwoRequestersOverDifferentCitySites_EachBatchCoversOnlyItsOwnCityAndService()
        {
            var userA = UniqueUid();
            var userB = UniqueUid();
            await SeedUserAsync(userA);
            await SeedUserAsync(userB);
            var openWeather = await SeedServiceAsync("OpenWeather");
            var weatherApi = await SeedServiceAsync("WeatherAPI", "https://example2.test");
            var athensId = await SeedCityAsync("Athens", "Greece");
            var parisId = await SeedCityAsync("Paris", "France", 48.85m, 2.35m);

            // No overlap: userA has (Athens, OpenWeather), userB has (Paris, WeatherAPI).
            var athensSiteId = await SeedSelectionAsync(userA, athensId, openWeather);
            var parisSiteId = await SeedSelectionAsync(userB, parisId, weatherApi);
            await SeedForecastAsync(athensSiteId, new DateTime(2026, 8, 10, 12, 0, 0), temperature: 10m);
            await SeedForecastAsync(athensSiteId, new DateTime(2026, 8, 20, 12, 0, 0), temperature: 20m);
            await SeedForecastAsync(parisSiteId, new DateTime(2026, 8, 10, 12, 0, 0), temperature: 30m);
            await SeedForecastAsync(parisSiteId, new DateTime(2026, 8, 20, 12, 0, 0), temperature: 40m);

            var batchA = await RequestAnalyticsAsync(userA, [athensId], [openWeather]);
            var batchB = await RequestAnalyticsAsync(userB, [parisId], [weatherApi]);
            Assert.NotEqual(batchA, batchB);

            var emailSender = new FakeEmailSender();
            await ProcessQueuedReportsAsync(emailSender);

            await using var verifyDb = _fixture.CreateDbContext();
            var batches = await verifyDb.AnalyticsReportBatches
                .Include(batch => batch.Reports)
                    .ThenInclude(report => report.CityMetrics)
                .ToListAsync();

            Assert.Equal(2, batches.Count);
            Assert.Equal(2, await verifyDb.AnalyticsReports.CountAsync());
            Assert.Equal(2, await verifyDb.AnalyticsReportCityMetrics.CountAsync());

            var batchARow = batches.Single(batch => batch.BatchId == batchA);
            Assert.Equal(userA, batchARow.UserId);
            var reportA = Assert.Single(batchARow.Reports);
            Assert.Equal(openWeather, reportA.ServiceId);
            var metricA = Assert.Single(reportA.CityMetrics);
            Assert.Equal(athensId, metricA.CityId);
            Assert.Equal(15.00m, metricA.AvgTemperature);

            var batchBRow = batches.Single(batch => batch.BatchId == batchB);
            Assert.Equal(userB, batchBRow.UserId);
            var reportB = Assert.Single(batchBRow.Reports);
            Assert.Equal(weatherApi, reportB.ServiceId);
            var metricB = Assert.Single(reportB.CityMetrics);
            Assert.Equal(parisId, metricB.CityId);
            Assert.Equal(35.00m, metricB.AvgTemperature);

            Assert.Equal(2, emailSender.Sent.Count);
        }

        // --- helpers ---

        private static string UniqueUid() => $"uid-{Guid.NewGuid():N}";

        private async Task<Guid> RequestAnalyticsAsync(string uid, List<int> cityIds, List<int> serviceIds)
        {
            await using var db = _fixture.CreateDbContext();
            var controller = CreateController(db, FakeAppwriteAuthService.ReturningUid(uid), bearerToken: "token");
            var result = await controller.RequestAnalytics(
                new RequestAnalyticsRequest(cityIds, serviceIds, RangeStart, RangeEnd), CancellationToken.None);
            var accepted = Assert.IsType<AcceptedResult>(result);
            return Assert.IsType<RequestAnalyticsResponse>(accepted.Value).BatchId;
        }

        private async Task ProcessQueuedReportsAsync(IEmailSender emailSender)
        {
            await using var db = _fixture.CreateDbContext();
            var repository = new AnalyticsRepository(db, NullLogger<AnalyticsRepository>.Instance);
            var service = new AnalyticsService(
                FakeAppwriteAuthService.ReturningUid("irrelevant"),
                FakeAppwriteUsersService.ReturningEmail(UserEmail),
                repository,
                new AnalyticsReportRenderer(),
                emailSender,
                NullLogger<AnalyticsService>.Instance);

            await service.ProcessQueuedReportsAsync(CancellationToken.None);
        }

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
            string name, string country, decimal latitude = 37.98m, decimal longitude = 23.72m)
        {
            await using var db = _fixture.CreateDbContext();
            var city = new City { Name = name, Country = country, Latitude = latitude, Longitude = longitude };
            db.Cities.Add(city);
            await db.SaveChangesAsync();
            return city.CityId;
        }

        // Links the user to (city, service) and returns the CitySiteId so forecasts can be seeded.
        private async Task<int> SeedSelectionAsync(string uid, int cityId, int serviceId)
        {
            await using var db = _fixture.CreateDbContext();
            var citySite = await db.CitySites.SingleOrDefaultAsync(cs => cs.CityId == cityId && cs.ServiceId == serviceId);
            if (citySite is null)
            {
                citySite = new CitySite { CityId = cityId, ServiceId = serviceId };
                db.CitySites.Add(citySite);
                await db.SaveChangesAsync();
            }

            db.UserCitySites.Add(new UserCitySite { UserId = uid, CitySiteId = citySite.CitySiteId, AddedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
            return citySite.CitySiteId;
        }

        private async Task SeedForecastAsync(
            int citySiteId,
            DateTime timestamp,
            decimal temperature = 20m,
            decimal humidity = 50m,
            decimal windSpeed = 10m,
            bool dangerFlag = false)
        {
            await using var db = _fixture.CreateDbContext();
            db.Forecasts.Add(new Forecast
            {
                CitySiteId = citySiteId,
                Timestamp = timestamp,
                Type = "CURRENT",
                Temperature = temperature,
                Humidity = humidity,
                WindSpeed = windSpeed,
                DangerFlag = dangerFlag,
                RetrievedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        private static AnalyticsController CreateController(
            WeatherUserActionsDbContext db, IAppwriteAuthService authService, string? bearerToken)
        {
            var repository = new AnalyticsRepository(db, NullLogger<AnalyticsRepository>.Instance);
            var service = new AnalyticsService(
                authService,
                FakeAppwriteUsersService.ReturningEmail(UserEmail),
                repository,
                new AnalyticsReportRenderer(),
                new FakeEmailSender(),
                NullLogger<AnalyticsService>.Instance);

            var controller = new AnalyticsController(service)
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
