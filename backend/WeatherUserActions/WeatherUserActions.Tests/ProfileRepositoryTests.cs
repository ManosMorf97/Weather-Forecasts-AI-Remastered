using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using WeatherUserActions.Data;
using WeatherUserActions.Repositories;
using WeatherUserActions.Tests.Fixtures;
using Xunit;

namespace WeatherUserActions.Tests
{
    // Exercises the DbException/DbUpdateException handling in ProfileRepository against
    // the real Testcontainers SQL Server - no mocking of EF Core or ADO.NET exception types.
    [Collection(TestCollections.SqlServer)]
    public class ProfileRepositoryTests : IAsyncLifetime
    {
        private readonly SqlServerFixture _fixture;

        public ProfileRepositoryTests(SqlServerFixture fixture)
        {
            _fixture = fixture;
        }

        public Task InitializeAsync() => _fixture.ResetDatabaseAsync();

        public Task DisposeAsync() => Task.CompletedTask;

        [Fact]
        public async Task EnsureUserProvisionedAsync_ConcurrentCallsForSameUser_BothSucceedAndOnlyOneRowPersists()
        {
            var uid = UniqueUid();

            // Forces both inserts to actually collide at the DB, so this test exercises the
            // "someone else already inserted it" DbUpdateException recovery path (ProfileRepository.cs)
            // every run, instead of only when the two calls happen to overlap on their own.
            var barrier = new ConcurrentSaveBarrier(participantCount: 2);
            await using var dbA = _fixture.CreateDbContext(barrier);
            await using var dbB = _fixture.CreateDbContext(barrier);
            var repositoryA = new ProfileRepository(dbA, NullLogger<ProfileRepository>.Instance);
            var repositoryB = new ProfileRepository(dbB, NullLogger<ProfileRepository>.Instance);

            var results = await Task.WhenAll(
                repositoryA.EnsureUserProvisionedAsync(uid),
                repositoryB.EnsureUserProvisionedAsync(uid));

            Assert.All(results, Assert.True);

            await using var verifyDb = _fixture.CreateDbContext();
            Assert.Equal(1, await verifyDb.Users.CountAsync(user => user.UserId == uid));
        }

        [Fact]
        public async Task EnsureUserProvisionedAsync_PersistentInsertFailure_ReturnsFalse()
        {
            // Exceeds UserId's nvarchar(128) column, so SQL Server rejects the insert
            // with no chance of a "someone else already inserted it" recovery.
            var tooLongUserId = new string('a', 200);
            await using var db = _fixture.CreateDbContext();
            var repository = new ProfileRepository(db, NullLogger<ProfileRepository>.Instance);

            var succeeded = await repository.EnsureUserProvisionedAsync(tooLongUserId);

            Assert.False(succeeded);
        }

        [Fact]
        public async Task TryGetHasCitySiteSelectionAsync_DatabaseUnreachable_ReturnsFailure()
        {
            await using var brokenDb = CreateUnreachableDbContext();
            var repository = new ProfileRepository(brokenDb, NullLogger<ProfileRepository>.Instance);

            var (succeeded, hasCitySiteSelection) = await repository.TryGetHasCitySiteSelectionAsync(UniqueUid());

            Assert.False(succeeded);
            Assert.False(hasCitySiteSelection);
        }

        private static string UniqueUid() => $"uid-{Guid.NewGuid():N}";

        private static WeatherUserActionsDbContext CreateUnreachableDbContext()
        {
            var options = new DbContextOptionsBuilder<WeatherUserActionsDbContext>()
                .UseSqlServer("Server=127.0.0.1,1;Database=doesnotexist;User Id=sa;Password=wrong;Connect Timeout=1;TrustServerCertificate=true")
                .Options;

            return new WeatherUserActionsDbContext(options);
        }
    }
}
