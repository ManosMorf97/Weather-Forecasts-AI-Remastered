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
    // Mocks only at the repository boundary (IForecastsRepository) - ForecastsService and
    // ForecastsController run for real, so these exercise the actual orchestration and
    // Ok/Problem mapping logic without needing Docker/a real database.
    public class ForecastsControllerResponseTests
    {
        [Fact]
        public async Task GetForecasts_InvalidToken_ReturnsUnauthorized()
        {
            var controller = CreateController(
                FakeFirebaseAuthService.RejectingToken(), FakeForecastsRepository.ReturningForecasts([]));

            var result = await controller.GetForecasts(CancellationToken.None);

            Assert.IsType<UnauthorizedResult>(result.Result);
        }

        [Fact]
        public async Task GetForecasts_LoadFails_ReturnsProblem()
        {
            var controller = CreateController(
                FakeFirebaseAuthService.ReturningUid("uid-1"), FakeForecastsRepository.FailingToLoadForecasts());

            var result = await controller.GetForecasts(CancellationToken.None);

            var problem = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(StatusCodes.Status500InternalServerError, problem.StatusCode);
        }

        [Fact]
        public async Task GetForecasts_Succeeds_ReturnsOkWithForecasts()
        {
            var forecasts = new List<ForecastItemDto>
            {
                new("Athens", "Greece", "OpenWeather", "CURRENT", DateTime.UtcNow.AddHours(1), 28.5m, 40m, 12m, false),
            };
            var controller = CreateController(
                FakeFirebaseAuthService.ReturningUid("uid-1"), FakeForecastsRepository.ReturningForecasts(forecasts));

            var result = await controller.GetForecasts(CancellationToken.None);

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var body = Assert.IsType<GetForecastsResponse>(ok.Value);
            Assert.Equal(forecasts, body.Forecasts);
        }

        private static ForecastsController CreateController(
            IFirebaseAuthService authService, IForecastsRepository repository)
        {
            var service = new ForecastsService(authService, repository, NullLogger<ForecastsService>.Instance);
            var controller = new ForecastsController(service)
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
