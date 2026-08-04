using Microsoft.Extensions.Configuration;
using ProductInventory.Infrastructure;

namespace ProductInventory.IntegrationTests;

// Pure parsing logic (no database), so this class stays out of the PostgreSQL collection and
// needs no Docker. It guards the DATABASE_URL path that only runs in hosted deployments.
public sealed class ConnectionStringTests
{
    [Fact]
    public void DatabaseUrl_WithoutExplicitPort_DefaultsToPostgresPort()
    {
        var connection = Build("postgresql://inventory:s3cr3t@dpg-abc123-a/product_inventory");

        Assert.Contains("Port=5432", connection);
        Assert.Contains("Host=dpg-abc123-a", connection);
        Assert.Contains("Database=product_inventory", connection);
        Assert.Contains("SSL Mode=Require", connection);
    }

    [Fact]
    public void DatabaseUrl_WithExplicitPort_KeepsThatPort()
    {
        var connection = Build(
            "postgres://inventory:s3cr3t@dpg-abc123-a.oregon-postgres.render.com:6543/product_inventory");

        Assert.Contains("Port=6543", connection);
    }

    [Fact]
    public void DatabaseUrl_WithEncodedCredentials_IsDecoded()
    {
        var connection = Build("postgresql://inventory:p%40ss%3Aword@dpg-abc123-a/product_inventory");

        Assert.Contains("Username=inventory", connection);
        Assert.Contains("Password=p@ss:word", connection);
    }

    private static string Build(string databaseUrl)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["DATABASE_URL"] = databaseUrl })
            .Build();

        return DependencyInjection.GetConnectionString(configuration);
    }
}
