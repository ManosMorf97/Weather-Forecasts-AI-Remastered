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
    // Mocks only at the repository boundary (IProfileRepository) - ProfileService and
    // ProfileController run for real, so these exercise the actual orchestration and
    // Ok/Problem mapping logic without needing Docker/a real database.
    public class ProfileControllerFailureTests
    {
        [Fact]
        public async Task CreateProfile_InvalidToken_ReturnsUnauthorized()
        {
            var controller = CreateController(
                FakeFirebaseAuthService.RejectingToken(), FakeProfileRepository.ReturningSelection(false));

            var result = await controller.CreateProfile(CancellationToken.None);

            Assert.IsType<UnauthorizedResult>(result.Result);
        }

        [Fact]
        public async Task CreateProfile_ProvisioningFails_ReturnsProblem()
        {
            var controller = CreateController(
                FakeFirebaseAuthService.ReturningUid("uid-1"), FakeProfileRepository.FailingToProvision());

            var result = await controller.CreateProfile(CancellationToken.None);

            var problem = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(StatusCodes.Status500InternalServerError, problem.StatusCode);
        }

        [Fact]
        public async Task CreateProfile_CitySiteSelectionCheckFails_ReturnsProblem()
        {
            var controller = CreateController(
                FakeFirebaseAuthService.ReturningUid("uid-1"), FakeProfileRepository.FailingToCheckSelection());

            var result = await controller.CreateProfile(CancellationToken.None);

            var problem = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(StatusCodes.Status500InternalServerError, problem.StatusCode);
        }

        [Fact]
        public async Task CreateProfile_Succeeds_ReturnsOkWithSelection()
        {
            var controller = CreateController(
                FakeFirebaseAuthService.ReturningUid("uid-1"), FakeProfileRepository.ReturningSelection(true));

            var result = await controller.CreateProfile(CancellationToken.None);

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var body = Assert.IsType<CreateProfileResponse>(ok.Value);
            //Checks if the value hascitySiteSelection is Parsed on repo
            Assert.True(body.HasCitySiteSelection);
        }

        private static ProfileController CreateController(
            IFirebaseAuthService authService, IProfileRepository repository)
        {
            var service = new ProfileService(authService, repository, NullLogger<ProfileService>.Instance);
            var controller = new ProfileController(service)
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
