using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using ProductInventory.Application.Interfaces;
using ProductInventory.Domain.Entities;
using ProductInventory.Infrastructure.Data;
using ProductInventory.Infrastructure.Data.Repositories;

namespace ProductInventory.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        ILogger? bootstrapLogger = null)
    {
        var connectionString = GetConnectionString(configuration);
        var seedDemoData = bool.TryParse(configuration["SeedDemoData"], out var shouldSeed)
            && shouldSeed;
        bootstrapLogger?.LogInformation("Configuring PostgreSQL database connection.");

        services.AddDbContext<ProductDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(ProductDbContext).Assembly.GetName().Name);
            });

            if (seedDemoData)
            {
                options.UseAsyncSeeding(async (context, _, ct) =>
                {
                    if (await context.Set<Product>().AnyAsync(ct))
                        return;

                    var products = new List<Product>();
                    for (int i = 1; i <= 100; i++)
                    {
                        var id = new Guid($"00000000-0000-0000-0000-{i:D12}");
                        var price = Math.Round(10m + (i * 7.89m), 2);
                        var quantity = i * 7 % 100;
                        var isActive = i % 5 != 0;
                        var name = $"Product {i:D3}";
                        var product = Product.Create(id, name, price, quantity, isActive);
                        products.Add(product);
                    }
                    context.Set<Product>().AddRange(products);
                    await context.SaveChangesAsync(ct);
                });
                options.UseSeeding((context, _) =>
                {
                    if (context.Set<Product>().Any())
                        return;

                    var products = new List<Product>();
                    for (int i = 1; i <= 100; i++)
                    {
                        var id = new Guid($"00000000-0000-0000-0000-{i:D12}");
                        var price = Math.Round(10m + (i * 7.89m), 2);
                        var quantity = i * 7 % 100;
                        var isActive = i % 5 != 0;
                        var name = $"Product {i:D3}";
                        var product = Product.Create(id, name, price, quantity, isActive);
                        products.Add(product);
                    }
                    context.Set<Product>().AddRange(products);
                    context.SaveChanges();
                });
            }
        });

        services.AddScoped<IProductRepository, ProductRepository>();

        return services;
    }

    public static string GetConnectionString(IConfiguration configuration)
    {
        var databaseUrl = configuration["DATABASE_URL"];
        if (!string.IsNullOrWhiteSpace(databaseUrl))
            return ConvertPostgresUrlToConnectionString(databaseUrl);

        var connStr = configuration.GetConnectionString("ProductsDb");
        if (!string.IsNullOrWhiteSpace(connStr))
            return connStr;

        throw new InvalidOperationException(
            "Connection string 'ConnectionStrings:ProductsDb' or 'DATABASE_URL' must be configured.");
    }

    private static string ConvertPostgresUrlToConnectionString(string url)
    {
        var uri = new Uri(url);
        var separatorIndex = uri.UserInfo.IndexOf(':');
        var host = uri.Host;
        var port = uri.Port;
        var database = uri.AbsolutePath.TrimStart('/');
        var username = separatorIndex >= 0
            ? Uri.UnescapeDataString(uri.UserInfo[..separatorIndex])
            : Uri.UnescapeDataString(uri.UserInfo);
        var password = separatorIndex >= 0
            ? Uri.UnescapeDataString(uri.UserInfo[(separatorIndex + 1)..])
            : string.Empty;

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = host,
            Port = port,
            Database = database,
            Username = username,
            Password = password,
            SslMode = SslMode.Require
        };

        return builder.ConnectionString;
    }
}
