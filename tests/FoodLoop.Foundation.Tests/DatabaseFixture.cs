using FoodLoop.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
namespace FoodLoop.Foundation.Tests;
public sealed class DatabaseFixture : IAsyncLifetime
{
    // Only this generated, disposable database is created and removed by the tests.
    private readonly string databaseName = "FoodLoop_FoundationTests_" + Guid.NewGuid().ToString("N");
    public string ConnectionString { get; }
    public DatabaseFixture()
    {
        var connection = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("FOODLOOP_TEST_SQLSERVER")
            ?? "Server=(localdb)\\MSSQLLocalDB;Trusted_Connection=True;TrustServerCertificate=True");
        connection.InitialCatalog = databaseName;
        ConnectionString = connection.ConnectionString;
    }
    public ApplicationDbContext CreateContext() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(ConnectionString).Options);
    public async Task InitializeAsync()
    {
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }
    public async Task DisposeAsync()
    {
        var target = new SqlConnectionStringBuilder(ConnectionString).InitialCatalog;
        if (target != databaseName || !target.StartsWith("FoodLoop_FoundationTests_", StringComparison.Ordinal))
            throw new InvalidOperationException("Refusing to remove an unexpected database.");
        await using var db = CreateContext();
        await db.Database.EnsureDeletedAsync();
    }
}
