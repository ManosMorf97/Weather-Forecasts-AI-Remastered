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
    public class SelectionsControllerTests : IAsyncLifetime
    {
        private static readonly CityDto Athens = new("Athens", "Greece", 37.98m, 23.72m);
        private static readonly CityDto Paris = new("Paris", "France", 48.85m, 2.35m);

        private readonly SqlServerFixture _fixture;

        public SelectionsControllerTests(SqlServerFixture fixture)
        {
            _fixture = fixture;
        }

        public Task InitializeAsync() => _fixture.ResetDatabaseAsync();

        public Task DisposeAsync() => Task.CompletedTask;

        [Fact]
        public async Task SaveSelections_MissingAuthorizationHeader_ReturnsUnauthorized()
        {
            await using var db = _fixture.CreateDbContext();
            var controller = CreateController(db, FakeFirebaseAuthService.ReturningUid("irrelevant"), bearerToken: null);

            var result = await controller.SaveSelections(new SaveSelectionsRequest([Athens], [1]), CancellationToken.None);

            Assert.IsType<UnauthorizedResult>(result);
        }

        [Fact]
        public async Task SaveSelections_UnknownServiceId_ReturnsBadRequestAndPersistsNothing()
        {
            var uid = UniqueUid();
            await using var db = _fixture.CreateDbContext();
            var controller = CreateController(db, FakeFirebaseAuthService.ReturningUid(uid), bearerToken: "token");

            var result = await controller.SaveSelections(
                new SaveSelectionsRequest([Athens], [999_999]), CancellationToken.None);

            var problem = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);

            await using var verifyDb = _fixture.CreateDbContext();
            Assert.Equal(0, await verifyDb.Cities.CountAsync());
            Assert.Equal(0, await verifyDb.UserCitySites.CountAsync());
        }

        [Fact]
        public async Task SaveSelections_NewCityAndService_CreatesCitySiteAndLinksUser()
        {
            var uid = UniqueUid();
            await SeedUserAsync(uid);
            var serviceId = await SeedServiceAsync();
            await using var db = _fixture.CreateDbContext();
            var controller = CreateController(db, FakeFirebaseAuthService.ReturningUid(uid), bearerToken: "token");

            var result = await controller.SaveSelections(
                new SaveSelectionsRequest([Athens], [serviceId]), CancellationToken.None);

            Assert.IsType<NoContentResult>(result);

            await using var verifyDb = _fixture.CreateDbContext();
            var city = Assert.Single(verifyDb.Cities);
            Assert.Equal(Athens.Name, city.Name);

            var citySite = Assert.Single(verifyDb.CitySites);
            Assert.Equal(city.CityId, citySite.CityId);
            Assert.Equal(serviceId, citySite.ServiceId);

            var userCitySite = Assert.Single(verifyDb.UserCitySites);
            Assert.Equal(uid, userCitySite.UserId);
            Assert.Equal(citySite.CitySiteId, userCitySite.CitySiteId);
        }

        [Fact]
        public async Task SaveSelections_CityAlreadyExists_ReusesRowInsteadOfDuplicating()
        {
            var uid = UniqueUid();
            await SeedUserAsync(uid);
            var serviceId = await SeedServiceAsync();
            await using (var seedDb = _fixture.CreateDbContext())
            {
                seedDb.Cities.Add(new City
                {
                    Name = Athens.Name,
                    Country = Athens.Country,
                    Latitude = Athens.Latitude,
                    Longitude = Athens.Longitude,
                });
                await seedDb.SaveChangesAsync();
            }

            await using var db = _fixture.CreateDbContext();
            var controller = CreateController(db, FakeFirebaseAuthService.ReturningUid(uid), bearerToken: "token");

            var result = await controller.SaveSelections(
                new SaveSelectionsRequest([Athens], [serviceId]), CancellationToken.None);

            Assert.IsType<NoContentResult>(result);

            await using var verifyDb = _fixture.CreateDbContext();
            Assert.Equal(1, await verifyDb.Cities.CountAsync());
        }

        [Fact]
        public async Task SaveSelections_CalledAgainWithDifferentCity_RemovesStaleSelectionKeepsCityRow()
        {
            var uid = UniqueUid();
            await SeedUserAsync(uid);
            var serviceId = await SeedServiceAsync();

            await using (var firstCallDb = _fixture.CreateDbContext())
            {
                var controller = CreateController(firstCallDb, FakeFirebaseAuthService.ReturningUid(uid), bearerToken: "token");
                await controller.SaveSelections(new SaveSelectionsRequest([Athens], [serviceId]), CancellationToken.None);
            }

            await using (var secondCallDb = _fixture.CreateDbContext())
            {
                var controller = CreateController(secondCallDb, FakeFirebaseAuthService.ReturningUid(uid), bearerToken: "token");
                var result = await controller.SaveSelections(new SaveSelectionsRequest([Paris], [serviceId]), CancellationToken.None);
                Assert.IsType<NoContentResult>(result);
            }

            await using var verifyDb = _fixture.CreateDbContext();
            // Both City rows still exist (shared reference data) - only the user's link changed.
            Assert.Equal(2, await verifyDb.Cities.CountAsync());
            var userCitySite = Assert.Single(verifyDb.UserCitySites.Include(ucs => ucs.CitySite).ThenInclude(cs => cs.City));
            Assert.Equal(Paris.Name, userCitySite.CitySite.City.Name);
        }

        [Fact]
        public async Task SaveSelections_CalledAgainWithOverlappingCities_PreservesAddedAtForUnchangedRow()
        {
            var uid = UniqueUid();
            await SeedUserAsync(uid);
            var serviceId = await SeedServiceAsync();

            DateTime originalAddedAt;
            await using (var firstCallDb = _fixture.CreateDbContext())
            {
                var controller = CreateController(firstCallDb, FakeFirebaseAuthService.ReturningUid(uid), bearerToken: "token");
                await controller.SaveSelections(new SaveSelectionsRequest([Athens], [serviceId]), CancellationToken.None);
            }

            await using (var readDb = _fixture.CreateDbContext())
            {
                originalAddedAt = (await readDb.UserCitySites.SingleAsync(ucs => ucs.UserId == uid)).AddedAt;
            }

            await using (var secondCallDb = _fixture.CreateDbContext())
            {
                var controller = CreateController(secondCallDb, FakeFirebaseAuthService.ReturningUid(uid), bearerToken: "token");
                await controller.SaveSelections(new SaveSelectionsRequest([Athens, Paris], [serviceId]), CancellationToken.None);
            }

            await using var verifyDb = _fixture.CreateDbContext();
            var selections = await verifyDb.UserCitySites
                .Include(ucs => ucs.CitySite).ThenInclude(cs => cs.City)
                .Where(ucs => ucs.UserId == uid)
                .ToListAsync();

            Assert.Equal(2, selections.Count);
            var athensSelection = Assert.Single(selections, ucs => ucs.CitySite.City.Name == Athens.Name);
            Assert.Equal(originalAddedAt, athensSelection.AddedAt);
        }

        private static string UniqueUid() => $"uid-{Guid.NewGuid():N}";

        private async Task<int> SeedServiceAsync()
        {
            await using var db = _fixture.CreateDbContext();
            var service = new ForecastingService { Name = "OpenWeather", ApiEndpoint = "https://example.test" };
            db.ForecastingServices.Add(service);
            await db.SaveChangesAsync();
            return service.ServiceId;
        }

        // UC4/UC5 precondition: the user is already provisioned (UC2) by the time this endpoint is reached.
        private async Task SeedUserAsync(string uid)
        {
            await using var db = _fixture.CreateDbContext();
            db.Users.Add(new User { UserId = uid, CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }

        private static SelectionsController CreateController(
            WeatherUserActionsDbContext db, IFirebaseAuthService authService, string? bearerToken)
        {
            var repository = new SelectionsRepository(db, NullLogger<SelectionsRepository>.Instance);
            var service = new SelectionsService(authService, repository, NullLogger<SelectionsService>.Instance);
            var controller = new SelectionsController(service)
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
