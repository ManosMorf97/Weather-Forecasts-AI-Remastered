using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using WeatherUserActions.Controllers;
using WeatherUserActions.Data;
using WeatherUserActions.Dtos;
using WeatherUserActions.Models;
using WeatherUserActions.Services;
using WeatherUserActions.Tests.Fakes;
using WeatherUserActions.Tests.Fixtures;
using Xunit;

namespace WeatherUserActions.Tests
{
    [Collection(TestCollections.SqlServer)]
    public class ProfileControllerTests
    {
        private readonly SqlServerFixture _fixture;

        public ProfileControllerTests(SqlServerFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task CreateProfile_MissingAuthorizationHeader_ReturnsUnauthorized()
        {
            await using var db = _fixture.CreateDbContext();
            var controller = CreateController(db, FakeFirebaseAuthService.ReturningUid("irrelevant"), bearerToken: null);

            var result = await controller.CreateProfile(CancellationToken.None);

            Assert.IsType<UnauthorizedResult>(result.Result);
        }

        [Fact]
        public async Task CreateProfile_InvalidToken_ReturnsUnauthorized()
        {
            await using var db = _fixture.CreateDbContext();
            var controller = CreateController(db, FakeFirebaseAuthService.RejectingToken(), bearerToken: "bad-token");

            var result = await controller.CreateProfile(CancellationToken.None);

            Assert.IsType<UnauthorizedResult>(result.Result);
        }

        [Fact]
        public async Task CreateProfile_NewUser_CreatesUserRowAndReportsNoSelection()
        {
            var uid = UniqueUid();
            await using var db = _fixture.CreateDbContext();
            var controller = CreateController(db, FakeFirebaseAuthService.ReturningUid(uid), bearerToken: "token");

            var result = await controller.CreateProfile(CancellationToken.None);

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var body = Assert.IsType<CreateProfileResponse>(ok.Value);
            Assert.False(body.HasCitySiteSelection);

            await using var verifyDb = _fixture.CreateDbContext();
            Assert.NotNull(await verifyDb.Users.FindAsync(uid));
        }

        [Fact]
        public async Task CreateProfile_ExistingUserWithCitySiteSelection_ReportsSelectionTrue()
        {
            var uid = UniqueUid();

            await using (var seedDb = _fixture.CreateDbContext())
            {
                var city = new City { Name = "Athens", Country = "Greece", Latitude = 37.98m, Longitude = 23.72m };
                var service = new ForecastingService { Name = "OpenWeather", ApiEndpoint = "https://example.test" };
                var citySite = new CitySite { City = city, Service = service };
                seedDb.Cities.Add(city);
                seedDb.ForecastingServices.Add(service);
                seedDb.CitySites.Add(citySite);
                seedDb.Users.Add(new User { UserId = uid, CreatedAt = DateTime.UtcNow });
                await seedDb.SaveChangesAsync();

                seedDb.UserCitySites.Add(new UserCitySite
                {
                    UserId = uid,
                    CitySiteId = citySite.CitySiteId,
                    AddedAt = DateTime.UtcNow,
                });
                await seedDb.SaveChangesAsync();
            }

            await using var db = _fixture.CreateDbContext();
            var controller = CreateController(db, FakeFirebaseAuthService.ReturningUid(uid), bearerToken: "token");

            var result = await controller.CreateProfile(CancellationToken.None);

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var body = Assert.IsType<CreateProfileResponse>(ok.Value);
            Assert.True(body.HasCitySiteSelection);
        }

        [Fact]
        public async Task CreateProfile_CalledTwiceForSameUser_IsIdempotent()
        {
            var uid = UniqueUid();

            await using (var firstCallDb = _fixture.CreateDbContext())
            {
                var controller = CreateController(firstCallDb, FakeFirebaseAuthService.ReturningUid(uid), bearerToken: "token");
                await controller.CreateProfile(CancellationToken.None);
            }

            await using (var secondCallDb = _fixture.CreateDbContext())
            {
                var controller = CreateController(secondCallDb, FakeFirebaseAuthService.ReturningUid(uid), bearerToken: "token");
                var result = await controller.CreateProfile(CancellationToken.None);
                Assert.IsType<OkObjectResult>(result.Result);
            }

            await using var verifyDb = _fixture.CreateDbContext();
            var matchingRows = await verifyDb.Users.CountAsync(user => user.UserId == uid);
            Assert.Equal(1, matchingRows);
        }

        private static string UniqueUid() => $"uid-{Guid.NewGuid():N}";

        private static ProfileController CreateController(
            WeatherUserActionsDbContext db, IFirebaseAuthService authService, string? bearerToken)
        {
            var controller = new ProfileController(db, authService, NullLogger<ProfileController>.Instance)
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
