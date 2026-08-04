using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProductInventory.Api.Middleware;
using ProductInventory.Application.Interfaces;
using ProductInventory.Application.Services;
using ProductInventory.Infrastructure;
using ProductInventory.Infrastructure.Data;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// JSON logging
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options =>
{
    options.JsonWriterOptions = new JsonWriterOptions { Indented = false };
});

// Controllers + global model validation customization
builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var traceId = System.Diagnostics.Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
            var problem = new ValidationProblemDetails(context.ModelState)
            {
                Type = "https://httpstatuses.com/400",
                Title = "Bad Request",
                Status = 400,
                Detail = "One or more validation errors occurred.",
                Extensions =
                {
                    ["traceId"] = traceId,
                    ["errorCode"] = "request.invalid"
                }
            };
            return new BadRequestObjectResult(problem)
            {
                ContentTypes = { "application/problem+json" }
            };
        };
    });

// CORS
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
if (allowedOrigins is { Length: > 0 })
{
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("Frontend", policy =>
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
    });
}
else
{
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("Frontend", policy =>
        {
            policy.SetIsOriginAllowed(_ => false);
        });
    });
}

// Infrastructure (EF Core + Repositories)
var bootstrapLogger = LoggerFactory.Create(config => config.AddJsonConsole()).CreateLogger("Bootstrap");
builder.Services.AddInfrastructure(builder.Configuration, bootstrapLogger);

// Application services
builder.Services.AddScoped<IProductService, ProductService>();

// Exception handler
builder.Services.AddExceptionHandler<ProductExceptionHandler>();
builder.Services.AddProblemDetails();

// OpenAPI + Scalar (only Development or Demo)
if (builder.Environment.IsDevelopment() ||
    builder.Environment.EnvironmentName == "Demo")
{
    builder.Services.AddOpenApi();
}

// Health checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ProductDbContext>("postgresql", tags: ["ready"]);

// Rate limiting for the public demo. Applied to controllers only (health stays exempt) and
// skipped in the Testing environment so integration tests are not throttled.
// ponytail: fixed window per client IP; behind a proxy this degrades to a shared window, which is
// adequate abuse protection for a demo. Tighten/partition further if it ever fronts real traffic.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("public", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 200,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

var app = builder.Build();

// Middleware pipeline
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment() || app.Environment.EnvironmentName == "Demo")
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.Title = "Product Inventory API";
    });
}

app.UseCors("Frontend");
app.UseRateLimiter();

var controllers = app.MapControllers();
if (!app.Environment.IsEnvironment("Testing"))
{
    controllers.RequireRateLimiting("public");
}

// Health endpoints
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false // exclude all checks
});

app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

// Auto-migrate: only AUTO_MIGRATE=true triggers migration
if (builder.Configuration.GetValue<bool>("AUTO_MIGRATE"))
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<ProductDbContext>();
    await context.Database.MigrateAsync();
}
// Do NOT migrate for SeedDemoData alone; EF seeding runs as part of migration via UseSeeding/UseAsyncSeeding

app.Run();

public partial class Program
{
}
