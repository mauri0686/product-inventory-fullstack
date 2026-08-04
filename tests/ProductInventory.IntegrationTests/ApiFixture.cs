using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProductInventory.Infrastructure.Data;
using Testcontainers.PostgreSql;

namespace ProductInventory.IntegrationTests;

public sealed class ApiFixture : IAsyncLifetime
{
    private readonly string? _originalProductsDb =
        Environment.GetEnvironmentVariable("ConnectionStrings__ProductsDb");
    private readonly string? _originalAutoMigrate =
        Environment.GetEnvironmentVariable("AUTO_MIGRATE");
    private readonly string? _originalSeedDemoData =
        Environment.GetEnvironmentVariable("SeedDemoData");
    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:17-alpine")
        .Build();

    private WebApplicationFactory<Program> _factory = null!;

    public HttpClient Client { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _database.StartAsync();

        Environment.SetEnvironmentVariable(
            "ConnectionStrings__ProductsDb",
            _database.GetConnectionString());
        Environment.SetEnvironmentVariable("AUTO_MIGRATE", "true");
        Environment.SetEnvironmentVariable("SeedDemoData", "true");

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
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
        Environment.SetEnvironmentVariable("ConnectionStrings__ProductsDb", _originalProductsDb);
        Environment.SetEnvironmentVariable("AUTO_MIGRATE", _originalAutoMigrate);
        Environment.SetEnvironmentVariable("SeedDemoData", _originalSeedDemoData);
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFixture>
{
    public const string Name = "PostgreSQL API";
}
