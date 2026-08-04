using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ProductInventory.Contracts;

namespace ProductInventory.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class ProductApiTests(ApiFixture fixture) : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private HttpClient Client => fixture.Client;

    public Task InitializeAsync() => fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CleanDatabase_MigratesAndSeedsExactlyOneHundredProducts()
    {
        var page = await Client.GetFromJsonAsync<PagedResponse<ProductResponse>>(
            "/api/products?pageSize=100",
            JsonOptions);

        Assert.NotNull(page);
        Assert.Equal(100, page.TotalCount);
        Assert.Equal(100, page.Items.Count);
        Assert.Equal("Product 001", page.Items[0].Name);
        Assert.Equal("Product 100", page.Items[^1].Name);
    }

    [Fact]
    public async Task CrudFlow_ReturnsCanonicalStatusesBodiesAndLocation()
    {
        var createResponse = await Client.PostAsJsonAsync(
            "/api/products",
            Request("QA Flow Product", 45.50m, 3));

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<ProductResponse>(JsonOptions);
        Assert.NotNull(created);
        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.EndsWith(
            $"/api/products/{created.Id}",
            createResponse.Headers.Location?.OriginalString,
            StringComparison.OrdinalIgnoreCase);

        var fetched = await Client.GetFromJsonAsync<ProductResponse>(
            $"/api/products/{created.Id}",
            JsonOptions);
        Assert.Equal(created, fetched);

        var updateResponse = await Client.PutAsJsonAsync(
            $"/api/products/{created.Id}",
            Request("QA Flow Product Updated", 51m, 7, false));
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.Content.ReadFromJsonAsync<ProductResponse>(JsonOptions);
        Assert.NotNull(updated);
        Assert.Equal(created.Id, updated.Id);
        Assert.Equal("QA Flow Product Updated", updated.Name);
        Assert.False(updated.IsActive);

        var deleteResponse = await Client.DeleteAsync($"/api/products/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var missingResponse = await Client.GetAsync($"/api/products/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, missingResponse.StatusCode);
        await AssertProblemAsync(missingResponse, "product.not_found");
    }

    [Fact]
    public async Task DuplicateName_IgnoringCaseAndWhitespace_ReturnsConflict()
    {
        var first = await Client.PostAsJsonAsync(
            "/api/products",
            Request("Unique Case Product", 10m, 1));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var duplicate = await Client.PostAsJsonAsync(
            "/api/products",
            Request("  uNiQuE cAsE pRoDuCt  ", 20m, 2));

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        await AssertProblemAsync(duplicate, "product.name_conflict");
    }

    [Fact]
    public async Task InvalidBodyAndQuery_ReturnValidationProblemDetails()
    {
        var invalidBody = await Client.PostAsJsonAsync(
            "/api/products",
            Request("   ", 0m, -1));

        Assert.Equal(HttpStatusCode.BadRequest, invalidBody.StatusCode);
        await AssertProblemAsync(invalidBody, "request.invalid", expectErrors: true);

        var invalidQuery = await Client.GetAsync(
            "/api/products?minPrice=20&maxPrice=10&page=0&pageSize=101&sortBy=unknown");

        Assert.Equal(HttpStatusCode.BadRequest, invalidQuery.StatusCode);
        await AssertProblemAsync(invalidQuery, "request.invalid", expectErrors: true);
    }

    [Fact]
    public async Task SearchFiltersUppercaseDescendingSortAndPagination_RunInPostgreSql()
    {
        var search = await GetPageAsync("?search=pRoDuCt%20010&pageSize=100");
        Assert.Equal(1, search.TotalCount);
        Assert.Equal("Product 010", Assert.Single(search.Items).Name);

        var inactive = await GetPageAsync("?isActive=false&pageSize=100");
        Assert.Equal(20, inactive.TotalCount);
        Assert.All(inactive.Items, product => Assert.False(product.IsActive));

        var prices = await GetPageAsync("?minPrice=80&maxPrice=100&pageSize=100");
        Assert.NotEmpty(prices.Items);
        Assert.All(prices.Items, product => Assert.InRange(product.Price, 80m, 100m));

        var descending = await GetPageAsync(
            "?sortBy=price&sortDirection=DESC&pageSize=2");
        Assert.Equal("Product 100", descending.Items[0].Name);
        Assert.Equal("Product 099", descending.Items[1].Name);

        var secondPage = await GetPageAsync("?page=2&pageSize=7");
        Assert.Equal(100, secondPage.TotalCount);
        Assert.Equal(7, secondPage.Items.Count);
        Assert.Equal("Product 008", secondPage.Items[0].Name);

        var escapedWildcard = await GetPageAsync("?search=%25&pageSize=100");
        Assert.Equal(0, escapedWildcard.TotalCount);
    }

    [Fact]
    public async Task SummaryAndHealthChecks_ReflectTheWholeSeededInventory()
    {
        var summary = await Client.GetFromJsonAsync<InventorySummaryResponse>(
            "/api/products/summary",
            JsonOptions);

        var expectedValue = Enumerable.Range(1, 100).Sum(index =>
            Math.Round(10m + (index * 7.89m), 2) * ((index * 7) % 100));

        Assert.NotNull(summary);
        Assert.Equal(100, summary.TotalProducts);
        Assert.Equal(80, summary.ActiveProducts);
        Assert.Equal(expectedValue, summary.InventoryValue);

        var live = await Client.GetStringAsync("/health/live");
        var ready = await Client.GetStringAsync("/health/ready");
        Assert.Equal("Healthy", live);
        Assert.Equal("Healthy", ready);
    }

    private async Task<PagedResponse<ProductResponse>> GetPageAsync(string query) =>
        await Client.GetFromJsonAsync<PagedResponse<ProductResponse>>(
            $"/api/products{query}",
            JsonOptions)
        ?? throw new InvalidOperationException("The API returned no page body.");

    private static async Task AssertProblemAsync(
        HttpResponseMessage response,
        string expectedErrorCode,
        bool expectErrors = false)
    {
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;

        Assert.Equal(expectedErrorCode, root.GetProperty("errorCode").GetString());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("traceId").GetString()));
        if (expectErrors)
            Assert.Equal(JsonValueKind.Object, root.GetProperty("errors").ValueKind);
    }

    private static ProductUpsertRequest Request(
        string name,
        decimal price,
        int quantity,
        bool isActive = true) =>
        new()
        {
            Name = name,
            Price = price,
            Quantity = quantity,
            IsActive = isActive
        };
}
