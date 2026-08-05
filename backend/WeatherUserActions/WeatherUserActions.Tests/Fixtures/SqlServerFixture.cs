using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;
using WeatherUserActions.Data;
using Xunit;

namespace WeatherUserActions.Tests.Fixtures
{
    // Spins up a real SQL Server instance via Testcontainers so tests exercise
    // actual constraint/upsert semantics instead of the EF Core InMemory provider.
    public class SqlServerFixture : IAsyncLifetime
    {
        private readonly MsSqlContainer _container =
            new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

        private string ConnectionString => _container.GetConnectionString();

        public async Task InitializeAsync()
        {
            await _container.StartAsync();

            await using var dbContext = CreateDbContext();
            await dbContext.Database.MigrateAsync();
        }

        public async Task DisposeAsync()
        {
            await _container.DisposeAsync();
        }

        public WeatherUserActionsDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<WeatherUserActionsDbContext>()
                .UseSqlServer(ConnectionString)
                .Options;

            return new WeatherUserActionsDbContext(options);
        }
    }

    public static class TestCollections
    {
        public const string SqlServer = "SqlServer collection";
    }

    [CollectionDefinition(TestCollections.SqlServer)]
    public class SqlServerCollection : ICollectionFixture<SqlServerFixture>
    {
    }
}
