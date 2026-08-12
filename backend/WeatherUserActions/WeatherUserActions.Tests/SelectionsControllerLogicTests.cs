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
    public class SelectionsControllerLogicTests : IAsyncLifetime
    {
        private static readonly CityDto Athens = new("Athens", "Greece", 37.98m, 23.72m);
        private static readonly CityDto Paris = new("Paris", "France", 48.85m, 2.35m);
        private static readonly CityDto Berlin = new("Berlin", "Germany", 52.52m, 13.40m);

        private readonly SqlServerFixture _fixture;

        public SelectionsControllerLogicTests(SqlServerFixture fixture)
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
            await SeedUserAsync(uid);
            var validServiceId = await SeedServiceAsync();
            await using var db = _fixture.CreateDbContext();
            var controller = CreateController(db, FakeFirebaseAuthService.ReturningUid(uid), bearerToken: "token");

            var result = await controller.SaveSelections(
                new SaveSelectionsRequest([Athens], [validServiceId+1, validServiceId]), CancellationToken.None);

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
            var joinedUserCitySites = await verifyDb.UserCitySites
                .Include(ucs => ucs.CitySite).ThenInclude(cs => cs.City)
                .Include(ucs => ucs.CitySite).ThenInclude(cs => cs.Service)
                .ToListAsync();

            var joinedUserCitySite = Assert.Single(joinedUserCitySites);
            Assert.Equal(uid, joinedUserCitySite.UserId);
            Assert.Equal(Athens.Name, joinedUserCitySite.CitySite.City.Name);
            Assert.Equal(Athens.Country, joinedUserCitySite.CitySite.City.Country);
            Assert.Equal("OpenWeather", joinedUserCitySite.CitySite.Service.Name);
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

            var joinedUserCitySites = await verifyDb.UserCitySites
                .Include(ucs => ucs.CitySite).ThenInclude(cs => cs.City)
                .Include(ucs => ucs.CitySite).ThenInclude(cs => cs.Service)
                .ToListAsync();

            var joinedUserCitySite = Assert.Single(joinedUserCitySites);
            Assert.Equal(uid, joinedUserCitySite.UserId);
            Assert.Equal(Athens.Name, joinedUserCitySite.CitySite.City.Name);
            Assert.Equal(Athens.Country, joinedUserCitySite.CitySite.City.Country);
            Assert.Equal("OpenWeather", joinedUserCitySite.CitySite.Service.Name);
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

            var joinedUserCitySites = await verifyDb.UserCitySites
                .Include(ucs => ucs.CitySite).ThenInclude(cs => cs.City)
                .Include(ucs => ucs.CitySite).ThenInclude(cs => cs.Service)
                .ToListAsync();

            var joinedUserCitySite = Assert.Single(joinedUserCitySites);
            Assert.Equal(uid, joinedUserCitySite.UserId);
            Assert.Equal(Paris.Name, joinedUserCitySite.CitySite.City.Name);
            Assert.Equal(Paris.Country, joinedUserCitySite.CitySite.City.Country);
            Assert.Equal("OpenWeather", joinedUserCitySite.CitySite.Service.Name);
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
            var joinedUserCitySites = await verifyDb.UserCitySites
                .Include(ucs => ucs.CitySite).ThenInclude(cs => cs.City)
                .Include(ucs => ucs.CitySite).ThenInclude(cs => cs.Service)
                .OrderBy(ucs => ucs.CitySite.City.Name)
                .ToListAsync();

            Assert.Equal(2, joinedUserCitySites.Count);

            var athensSelection = joinedUserCitySites[0];
            Assert.Equal(uid, athensSelection.UserId);
            Assert.Equal(Athens.Name, athensSelection.CitySite.City.Name);
            Assert.Equal(Athens.Country, athensSelection.CitySite.City.Country);
            Assert.Equal("OpenWeather", athensSelection.CitySite.Service.Name);
            Assert.Equal(originalAddedAt, athensSelection.AddedAt);

            var parisSelection = joinedUserCitySites[1];
            Assert.Equal(uid, parisSelection.UserId);
            Assert.Equal(Paris.Name, parisSelection.CitySite.City.Name);
            Assert.Equal(Paris.Country, parisSelection.CitySite.City.Country);
            Assert.Equal("OpenWeather", parisSelection.CitySite.Service.Name);
        }

        [Fact]
        public async Task SaveSelections_NameMatchesOneRowAndCountryMatchesAnother_DoesNotReuseEitherRow()
        {
            var uid = UniqueUid();
            await SeedUserAsync(uid);
            var serviceId = await SeedServiceAsync();

            // Rows that share a Name with one requested city and a Country with the other,
            // but neither row is an actual Name+Country match for what's being requested.
            await using (var seedDb = _fixture.CreateDbContext())
            {
                seedDb.Cities.AddRange(
                    new City { Name = "Athens", Country = "Germany", Latitude = 1m, Longitude = 1m },
                    new City { Name = "Berlin", Country = "Greece", Latitude = 2m, Longitude = 2m });
                await seedDb.SaveChangesAsync();
            }

            var athensGreece = new CityDto("Athens", "Greece", 37.98m, 23.72m);
            var berlinGermany = new CityDto("Berlin", "Germany", 52.52m, 13.40m);

            await using var db = _fixture.CreateDbContext();
            var controller = CreateController(db, FakeFirebaseAuthService.ReturningUid(uid), bearerToken: "token");

            var result = await controller.SaveSelections(
                new SaveSelectionsRequest([athensGreece, berlinGermany], [serviceId]), CancellationToken.None);

            Assert.IsType<NoContentResult>(result);

            await using var verifyDb = _fixture.CreateDbContext();
            // The two mismatched seed rows must still exist untouched, plus two brand-new
            // rows for the actual Athens/Greece and Berlin/Germany pairs - never reused.
            Assert.Equal(4, await verifyDb.Cities.CountAsync());

            var joinedUserCitySites = await verifyDb.UserCitySites
                .Include(ucs => ucs.CitySite).ThenInclude(cs => cs.City)
                .Include(ucs => ucs.CitySite).ThenInclude(cs => cs.Service)
                .OrderBy(ucs => ucs.CitySite.City.Name)
                .ToListAsync();

            Assert.Equal(2, joinedUserCitySites.Count);

            var athensSelection = joinedUserCitySites[0];
            Assert.Equal(uid, athensSelection.UserId);
            Assert.Equal("Athens", athensSelection.CitySite.City.Name);
            Assert.Equal("Greece", athensSelection.CitySite.City.Country);
            Assert.Equal("OpenWeather", athensSelection.CitySite.Service.Name);

            var berlinSelection = joinedUserCitySites[1];
            Assert.Equal(uid, berlinSelection.UserId);
            Assert.Equal("Berlin", berlinSelection.CitySite.City.Name);
            Assert.Equal("Germany", berlinSelection.CitySite.City.Country);
            Assert.Equal("OpenWeather", berlinSelection.CitySite.Service.Name);
        }
        [Fact]
        public async Task SaveSelections_SameNameAndCountryButDifferentLatLon_DoesNotReuseRow()
        {
            var uid = UniqueUid();
            await SeedUserAsync(uid);
            var serviceId = await SeedServiceAsync();

            // Same Name+Country as Athens, but a different Lat/Long - matching is on the full
            // (Name, Country, Latitude, Longitude) signature, so this row must not be reused.
            await using (var seedDb = _fixture.CreateDbContext())
            {
                seedDb.Cities.Add(new City { Name = Athens.Name, Country = Athens.Country, Latitude = 1m, Longitude = 1m });
                await seedDb.SaveChangesAsync();
            }

            await using var db = _fixture.CreateDbContext();
            var controller = CreateController(db, FakeFirebaseAuthService.ReturningUid(uid), bearerToken: "token");

            var result = await controller.SaveSelections(
                new SaveSelectionsRequest([Athens], [serviceId]), CancellationToken.None);

            Assert.IsType<NoContentResult>(result);

            await using var verifyDb = _fixture.CreateDbContext();
            Assert.Equal(2, await verifyDb.Cities.CountAsync());

            // The original seed row (different Lat/Long) must still exist untouched, alongside
            // the brand-new row for the actual Athens/Greece pair - never merged or overwritten.
            Assert.True(await verifyDb.Cities.AnyAsync(c =>
                c.Name == Athens.Name && c.Country == Athens.Country && c.Latitude == 1m && c.Longitude == 1m));

            var joinedUserCitySites = await verifyDb.UserCitySites
                .Include(ucs => ucs.CitySite).ThenInclude(cs => cs.City)
                .Include(ucs => ucs.CitySite).ThenInclude(cs => cs.Service)
                .ToListAsync();

            var joinedUserCitySite = Assert.Single(joinedUserCitySites);
            Assert.Equal(uid, joinedUserCitySite.UserId);
            Assert.Equal(Athens.Name, joinedUserCitySite.CitySite.City.Name);
            Assert.Equal(Athens.Country, joinedUserCitySite.CitySite.City.Country);
            Assert.Equal(Athens.Latitude, joinedUserCitySite.CitySite.City.Latitude);
            Assert.Equal(Athens.Longitude, joinedUserCitySite.CitySite.City.Longitude);
            Assert.Equal("OpenWeather", joinedUserCitySite.CitySite.Service.Name);
        }
        [Fact]
        public async Task SaveSelections_TwoCitySitesAlreadyExist_ReusesBothRows()
        {
            var uid = UniqueUid();
            await SeedUserAsync(uid);
            var serviceA = await SeedServiceAsync();
            var serviceB = await SeedServiceAsync(name: "WeatherAPI", apiEndpoint: "https://example2.test");

            int citySiteAId, citySiteBId;
            await using (var seedDb = _fixture.CreateDbContext())
            {
                var athensCity = new City { Name = Athens.Name, Country = Athens.Country, Latitude = Athens.Latitude, Longitude = Athens.Longitude };
                seedDb.Cities.Add(athensCity);
                await seedDb.SaveChangesAsync();

                var citySiteA = new CitySite { CityId = athensCity.CityId, ServiceId = serviceA };
                var citySiteB = new CitySite { CityId = athensCity.CityId, ServiceId = serviceB };
                seedDb.CitySites.AddRange(citySiteA, citySiteB);
                await seedDb.SaveChangesAsync();
                citySiteAId = citySiteA.CitySiteId;
                citySiteBId = citySiteB.CitySiteId;
            }

            await using var db = _fixture.CreateDbContext();
            var controller = CreateController(db, FakeFirebaseAuthService.ReturningUid(uid), bearerToken: "token");

            var result = await controller.SaveSelections(
                new SaveSelectionsRequest([Athens], [serviceA, serviceB]), CancellationToken.None);

            Assert.IsType<NoContentResult>(result);

            await using var verifyDb = _fixture.CreateDbContext();
            Assert.Equal(1, await verifyDb.Cities.CountAsync());
            Assert.Equal(2, await verifyDb.CitySites.CountAsync());

            var joinedUserCitySites = await verifyDb.UserCitySites
                .Include(ucs => ucs.CitySite).ThenInclude(cs => cs.City)
                .Include(ucs => ucs.CitySite).ThenInclude(cs => cs.Service)
                .ToListAsync();
            Assert.Equal(2, joinedUserCitySites.Count);

            var siteA = Assert.Single(joinedUserCitySites, ucs => ucs.CitySiteId == citySiteAId);
            Assert.Equal(uid, siteA.UserId);
            Assert.Equal(Athens.Name, siteA.CitySite.City.Name);
            Assert.Equal("OpenWeather", siteA.CitySite.Service.Name);

            var siteB = Assert.Single(joinedUserCitySites, ucs => ucs.CitySiteId == citySiteBId);
            Assert.Equal(uid, siteB.UserId);
            Assert.Equal(Athens.Name, siteB.CitySite.City.Name);
            Assert.Equal("WeatherAPI", siteB.CitySite.Service.Name);
        }

        [Fact]
        public async Task SaveSelections_TwoUsersWithNoCommonCities_EachLinkedOnlyToOwnCitySite()
        {
            var uidA = UniqueUid();
            var uidB = UniqueUid();
            await SeedUserAsync(uidA);
            await SeedUserAsync(uidB);
            var serviceA = await SeedServiceAsync();
            var serviceB = await SeedServiceAsync(name: "WeatherAPI", apiEndpoint: "https://example2.test");

            await SaveAsync(uidA, [Athens], [serviceA]);
            await SaveAsync(uidB, [Paris], [serviceB]);

            await using var verifyDb = _fixture.CreateDbContext();
            Assert.Equal(2, await verifyDb.Cities.CountAsync());
            Assert.Equal(2, await verifyDb.CitySites.CountAsync());

            var joinedUserCitySites = await verifyDb.UserCitySites
                .Include(ucs => ucs.CitySite).ThenInclude(cs => cs.City)
                .Include(ucs => ucs.CitySite).ThenInclude(cs => cs.Service)
                .ToListAsync();
            Assert.Equal(2, joinedUserCitySites.Count);

            var siteA = Assert.Single(joinedUserCitySites, ucs => ucs.UserId == uidA);
            Assert.Equal(Athens.Name, siteA.CitySite.City.Name);
            Assert.Equal("OpenWeather", siteA.CitySite.Service.Name);

            var siteB = Assert.Single(joinedUserCitySites, ucs => ucs.UserId == uidB);
            Assert.Equal(Paris.Name, siteB.CitySite.City.Name);
            Assert.Equal("WeatherAPI", siteB.CitySite.Service.Name);

            Assert.NotEqual(siteA.CitySiteId, siteB.CitySiteId);
        }

        [Fact]
        public async Task SaveSelections_TwoUsersWithOnlyCommonCities_ReusesCityRowButCreatesSeparateCitySites()
        {
            var uidA = UniqueUid();
            var uidB = UniqueUid();
            await SeedUserAsync(uidA);
            await SeedUserAsync(uidB);
            var serviceA = await SeedServiceAsync();
            var serviceB = await SeedServiceAsync(name: "WeatherAPI", apiEndpoint: "https://example2.test");

            await SaveAsync(uidA, [Athens], [serviceA]);
            await SaveAsync(uidB, [Athens], [serviceB]);

            await using var verifyDb = _fixture.CreateDbContext();
            // Same city for both users, but different services - the City row is reused,
            // but each user's (City, Service) combo still needs its own CitySite row.
            Assert.Equal(1, await verifyDb.Cities.CountAsync());
            Assert.Equal(2, await verifyDb.CitySites.CountAsync());

            var joinedUserCitySites = await verifyDb.UserCitySites
                .Include(ucs => ucs.CitySite).ThenInclude(cs => cs.City)
                .Include(ucs => ucs.CitySite).ThenInclude(cs => cs.Service)
                .ToListAsync();
            Assert.Equal(2, joinedUserCitySites.Count);

            var siteA = Assert.Single(joinedUserCitySites, ucs => ucs.UserId == uidA);
            Assert.Equal(Athens.Name, siteA.CitySite.City.Name);
            Assert.Equal("OpenWeather", siteA.CitySite.Service.Name);

            var siteB = Assert.Single(joinedUserCitySites, ucs => ucs.UserId == uidB);
            Assert.Equal(Athens.Name, siteB.CitySite.City.Name);
            Assert.Equal("WeatherAPI", siteB.CitySite.Service.Name);

            Assert.Equal(siteA.CitySite.CityId, siteB.CitySite.CityId);
            Assert.NotEqual(siteA.CitySiteId, siteB.CitySiteId);
        }
        
        [Fact]
        public async Task SaveSelections_TwoUsersWithSomeCommonCities_ReusesSharedCityRowOnly()
        {
            var uidA = UniqueUid();
            var uidB = UniqueUid();
            await SeedUserAsync(uidA);
            await SeedUserAsync(uidB);
            var serviceA = await SeedServiceAsync();
            var serviceB = await SeedServiceAsync(name: "WeatherAPI", apiEndpoint: "https://example2.test");

            await SaveAsync(uidA, [Athens, Paris], [serviceA]);
            await SaveAsync(uidB, [Athens, Berlin], [serviceB]);

            await using var verifyDb = _fixture.CreateDbContext();
            // Athens is shared, Paris and Berlin are each unique to one user.
            Assert.Equal(3, await verifyDb.Cities.CountAsync());
            Assert.Equal(4, await verifyDb.CitySites.CountAsync());

            var joinedUserCitySites = await verifyDb.UserCitySites
                .Include(ucs => ucs.CitySite).ThenInclude(cs => cs.City)
                .Include(ucs => ucs.CitySite).ThenInclude(cs => cs.Service)
                .OrderBy(ucs => ucs.UserId)
                .ThenBy(ucs => ucs.CitySite.City.Name)
                .ToListAsync();
            Assert.Equal(4, joinedUserCitySites.Count);

            var userACitySites = joinedUserCitySites.Where(ucs => ucs.UserId == uidA).ToList();
            Assert.Equal(2, userACitySites.Count);
            Assert.Equal(Athens.Name, userACitySites[0].CitySite.City.Name);
            Assert.Equal("OpenWeather", userACitySites[0].CitySite.Service.Name);
            Assert.Equal(Paris.Name, userACitySites[1].CitySite.City.Name);
            Assert.Equal("OpenWeather", userACitySites[1].CitySite.Service.Name);

            var userBCitySites = joinedUserCitySites.Where(ucs => ucs.UserId == uidB).ToList();
            Assert.Equal(2, userBCitySites.Count);
            Assert.Equal(Athens.Name, userBCitySites[0].CitySite.City.Name);
            Assert.Equal("WeatherAPI", userBCitySites[0].CitySite.Service.Name);
            Assert.Equal(Berlin.Name, userBCitySites[1].CitySite.City.Name);
            Assert.Equal("WeatherAPI", userBCitySites[1].CitySite.Service.Name);

            Assert.Equal(userACitySites[0].CitySite.CityId, userBCitySites[0].CitySite.CityId);
            Assert.NotEqual(userACitySites[0].CitySiteId, userBCitySites[0].CitySiteId);
        }

        [Fact]
        public async Task SaveSelections_TwoUsersWithNoCommonServices_EachLinkedOnlyToOwnCitySite()
        {
            var uidA = UniqueUid();
            var uidB = UniqueUid();
            await SeedUserAsync(uidA);
            await SeedUserAsync(uidB);
            var serviceA = await SeedServiceAsync();
            var serviceB = await SeedServiceAsync(name: "WeatherAPI", apiEndpoint: "https://example2.test");

            await SaveAsync(uidA, [Athens], [serviceA]);
            await SaveAsync(uidB, [Paris], [serviceB]);

            await using var verifyDb = _fixture.CreateDbContext();
            Assert.Equal(2, await verifyDb.Cities.CountAsync());
            Assert.Equal(2, await verifyDb.CitySites.CountAsync());

            var joinedUserCitySites = await verifyDb.UserCitySites
                .Include(ucs => ucs.CitySite).ThenInclude(cs => cs.City)
                .Include(ucs => ucs.CitySite).ThenInclude(cs => cs.Service)
                .ToListAsync();
            Assert.Equal(2, joinedUserCitySites.Count);

            var userACitySites = joinedUserCitySites.Where(ucs => ucs.UserId == uidA).ToList();
            var userAoptions = Assert.Single(userACitySites);
            Assert.Equal(Athens.Name, userAoptions.CitySite.City.Name);
            Assert.Equal("OpenWeather", userAoptions.CitySite.Service.Name);
            Assert.Equal(serviceA, userAoptions.CitySite.ServiceId);

            var userBCitySites = joinedUserCitySites.Where(ucs => ucs.UserId == uidB).ToList();
            var userBoptions = Assert.Single(userBCitySites);
            Assert.Equal(Paris.Name, userBoptions.CitySite.City.Name);
            Assert.Equal("WeatherAPI", userBoptions.CitySite.Service.Name);
            Assert.Equal(serviceB, userBoptions.CitySite.ServiceId);
        }

        [Fact]
        public async Task SaveSelections_TwoUsersWithOnlyCommonServices_CreatesSeparateCitySitesPerCity()
        {
            var uidA = UniqueUid();
            var uidB = UniqueUid();
            await SeedUserAsync(uidA);
            await SeedUserAsync(uidB);
            var sharedService = await SeedServiceAsync();

            await SaveAsync(uidA, [Athens], [sharedService]);
            await SaveAsync(uidB, [Paris], [sharedService]);

            await using var verifyDb = _fixture.CreateDbContext();
            // Same ServiceId for both users, but different cities - sharing a ServiceId must not
            // cause CitySite reuse across cities; each (City, Service) pair is still its own row.
            Assert.Equal(2, await verifyDb.Cities.CountAsync());
            Assert.Equal(2, await verifyDb.CitySites.CountAsync());
            Assert.Equal(1, await verifyDb.ForecastingServices.CountAsync());

            var joinedUserCitySites = await verifyDb.UserCitySites
                .Include(ucs => ucs.CitySite).ThenInclude(cs => cs.City)
                .Include(ucs => ucs.CitySite).ThenInclude(cs => cs.Service)
                .ToListAsync();
            Assert.Equal(2, joinedUserCitySites.Count);
             
            var userAoptions = Assert.Single(joinedUserCitySites, ucs => ucs.UserId == uidA);
            Assert.Equal(Athens.Name, userAoptions.CitySite.City.Name);
            Assert.Equal("OpenWeather", userAoptions.CitySite.Service.Name);

            var userBoptions = Assert.Single(joinedUserCitySites, ucs => ucs.UserId == uidB);
            Assert.Equal(Paris.Name, userBoptions.CitySite.City.Name);
            Assert.Equal("OpenWeather", userBoptions.CitySite.Service.Name);

            Assert.NotEqual(userAoptions.CitySiteId, userBoptions.CitySiteId);
            Assert.Equal(sharedService, userAoptions.CitySite.ServiceId);
            Assert.Equal(sharedService, userBoptions.CitySite.ServiceId);
        }

        [Fact]
        public async Task SaveSelections_TwoUsersWithSomeCommonServices_CreatesSeparateCitySitesDespiteSharedServiceId()
        {
            var uidA = UniqueUid();
            var uidB = UniqueUid();
            await SeedUserAsync(uidA);
            await SeedUserAsync(uidB);
            var sharedService = await SeedServiceAsync();
            var serviceOnlyA = await SeedServiceAsync(name: "WeatherAPI", apiEndpoint: "https://example2.test");
            var serviceOnlyB = await SeedServiceAsync(name: "AccuWeather", apiEndpoint: "https://example3.test");

            await SaveAsync(uidA, [Athens], [sharedService, serviceOnlyA]);
            await SaveAsync(uidB, [Paris], [sharedService, serviceOnlyB]);

            await using var verifyDb = _fixture.CreateDbContext();
            Assert.Equal(2, await verifyDb.Cities.CountAsync());
            Assert.Equal(4, await verifyDb.CitySites.CountAsync());

            var joinedUserCitySites = await verifyDb.UserCitySites
                .Include(ucs => ucs.CitySite).ThenInclude(cs => cs.City)
                .Include(ucs => ucs.CitySite).ThenInclude(cs => cs.Service)
                .ToListAsync();
            Assert.Equal(4, joinedUserCitySites.Count);

            var userACitySites = joinedUserCitySites
                .Where(ucs => ucs.UserId == uidA)
                .OrderBy(ucs => ucs.CitySite.Service.Name)
                .ToList();
            var userBCitySites = joinedUserCitySites
                .Where(ucs => ucs.UserId == uidB)
                .OrderBy(ucs => ucs.CitySite.Service.Name)
                .ToList();
            Assert.Equal(2, userACitySites.Count);
            Assert.Equal(2, userBCitySites.Count);

            // Ordered by service name: OpenWeather (shared) < WeatherAPI (A-only).
            Assert.Equal(Athens.Name, userACitySites[0].CitySite.City.Name);
            Assert.Equal("OpenWeather", userACitySites[0].CitySite.Service.Name);
            Assert.Equal(Athens.Name, userACitySites[1].CitySite.City.Name);
            Assert.Equal("WeatherAPI", userACitySites[1].CitySite.Service.Name);

            // Ordered by service name: AccuWeather (B-only) < OpenWeather (shared).
            Assert.Equal(Paris.Name, userBCitySites[0].CitySite.City.Name);
            Assert.Equal("AccuWeather", userBCitySites[0].CitySite.Service.Name);
            Assert.Equal(Paris.Name, userBCitySites[1].CitySite.City.Name);
            Assert.Equal("OpenWeather", userBCitySites[1].CitySite.Service.Name);

            var sharedForA = userACitySites[0];
            var sharedForB = userBCitySites[1];
            Assert.NotEqual(sharedForA.CitySiteId, sharedForB.CitySiteId);
        }

        [Fact]
        public async Task SaveSelections_TwoUsersWithSomeCommonCitySites_ReusesTheSharedCitySiteRow()
        {
            var uidA = UniqueUid();
            var uidB = UniqueUid();
            await SeedUserAsync(uidA);
            await SeedUserAsync(uidB);
            var sharedService = await SeedServiceAsync();

            await SaveAsync(uidA, [Athens, Paris], [sharedService]);
            await SaveAsync(uidB, [Athens, Berlin], [sharedService]);

            await using var verifyDb = _fixture.CreateDbContext();
            // Athens+sharedService is the one CitySite both users pick - it must be the same row.
            Assert.Equal(3, await verifyDb.Cities.CountAsync());
            Assert.Equal(3, await verifyDb.CitySites.CountAsync());
            Assert.Single(verifyDb.ForecastingServices);
            var joinedUserCitySites = await verifyDb.UserCitySites
                .Include(ucs => ucs.CitySite).ThenInclude(cs => cs.City)
                .Include(ucs => ucs.CitySite).ThenInclude(cs => cs.Service)
                .OrderBy(ucs => ucs.UserId)
                .ThenBy(ucs => ucs.CitySite.City.Name)
                .ToListAsync();
            Assert.Equal(4, joinedUserCitySites.Count);

            var userACitySites = joinedUserCitySites.Where(ucs => ucs.UserId == uidA).ToList();
            Assert.Equal(2, userACitySites.Count);
            Assert.Equal(uidA, userACitySites[0].UserId);
            Assert.Equal(Athens.Name, userACitySites[0].CitySite.City.Name);
            Assert.Equal("OpenWeather", userACitySites[0].CitySite.Service.Name);
            Assert.Equal(uidA, userACitySites[1].UserId);
            Assert.Equal(Paris.Name, userACitySites[1].CitySite.City.Name);
            Assert.Equal("OpenWeather", userACitySites[1].CitySite.Service.Name);

            var userBCitySites = joinedUserCitySites.Where(ucs => ucs.UserId == uidB).ToList();
            Assert.Equal(2, userBCitySites.Count);
            Assert.Equal(uidB, userBCitySites[0].UserId);
            Assert.Equal(Athens.Name, userBCitySites[0].CitySite.City.Name);
            Assert.Equal("OpenWeather", userBCitySites[0].CitySite.Service.Name);
            Assert.Equal(uidB, userBCitySites[1].UserId);
            Assert.Equal(Berlin.Name, userBCitySites[1].CitySite.City.Name);
            Assert.Equal("OpenWeather", userBCitySites[1].CitySite.Service.Name);

            Assert.Equal(userACitySites[0].CitySiteId, userBCitySites[0].CitySiteId);
        }

        [Fact]
        public async Task SaveSelections_TwoUsersWithAllCommonCitySites_ReusesBothCitySiteRowsForBothUsers()
        {
            var uidA = UniqueUid();
            var uidB = UniqueUid();
            await SeedUserAsync(uidA);
            await SeedUserAsync(uidB);
            var sharedService = await SeedServiceAsync();

            await SaveAsync(uidA, [Athens, Paris], [sharedService]);
            await SaveAsync(uidB, [Athens, Paris], [sharedService]);

            await using var verifyDb = _fixture.CreateDbContext();
            // Both users request the exact same (City, Service) pairs - both CitySite rows
            // are shared, not duplicated per user.
            Assert.Equal(2, await verifyDb.Cities.CountAsync());
            Assert.Equal(2, await verifyDb.CitySites.CountAsync());
            Assert.Equal(4, await verifyDb.UserCitySites.CountAsync());

            var joinedUserCitySites = await verifyDb.UserCitySites
                .Include(ucs => ucs.CitySite).ThenInclude(cs => cs.City)
                .Include(ucs => ucs.CitySite).ThenInclude(cs => cs.Service)
                .OrderBy(ucs => ucs.UserId)
                .ThenBy(ucs => ucs.CitySite.City.Name)
                .ToListAsync();
            Assert.Equal(4, joinedUserCitySites.Count);

            var userACitySites = joinedUserCitySites.Where(ucs => ucs.UserId == uidA).ToList();
            Assert.Equal(2, userACitySites.Count);
            Assert.Equal(uidA, userACitySites[0].UserId);
            Assert.Equal(Athens.Name, userACitySites[0].CitySite.City.Name);
            Assert.Equal("OpenWeather", userACitySites[0].CitySite.Service.Name);
            Assert.Equal(uidA, userACitySites[1].UserId);
            Assert.Equal(Paris.Name, userACitySites[1].CitySite.City.Name);
            Assert.Equal("OpenWeather", userACitySites[1].CitySite.Service.Name);

            var userBCitySites = joinedUserCitySites.Where(ucs => ucs.UserId == uidB).ToList();
            Assert.Equal(2, userBCitySites.Count);
            Assert.Equal(uidB, userBCitySites[0].UserId);
            Assert.Equal(Athens.Name, userBCitySites[0].CitySite.City.Name);
            Assert.Equal("OpenWeather", userBCitySites[0].CitySite.Service.Name);
            Assert.Equal(uidB, userBCitySites[1].UserId);
            Assert.Equal(Paris.Name, userBCitySites[1].CitySite.City.Name);
            Assert.Equal("OpenWeather", userBCitySites[1].CitySite.Service.Name);

            var aCitySiteIds = userACitySites.Select(ucs => ucs.CitySiteId).ToHashSet();
            var bCitySiteIds = userBCitySites.Select(ucs => ucs.CitySiteId).ToHashSet();
            Assert.Equal(aCitySiteIds, bCitySiteIds);
        }

        [Fact]
        public async Task SaveSelections_UserHasPendingUserServices_ClearsThemForThatUserOnly()
        {
            var uid = UniqueUid();
            var otherUid = UniqueUid();
            await SeedUserAsync(uid);
            await SeedUserAsync(otherUid);
            var serviceId = await SeedServiceAsync();

            // Pending service-only picks (no city yet) for both users - only the requesting
            // user's rows should be cleared once their city selection materializes.
            await using (var seedDb = _fixture.CreateDbContext())
            {
                seedDb.UserServices.Add(new UserService { UserId = uid, ServiceId = serviceId, AddedAt = DateTime.UtcNow });
                seedDb.UserServices.Add(new UserService { UserId = otherUid, ServiceId = serviceId, AddedAt = DateTime.UtcNow });
                await seedDb.SaveChangesAsync();
            }

            await using var db = _fixture.CreateDbContext();
            var controller = CreateController(db, FakeFirebaseAuthService.ReturningUid(uid), bearerToken: "token");

            var result = await controller.SaveSelections(
                new SaveSelectionsRequest([Athens], [serviceId]), CancellationToken.None);

            Assert.IsType<NoContentResult>(result);

            await using var verifyDb = _fixture.CreateDbContext();
            var remainingUserServices = await verifyDb.UserServices.ToListAsync();
            var remainingUserService = Assert.Single(remainingUserServices);
            Assert.Equal(otherUid, remainingUserService.UserId);
        }

        private static string UniqueUid() => $"uid-{Guid.NewGuid():N}";

        private async Task<int> SeedServiceAsync(string name="OpenWeather", string apiEndpoint="https://example.test")
        {
            await using var db = _fixture.CreateDbContext();
            var service = new ForecastingService { Name = name, ApiEndpoint = apiEndpoint };
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

        private async Task SaveAsync(string uid, List<CityDto> cities, List<int> serviceIds)
        {
            await using var db = _fixture.CreateDbContext();
            var controller = CreateController(db, FakeFirebaseAuthService.ReturningUid(uid), bearerToken: "token");
            var result = await controller.SaveSelections(new SaveSelectionsRequest(cities, serviceIds), CancellationToken.None);
            Assert.IsType<NoContentResult>(result);
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
