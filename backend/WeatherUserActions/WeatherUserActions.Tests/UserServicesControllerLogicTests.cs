using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using WeatherUserActions.Controllers;
using WeatherUserActions.Data;
using WeatherUserActions.Dtos;
using WeatherUserActions.FirebaseServices;
using WeatherUserActions.Models;
using WeatherUserActions.Repositories;
using WeatherUserActions.Services;
using WeatherUserActions.Tests.Fakes;
using WeatherUserActions.Tests.Fixtures;
using Xunit;

namespace WeatherUserActions.Tests
{
    [Collection(TestCollections.SqlServer)]
    public class UserServicesControllerLogicTests : IAsyncLifetime
    {
        private readonly SqlServerFixture _fixture;

        public UserServicesControllerLogicTests(SqlServerFixture fixture)
        {
            _fixture = fixture;
        }

        public Task InitializeAsync() => _fixture.ResetDatabaseAsync();

        public Task DisposeAsync() => Task.CompletedTask;

        [Fact]
        public async Task SaveServices_MissingAuthorizationHeader_ReturnsUnauthorized()
        {
            await using var db = _fixture.CreateDbContext();
            var controller = CreateController(db, FakeFirebaseAuthService.ReturningUid("irrelevant"), bearerToken: null);

            var result = await controller.SaveServices(new SaveServicesRequest([1]), CancellationToken.None);

            Assert.IsType<UnauthorizedResult>(result);
        }

        [Fact]
        public async Task SaveServices_UnknownServiceId_ReturnsBadRequestAndPersistsNothing()
        {
            var uid = UniqueUid();
            await SeedUserAsync(uid);
            var validServiceId = await SeedServiceAsync();
            await using var db = _fixture.CreateDbContext();
            var controller = CreateController(db, FakeFirebaseAuthService.ReturningUid(uid), bearerToken: "token");

            var result = await controller.SaveServices(
                new SaveServicesRequest([validServiceId + 1, validServiceId]), CancellationToken.None);

            var problem = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);

            await using var verifyDb = _fixture.CreateDbContext();
            Assert.Equal(0, await verifyDb.UserServices.CountAsync());
        }

        [Fact]
        public async Task SaveServices_NewServices_CreatesPendingUserServiceRows()
        {
            var uid = UniqueUid();
            await SeedUserAsync(uid);
            var serviceId = await SeedServiceAsync();
            await using var db = _fixture.CreateDbContext();
            var controller = CreateController(db, FakeFirebaseAuthService.ReturningUid(uid), bearerToken: "token");

            var result = await controller.SaveServices(new SaveServicesRequest([serviceId]), CancellationToken.None);

            Assert.IsType<NoContentResult>(result);

            await using var verifyDb = _fixture.CreateDbContext();
            var joinedUserServices = await verifyDb.UserServices
                .Include(us => us.Service)
                .ToListAsync();

            var joinedUserService = Assert.Single(joinedUserServices);
            Assert.Equal(uid, joinedUserService.UserId);
            Assert.Equal("OpenWeather", joinedUserService.Service.Name);
            Assert.NotEqual(default, joinedUserService.AddedAt);
        }

        [Fact]
        public async Task SaveServices_CalledAgainWithDifferentServices_ReplacesPendingSelection()
        {
            var uid = UniqueUid();
            await SeedUserAsync(uid);
            var serviceA = await SeedServiceAsync();
            var serviceB = await SeedServiceAsync(name: "WeatherAPI", apiEndpoint: "https://example2.test");

            await using (var firstCallDb = _fixture.CreateDbContext())
            {
                var controller = CreateController(firstCallDb, FakeFirebaseAuthService.ReturningUid(uid), bearerToken: "token");
                await controller.SaveServices(new SaveServicesRequest([serviceA]), CancellationToken.None);
            }

            await using (var secondCallDb = _fixture.CreateDbContext())
            {
                var controller = CreateController(secondCallDb, FakeFirebaseAuthService.ReturningUid(uid), bearerToken: "token");
                var result = await controller.SaveServices(new SaveServicesRequest([serviceB]), CancellationToken.None);
                Assert.IsType<NoContentResult>(result);
            }

            await using var verifyDb = _fixture.CreateDbContext();
            var joinedUserServices = await verifyDb.UserServices
                .Include(us => us.Service)
                .ToListAsync();

            var joinedUserService = Assert.Single(joinedUserServices);
            Assert.Equal(uid, joinedUserService.UserId);
            Assert.Equal("WeatherAPI", joinedUserService.Service.Name);
        }

        [Fact]
        public async Task SaveServices_CalledAgainWithOverlappingServices_PreservesAddedAtForUnchangedRow()
        {
            var uid = UniqueUid();
            await SeedUserAsync(uid);
            var serviceA = await SeedServiceAsync();
            var serviceB = await SeedServiceAsync(name: "WeatherAPI", apiEndpoint: "https://example2.test");

            DateTime originalAddedAt;
            await using (var firstCallDb = _fixture.CreateDbContext())
            {
                var controller = CreateController(firstCallDb, FakeFirebaseAuthService.ReturningUid(uid), bearerToken: "token");
                await controller.SaveServices(new SaveServicesRequest([serviceA]), CancellationToken.None);
            }

            await using (var readDb = _fixture.CreateDbContext())
            {
                originalAddedAt = (await readDb.UserServices.SingleAsync(us => us.UserId == uid)).AddedAt;
            }

            await using (var secondCallDb = _fixture.CreateDbContext())
            {
                var controller = CreateController(secondCallDb, FakeFirebaseAuthService.ReturningUid(uid), bearerToken: "token");
                await controller.SaveServices(new SaveServicesRequest([serviceA, serviceB]), CancellationToken.None);
            }

            await using var verifyDb = _fixture.CreateDbContext();
            var joinedUserServices = await verifyDb.UserServices
                .Include(us => us.Service)
                .OrderBy(us => us.Service.Name)
                .ToListAsync();

            Assert.Equal(2, joinedUserServices.Count);

            var openWeatherSelection = joinedUserServices[0];
            Assert.Equal(uid, openWeatherSelection.UserId);
            Assert.Equal("OpenWeather", openWeatherSelection.Service.Name);
            Assert.Equal(originalAddedAt, openWeatherSelection.AddedAt);

            var weatherApiSelection = joinedUserServices[1];
            Assert.Equal(uid, weatherApiSelection.UserId);
            Assert.Equal("WeatherAPI", weatherApiSelection.Service.Name);
        }

        [Fact]
        public async Task SaveServices_TwoUsers_EachOnlyAffectsOwnPendingRows()
        {
            var uidA = UniqueUid();
            var uidB = UniqueUid();
            await SeedUserAsync(uidA);
            await SeedUserAsync(uidB);
            var serviceA = await SeedServiceAsync();
            var serviceB = await SeedServiceAsync(name: "WeatherAPI", apiEndpoint: "https://example2.test");

            await SaveAsync(uidA, [serviceA]);
            await SaveAsync(uidB, [serviceB]);

            await using var verifyDb = _fixture.CreateDbContext();
            var joinedUserServices = await verifyDb.UserServices
                .Include(us => us.Service)
                .ToListAsync();
            Assert.Equal(2, joinedUserServices.Count);

            var userA = Assert.Single(joinedUserServices, us => us.UserId == uidA);
            Assert.Equal("OpenWeather", userA.Service.Name);

            var userB = Assert.Single(joinedUserServices, us => us.UserId == uidB);
            Assert.Equal("WeatherAPI", userB.Service.Name);
        }

        private static string UniqueUid() => $"uid-{Guid.NewGuid():N}";

        private async Task<int> SeedServiceAsync(string name = "OpenWeather", string apiEndpoint = "https://example.test")
        {
            await using var db = _fixture.CreateDbContext();
            var service = new ForecastingService { Name = name, ApiEndpoint = apiEndpoint };
            db.ForecastingServices.Add(service);
            await db.SaveChangesAsync();
            return service.ServiceId;
        }

        // UC5 precondition: the user is already provisioned (UC2) by the time this endpoint is reached.
        private async Task SeedUserAsync(string uid)
        {
            await using var db = _fixture.CreateDbContext();
            db.Users.Add(new User { UserId = uid, CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }

        private async Task SaveAsync(string uid, List<int> serviceIds)
        {
            await using var db = _fixture.CreateDbContext();
            var controller = CreateController(db, FakeFirebaseAuthService.ReturningUid(uid), bearerToken: "token");
            var result = await controller.SaveServices(new SaveServicesRequest(serviceIds), CancellationToken.None);
            Assert.IsType<NoContentResult>(result);
        }

        private static UserServicesController CreateController(
            WeatherUserActionsDbContext db, IFirebaseAuthService authService, string? bearerToken)
        {
            var repository = new UserServicesRepository(db, NullLogger<UserServicesRepository>.Instance);
            var service = new UserServicesService(authService, repository, NullLogger<UserServicesService>.Instance);
            var controller = new UserServicesController(service)
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
