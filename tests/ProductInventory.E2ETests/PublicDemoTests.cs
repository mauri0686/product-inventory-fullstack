using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Xunit;

namespace ProductInventory.E2ETests;

public sealed class PublicDemoTests : IAsyncLifetime
{
    private readonly string _webBaseUrl = Environment.GetEnvironmentVariable("WEB_BASE_URL")
        ?? "https://mauri0686.github.io/product-inventory-fullstack/";
    private readonly string _apiBaseUrl = Environment.GetEnvironmentVariable("API_BASE_URL")
        ?? "https://mauri-product-inventory-api.onrender.com/";
    private readonly HttpClient _api = new();

    private IPlaywright _playwright = null!;
    private IBrowser _browser = null!;
    private IBrowserContext _context = null!;
    private IPage _page = null!;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory("artifacts/e2e");
        await WakeApiAsync();

        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });
        _context = await _browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 1440, Height = 1000 }
        });
        await _context.Tracing.StartAsync(new TracingStartOptions
        {
            Screenshots = true,
            Snapshots = true,
            Sources = true
        });
        _page = await _context.NewPageAsync();
    }

    [Fact]
    public async Task Public_demo_supports_complete_crud_and_preserves_dashboard_totals()
    {
        var suffix = $"{DateTime.UtcNow:MMddHHmmss}-{Guid.NewGuid():N}"[..18];
        var originalName = $"E2E product {suffix}";
        var updatedName = $"E2E updated {suffix}";

        try
        {
            await _page.GotoAsync(_webBaseUrl, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.NetworkIdle,
                Timeout = 120_000
            });

            await Expect(_page.GetByRole(AriaRole.Heading, new() { Name = "Product inventory" }))
                .ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 120_000 });

            var totalValue = _page.Locator("article", new() { HasText = "Total products" })
                .Locator(".summary-value");
            var baselineTotal = int.Parse(await totalValue.InnerTextAsync());

            await _page.GetByRole(AriaRole.Button, new() { Name = "Add product", Exact = true }).ClickAsync();
            await _page.GetByLabel("Name", new() { Exact = true }).FillAsync(originalName);
            await _page.GetByLabel("Price (USD)", new() { Exact = true }).FillAsync("19.50");
            await _page.GetByLabel("Quantity", new() { Exact = true }).FillAsync("3");
            await _page.GetByRole(AriaRole.Button, new() { Name = "Add product", Exact = true }).Last.ClickAsync();

            await Expect(_page.GetByText($"{originalName} was created.", new() { Exact = true }))
                .ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 30_000 });
            await Expect(totalValue).ToHaveTextAsync((baselineTotal + 1).ToString());

            var productRow = ProductRow(originalName);
            await productRow.GetByRole(AriaRole.Button, new() { Name = "Edit", Exact = true }).ClickAsync();
            await _page.GetByLabel("Name", new() { Exact = true }).FillAsync(updatedName);
            await _page.GetByLabel("Price (USD)", new() { Exact = true }).FillAsync("25.00");
            await _page.GetByLabel("Quantity", new() { Exact = true }).FillAsync("4");
            await _page.GetByRole(AriaRole.Button, new() { Name = "Save changes", Exact = true }).ClickAsync();

            await Expect(_page.GetByText($"{updatedName} was updated.", new() { Exact = true }))
                .ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 30_000 });

            await _page.GetByLabel("Search products").FillAsync(updatedName.ToLowerInvariant());
            productRow = ProductRow(updatedName);
            await Expect(productRow).ToBeVisibleAsync();
            await Expect(totalValue).ToHaveTextAsync((baselineTotal + 1).ToString());

            await productRow.GetByRole(AriaRole.Button, new() { Name = "Delete", Exact = true }).ClickAsync();
            await Expect(productRow.GetByRole(AriaRole.Alert)).ToContainTextAsync(updatedName);
            await productRow.GetByRole(AriaRole.Button, new() { Name = "Delete", Exact = true }).ClickAsync();

            await Expect(_page.GetByText($"{updatedName} was deleted.", new() { Exact = true }))
                .ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 30_000 });
            await Expect(ProductRow(updatedName)).ToHaveCountAsync(0);
            await Expect(totalValue).ToHaveTextAsync(baselineTotal.ToString());
        }
        catch
        {
            await _page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = "artifacts/e2e/failure.png",
                FullPage = true
            });
            throw;
        }
        finally
        {
            await DeleteByExactNameIfPresentAsync(originalName);
            await DeleteByExactNameIfPresentAsync(updatedName);
        }
    }

    public async Task DisposeAsync()
    {
        if (_context is not null)
        {
            await _context.Tracing.StopAsync(new TracingStopOptions
            {
                Path = "artifacts/e2e/trace.zip"
            });
            await _context.DisposeAsync();
        }

        if (_browser is not null)
        {
            await _browser.DisposeAsync();
        }

        _playwright?.Dispose();
        _api.Dispose();
    }

    private ILocator ProductRow(string name) => _page.GetByRole(AriaRole.Row)
        .Filter(new LocatorFilterOptions
        {
            HasTextRegex = new Regex(Regex.Escape(name), RegexOptions.IgnoreCase)
        });

    private async Task WakeApiAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));

        while (!timeout.IsCancellationRequested)
        {
            try
            {
                using var response = await _api.GetAsync(
                    new Uri(new Uri(_apiBaseUrl), "health/ready"), timeout.Token);
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
                // The free service may still be waking up.
            }

            await Task.Delay(TimeSpan.FromSeconds(5), timeout.Token);
        }

        throw new TimeoutException("The public API did not become ready within two minutes.");
    }

    private async Task DeleteByExactNameIfPresentAsync(string name)
    {
        try
        {
            var uri = new Uri(
                new Uri(_apiBaseUrl),
                $"api/products?search={Uri.EscapeDataString(name)}&pageSize=100");
            var result = await _api.GetFromJsonAsync<PagedProducts>(uri);
            var match = result?.Items.FirstOrDefault(product =>
                string.Equals(product.Name, name, StringComparison.Ordinal));

            if (match is not null)
            {
                await _api.DeleteAsync(new Uri(new Uri(_apiBaseUrl), $"api/products/{match.Id}"));
            }
        }
        catch (HttpRequestException)
        {
            // Preserve the original test failure; cleanup is best effort.
        }
    }

    private static ILocatorAssertions Expect(ILocator locator) => Assertions.Expect(locator);

    private sealed record PagedProducts(
        IReadOnlyList<ProductItem> Items,
        int Page,
        int PageSize,
        int TotalCount);

    private sealed record ProductItem(
        Guid Id,
        string Name,
        decimal Price,
        int Quantity,
        bool IsActive);
}
