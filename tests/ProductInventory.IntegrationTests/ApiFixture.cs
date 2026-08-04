using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ProductInventory.Infrastructure.Data;
using Testcontainers.PostgreSql;

namespace ProductInventory.IntegrationTests;

public sealed class ApiFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:17-alpine")
        .Build();

    private WebApplicationFactory<Program> _factory = null!;

    public HttpClient Client { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _database.StartAsync();

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureAppConfiguration((_, configuration) =>
                {
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:ProductsDb"] = _database.GetConnectionString(),
                        ["AUTO_MIGRATE"] = "true",
                        ["SeedDemoData"] = "true",
                        ["Cors:AllowedOrigins:0"] = "http://localhost"
                    });
                });
            });

        Client = _factory.CreateClient();
    }

    public async Task ResetAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ProductDbContext>();

        await context.Database.ExecuteSqlRawAsync("TRUNCATE TABLE products;");
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        Client.Dispose();
        _factory.Dispose();
        await _database.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFixture>
{
    public const string Name = "PostgreSQL API";
}
