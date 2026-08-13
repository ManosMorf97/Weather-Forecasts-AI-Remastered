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
    // Mocks only at the repository boundary (IUserServicesRepository) - UserServicesService and
    // UserServicesController run for real, so these exercise the actual orchestration and
    // NoContent/Problem mapping logic without needing Docker/a real database.
    public class UserServicesControllerResponseTests
    {
        private static readonly SaveServicesRequest ValidRequest = new(ServiceIds: [1]);

        [Fact]
        public async Task SaveServices_InvalidToken_ReturnsUnauthorized()
        {
            var controller = CreateController(
                FakeFirebaseAuthService.RejectingToken(), FakeUserServicesRepository.Succeeding());

            var result = await controller.SaveServices(ValidRequest, CancellationToken.None);

            Assert.IsType<UnauthorizedResult>(result);
        }

        [Fact]
        public async Task SaveServices_ServiceIdValidationFails_ReturnsProblem()
        {
            var controller = CreateController(
                FakeFirebaseAuthService.ReturningUid("uid-1"), FakeUserServicesRepository.FailingToValidateServiceIds());

            var result = await controller.SaveServices(ValidRequest, CancellationToken.None);

            var problem = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status500InternalServerError, problem.StatusCode);
        }

        [Fact]
        public async Task SaveServices_InvalidServiceIds_ReturnsBadRequestProblem()
        {
            var controller = CreateController(
                FakeFirebaseAuthService.ReturningUid("uid-1"), FakeUserServicesRepository.WithInvalidServiceIds());

            var result = await controller.SaveServices(ValidRequest, CancellationToken.None);

            var problem = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        }

        [Fact]
        public async Task SaveServices_ReplaceFails_ReturnsProblem()
        {
            var controller = CreateController(
                FakeFirebaseAuthService.ReturningUid("uid-1"), FakeUserServicesRepository.FailingToReplaceServices());

            var result = await controller.SaveServices(ValidRequest, CancellationToken.None);

            var problem = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status500InternalServerError, problem.StatusCode);
        }

        [Fact]
        public async Task SaveServices_Succeeds_ReturnsNoContent()
        {
            var controller = CreateController(
                FakeFirebaseAuthService.ReturningUid("uid-1"), FakeUserServicesRepository.Succeeding());

            var result = await controller.SaveServices(ValidRequest, CancellationToken.None);

            Assert.IsType<NoContentResult>(result);
        }

        private static UserServicesController CreateController(
            IFirebaseAuthService authService, IUserServicesRepository repository)
        {
            var service = new UserServicesService(authService, repository, NullLogger<UserServicesService>.Instance);
            var controller = new UserServicesController(service)
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
