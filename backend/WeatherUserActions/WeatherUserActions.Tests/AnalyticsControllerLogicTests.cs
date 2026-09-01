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
            Assert.Equal(0, await verifyDb.AnalyticsReports.CountAsync());
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
            Assert.Equal(0, await verifyDb.AnalyticsReports.CountAsync());
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
        public async Task RequestAnalytics_TwoSelectedServices_QueuesOneReportPerServiceWithCityMetricRows()
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
            var reports = await verifyDb.AnalyticsReports
                .Include(report => report.CityMetrics)
                .OrderBy(report => report.ServiceId)
                .ToListAsync();

            Assert.Equal(2, reports.Count);
            Assert.All(reports, report =>
            {
                Assert.Equal(body.BatchId, report.BatchId);
                Assert.Equal(uid, report.UserId);
                Assert.Equal(AnalyticsReportStatus.Queued, report.Status);
                Assert.Equal("JSON", report.Format);
                Assert.Equal(RangeStart, report.DateRangeStart);
                Assert.Equal(RangeEnd, report.DateRangeEnd);

                var metric = Assert.Single(report.CityMetrics);
                Assert.Equal(cityId, metric.CityId);
                Assert.Null(metric.AvgTemperature);
                Assert.Null(metric.MaxWindSpeed);
                Assert.Equal(0, metric.DangerDayCount);
                Assert.Equal(0, metric.SampleCount);
            });
            Assert.Equal(new[] { openWeather, weatherApi }, reports.Select(report => report.ServiceId).ToArray());
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
                Assert.Null(report.DeliveredAt);
            }

            // Second tick: transport recovered - the batch is delivered.
            var workingSender = new FakeEmailSender();
            await ProcessQueuedReportsAsync(workingSender);
            Assert.Single(workingSender.Sent);

            await using (var afterDelivery = _fixture.CreateDbContext())
            {
                var report = await afterDelivery.AnalyticsReports.SingleAsync(r => r.BatchId == batchId);
                Assert.NotNull(report.DeliveredAt);
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
            Assert.All(reports, report => Assert.NotNull(report.DeliveredAt));

            var email = Assert.Single(emailSender.Sent);
            Assert.Contains("ready", email.Subject);
            Assert.Contains("WeatherAPI", email.Body);
            Assert.Single(email.Attachments);
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
