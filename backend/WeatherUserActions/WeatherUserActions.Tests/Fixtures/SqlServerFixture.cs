using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Respawn;
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

        private Respawner _respawner = null!;

        private string ConnectionString => _container.GetConnectionString();

        public async Task InitializeAsync()
        {
            await _container.StartAsync();

            await using var dbContext = CreateDbContext();
            await dbContext.Database.MigrateAsync();

            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();
            _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
            {
                DbAdapter = DbAdapter.SqlServer,
            });
        }

        public async Task DisposeAsync()
        {
            await _container.DisposeAsync();
        }

        // Wipes all table data (schema/constraints untouched) so each test starts clean.
        public async Task ResetDatabaseAsync()
        {
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();
            await _respawner.ResetAsync(connection);
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
