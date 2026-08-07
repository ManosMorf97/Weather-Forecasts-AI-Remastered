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
    // Mocks only at the repository boundary (ISelectionsRepository) - SelectionsService and
    // SelectionsController run for real, so these exercise the actual orchestration and
    // NoContent/Problem mapping logic without needing Docker/a real database.
    public class SelectionsControllerFailureTests
    {
        private static readonly SaveSelectionsRequest ValidRequest = new(
            Cities: [new CityDto("Athens", "Greece", 37.98m, 23.72m)],
            ServiceIds: [1]);

        [Fact]
        public async Task SaveSelections_InvalidToken_ReturnsUnauthorized()
        {
            var controller = CreateController(
                FakeFirebaseAuthService.RejectingToken(), FakeSelectionsRepository.Succeeding());

            var result = await controller.SaveSelections(ValidRequest, CancellationToken.None);

            Assert.IsType<UnauthorizedResult>(result);
        }

        [Fact]
        public async Task SaveSelections_ServiceIdValidationFails_ReturnsProblem()
        {
            var controller = CreateController(
                FakeFirebaseAuthService.ReturningUid("uid-1"), FakeSelectionsRepository.FailingToValidateServiceIds());

            var result = await controller.SaveSelections(ValidRequest, CancellationToken.None);

            var problem = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status500InternalServerError, problem.StatusCode);
        }

        [Fact]
        public async Task SaveSelections_InvalidServiceIds_ReturnsBadRequestProblem()
        {
            var controller = CreateController(
                FakeFirebaseAuthService.ReturningUid("uid-1"), FakeSelectionsRepository.WithInvalidServiceIds());

            var result = await controller.SaveSelections(ValidRequest, CancellationToken.None);

            var problem = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        }

        [Fact]
        public async Task SaveSelections_ReplaceFails_ReturnsProblem()
        {
            var controller = CreateController(
                FakeFirebaseAuthService.ReturningUid("uid-1"), FakeSelectionsRepository.FailingToReplaceSelection());

            var result = await controller.SaveSelections(ValidRequest, CancellationToken.None);

            var problem = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status500InternalServerError, problem.StatusCode);
        }

        [Fact]
        public async Task SaveSelections_Succeeds_ReturnsNoContent()
        {
            var controller = CreateController(
                FakeFirebaseAuthService.ReturningUid("uid-1"), FakeSelectionsRepository.Succeeding());

            var result = await controller.SaveSelections(ValidRequest, CancellationToken.None);

            Assert.IsType<NoContentResult>(result);
        }

        private static SelectionsController CreateController(
            IFirebaseAuthService authService, ISelectionsRepository repository)
        {
            var service = new SelectionsService(authService, repository, NullLogger<SelectionsService>.Instance);
            var controller = new SelectionsController(service)
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
