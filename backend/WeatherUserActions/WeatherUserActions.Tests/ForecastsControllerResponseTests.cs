using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using WeatherUserActions.Controllers;
using WeatherUserActions.Dtos;
using WeatherUserActions.FirebaseServices;
using WeatherUserActions.Repositories;
using WeatherUserActions.Services;
using WeatherUserActions.Tests.Fakes;
using Xunit;

namespace WeatherUserActions.Tests
{
    // Mocks only at the repository boundary (IForecastsRepository/IRatingsRepository) -
    // ForecastsService/RatingsService and ForecastsController run for real, so these exercise
    // the actual orchestration and Ok/Problem mapping logic without needing Docker/a real database.
    public class ForecastsControllerResponseTests
    {
        private static readonly RateForecastRequest ValidRatingRequest = new(Value: 4);

        [Fact]
        public async Task GetForecasts_InvalidToken_ReturnsUnauthorized()
        {
            var controller = CreateController(
                FakeFirebaseAuthService.RejectingToken(), FakeForecastsRepository.ReturningForecasts([]), FakeRatingsRepository.Succeeding());

            var result = await controller.GetForecasts(CancellationToken.None);

            Assert.IsType<UnauthorizedResult>(result.Result);
        }

        [Fact]
        public async Task GetForecasts_LoadFails_ReturnsProblem()
        {
            var controller = CreateController(
                FakeFirebaseAuthService.ReturningUid("uid-1"), FakeForecastsRepository.FailingToLoadForecasts(), FakeRatingsRepository.Succeeding());

            var result = await controller.GetForecasts(CancellationToken.None);

            var problem = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(StatusCodes.Status500InternalServerError, problem.StatusCode);
        }

        [Fact]
        public async Task GetForecasts_Succeeds_ReturnsOkWithForecasts()
        {
            var forecasts = new List<ForecastItemDto>
            {
                new(1, "Athens", "Greece", "OpenWeather", "CURRENT", DateTime.UtcNow.AddHours(1), 28.5m, 40m, 12m, false, UserRating: 4),
            };
            var controller = CreateController(
                FakeFirebaseAuthService.ReturningUid("uid-1"), FakeForecastsRepository.ReturningForecasts(forecasts), FakeRatingsRepository.Succeeding());

            var result = await controller.GetForecasts(CancellationToken.None);

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var body = Assert.IsType<GetForecastsResponse>(ok.Value);
            Assert.Equal(forecasts, body.Forecasts);
        }

        // --- RateForecast (UC7 main flow / A1) ---

        [Fact]
        public async Task RateForecast_InvalidToken_ReturnsUnauthorized()
        {
            var controller = CreateController(
                FakeFirebaseAuthService.RejectingToken(), FakeForecastsRepository.ReturningForecasts([]), FakeRatingsRepository.Succeeding());

            var result = await controller.RateForecast(1, ValidRatingRequest, CancellationToken.None);

            Assert.IsType<UnauthorizedResult>(result);
        }

        [Fact]
        public async Task RateForecast_ForecastMissing_ReturnsNotFoundProblem()
        {
            var controller = CreateController(
                FakeFirebaseAuthService.ReturningUid("uid-1"), FakeForecastsRepository.ReturningForecasts([]), FakeRatingsRepository.WithMissingForecast());

            var result = await controller.RateForecast(1, ValidRatingRequest, CancellationToken.None);

            var problem = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status404NotFound, problem.StatusCode);
        }

        [Fact]
        public async Task RateForecast_UpsertFails_ReturnsProblem()
        {
            var controller = CreateController(
                FakeFirebaseAuthService.ReturningUid("uid-1"), FakeForecastsRepository.ReturningForecasts([]), FakeRatingsRepository.FailingToUpsert());

            var result = await controller.RateForecast(1, ValidRatingRequest, CancellationToken.None);

            var problem = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status500InternalServerError, problem.StatusCode);
        }

        [Fact]
        public async Task RateForecast_Succeeds_ReturnsOk()
        {
            var controller = CreateController(
                FakeFirebaseAuthService.ReturningUid("uid-1"), FakeForecastsRepository.ReturningForecasts([]), FakeRatingsRepository.Succeeding());

            var result = await controller.RateForecast(1, ValidRatingRequest, CancellationToken.None);

            Assert.IsType<OkResult>(result);
        }

        // --- RemoveRating (UC7 A2) ---

        [Fact]
        public async Task RemoveRating_InvalidToken_ReturnsUnauthorized()
        {
            var controller = CreateController(
                FakeFirebaseAuthService.RejectingToken(), FakeForecastsRepository.ReturningForecasts([]), FakeRatingsRepository.Succeeding());

            var result = await controller.RemoveRating(1, CancellationToken.None);

            Assert.IsType<UnauthorizedResult>(result);
        }

        [Fact]
        public async Task RemoveRating_RemoveFails_ReturnsProblem()
        {
            var controller = CreateController(
                FakeFirebaseAuthService.ReturningUid("uid-1"), FakeForecastsRepository.ReturningForecasts([]), FakeRatingsRepository.FailingToRemove());

            var result = await controller.RemoveRating(1, CancellationToken.None);

            var problem = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status500InternalServerError, problem.StatusCode);
        }

        [Fact]
        public async Task RemoveRating_Succeeds_ReturnsOk()
        {
            var controller = CreateController(
                FakeFirebaseAuthService.ReturningUid("uid-1"), FakeForecastsRepository.ReturningForecasts([]), FakeRatingsRepository.Succeeding());

            var result = await controller.RemoveRating(1, CancellationToken.None);

            Assert.IsType<OkResult>(result);
        }

        private static ForecastsController CreateController(
            IFirebaseAuthService authService, IForecastsRepository forecastsRepository, IRatingsRepository ratingsRepository)
        {
            var forecastsService = new ForecastsService(authService, forecastsRepository, NullLogger<ForecastsService>.Instance);
            var ratingsService = new RatingsService(authService, ratingsRepository, NullLogger<RatingsService>.Instance);
            var controller = new ForecastsController(forecastsService, ratingsService)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext(),
                },
            };

            controller.ControllerContext.HttpContext.Request.Headers.Authorization = "Bearer token";

            return controller;
        }
    }
}
