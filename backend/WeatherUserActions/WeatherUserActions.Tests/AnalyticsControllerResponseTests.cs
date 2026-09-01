using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using WeatherUserActions.Analytics;
using WeatherUserActions.AppwriteServices;
using WeatherUserActions.Controllers;
using WeatherUserActions.Dtos;
using WeatherUserActions.Repositories;
using WeatherUserActions.Services;
using WeatherUserActions.Tests.Fakes;
using Xunit;

namespace WeatherUserActions.Tests
{
    // Mocks only the IAnalyticsRepository boundary - AnalyticsService and AnalyticsController run
    // for real, so these exercise the actual result -> HTTP mapping without Docker/a real database.
    public class AnalyticsControllerResponseTests
    {
        private static readonly DateOnly RangeStart = new(2026, 8, 1);
        private static readonly DateOnly RangeEnd = new(2026, 8, 31);

        [Fact]
        public async Task RequestAnalytics_MissingAuthorizationHeader_ReturnsUnauthorized()
        {
            var controller = CreateController(
                FakeAppwriteAuthService.ReturningUid("uid-1"),
                FakeAnalyticsRepository.ReturningScope([1], [1]),
                bearerToken: null);

            var result = await controller.RequestAnalytics(
                new RequestAnalyticsRequest([1], [1], RangeStart, RangeEnd), CancellationToken.None);

            Assert.IsType<UnauthorizedResult>(result);
        }

        [Fact]
        public async Task RequestAnalytics_InvalidToken_ReturnsUnauthorized()
        {
            var controller = CreateController(
                FakeAppwriteAuthService.RejectingToken(),
                FakeAnalyticsRepository.ReturningScope([1], [1]),
                bearerToken: "bad-token");

            var result = await controller.RequestAnalytics(
                new RequestAnalyticsRequest([1], [1], RangeStart, RangeEnd), CancellationToken.None);

            Assert.IsType<UnauthorizedResult>(result);
        }

        [Fact]
        public async Task RequestAnalytics_CityOutsideSelectionScope_ReturnsBadRequest()
        {
            var controller = CreateController(
                FakeAppwriteAuthService.ReturningUid("uid-1"),
                FakeAnalyticsRepository.ReturningScope(cityIds: [1], serviceIds: [1]),
                bearerToken: "token");

            var result = await controller.RequestAnalytics(
                new RequestAnalyticsRequest([2], [1], RangeStart, RangeEnd), CancellationToken.None);

            var problem = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        }

        [Fact]
        public async Task RequestAnalytics_ServiceOutsideSelectionScope_ReturnsBadRequest()
        {
            var controller = CreateController(
                FakeAppwriteAuthService.ReturningUid("uid-1"),
                FakeAnalyticsRepository.ReturningScope(cityIds: [1], serviceIds: [1]),
                bearerToken: "token");

            var result = await controller.RequestAnalytics(
                new RequestAnalyticsRequest([1], [9], RangeStart, RangeEnd), CancellationToken.None);

            var problem = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        }

        [Fact]
        public async Task RequestAnalytics_EmptyCityList_ReturnsBadRequest()
        {
            var controller = CreateController(
                FakeAppwriteAuthService.ReturningUid("uid-1"),
                FakeAnalyticsRepository.ReturningScope([1], [1]),
                bearerToken: "token");

            var result = await controller.RequestAnalytics(
                new RequestAnalyticsRequest([], [1], RangeStart, RangeEnd), CancellationToken.None);

            var problem = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        }

        [Fact]
        public async Task RequestAnalytics_RangeOf367DaysInclusive_ReturnsBadRequest()
        {
            var controller = CreateController(
                FakeAppwriteAuthService.ReturningUid("uid-1"),
                FakeAnalyticsRepository.ReturningScope([1], [1]),
                bearerToken: "token");

            // 2026 is not a leap year, so 2026-01-01..2027-01-02 spans 367 days inclusive.
            var result = await controller.RequestAnalytics(
                new RequestAnalyticsRequest([1], [1], new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 2)),
                CancellationToken.None);

            var problem = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        }

        [Fact]
        public async Task RequestAnalytics_RangeOf366DaysInclusive_IsAccepted()
        {
            var controller = CreateController(
                FakeAppwriteAuthService.ReturningUid("uid-1"),
                FakeAnalyticsRepository.ReturningScope([1], [1]),
                bearerToken: "token");

            // 2026-01-01..2027-01-01 spans exactly 366 days inclusive - the maximum allowed.
            var result = await controller.RequestAnalytics(
                new RequestAnalyticsRequest([1], [1], new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 1)),
                CancellationToken.None);

            Assert.IsType<AcceptedResult>(result);
        }

        [Fact]
        public async Task RequestAnalytics_StartAfterEnd_ReturnsBadRequest()
        {
            var controller = CreateController(
                FakeAppwriteAuthService.ReturningUid("uid-1"),
                FakeAnalyticsRepository.ReturningScope([1], [1]),
                bearerToken: "token");

            var result = await controller.RequestAnalytics(
                new RequestAnalyticsRequest([1], [1], RangeEnd, RangeStart), CancellationToken.None);

            var problem = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        }

        [Fact]
        public async Task RequestAnalytics_DateRangeExceeds366Days_ReturnsBadRequest()
        {
            var controller = CreateController(
                FakeAppwriteAuthService.ReturningUid("uid-1"),
                FakeAnalyticsRepository.ReturningScope([1], [1]),
                bearerToken: "token");

            var result = await controller.RequestAnalytics(
                new RequestAnalyticsRequest([1], [1], new DateOnly(2026, 1, 1), new DateOnly(2027, 6, 1)),
                CancellationToken.None);

            var problem = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        }

        [Fact]
        public async Task RequestAnalytics_SelectionScopeLookupFails_ReturnsProblem()
        {
            var controller = CreateController(
                FakeAppwriteAuthService.ReturningUid("uid-1"),
                FakeAnalyticsRepository.FailingToLoadScope(),
                bearerToken: "token");

            var result = await controller.RequestAnalytics(
                new RequestAnalyticsRequest([1], [1], RangeStart, RangeEnd), CancellationToken.None);

            var problem = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status500InternalServerError, problem.StatusCode);
        }

        [Fact]
        public async Task RequestAnalytics_EnqueueFails_ReturnsProblem()
        {
            var controller = CreateController(
                FakeAppwriteAuthService.ReturningUid("uid-1"),
                FakeAnalyticsRepository.FailingToEnqueue([1], [1]),
                bearerToken: "token");

            var result = await controller.RequestAnalytics(
                new RequestAnalyticsRequest([1], [1], RangeStart, RangeEnd), CancellationToken.None);

            var problem = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status500InternalServerError, problem.StatusCode);
        }

        [Fact]
        public async Task RequestAnalytics_ValidRequest_ReturnsAcceptedWithBatchId()
        {
            var controller = CreateController(
                FakeAppwriteAuthService.ReturningUid("uid-1"),
                FakeAnalyticsRepository.ReturningScope(cityIds: [1, 2], serviceIds: [1, 3]),
                bearerToken: "token");

            var result = await controller.RequestAnalytics(
                new RequestAnalyticsRequest([1, 2], [1, 3], RangeStart, RangeEnd), CancellationToken.None);

            var accepted = Assert.IsType<AcceptedResult>(result);
            var body = Assert.IsType<RequestAnalyticsResponse>(accepted.Value);
            Assert.NotEqual(Guid.Empty, body.BatchId);
        }

        private static AnalyticsController CreateController(
            IAppwriteAuthService authService, IAnalyticsRepository repository, string? bearerToken)
        {
            var service = new AnalyticsService(
                authService,
                FakeAppwriteUsersService.ReturningEmail("user@example.test"),
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
