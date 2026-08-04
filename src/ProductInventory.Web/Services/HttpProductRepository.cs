using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ProductInventory.Contracts;

namespace ProductInventory.Web.Services;

public sealed class HttpProductRepository(HttpClient httpClient) : IProductRepository
{
    public async Task<PagedResponse<ProductResponse>> GetProductsAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        await Task.Delay(500, cancellationToken);

        return await httpClient.GetFromJsonAsync<PagedResponse<ProductResponse>>(
                   $"api/products?page={page}&pageSize={pageSize}", cancellationToken)
               ?? throw new InvalidOperationException("The products response was empty.");
    }

    public async Task<InventorySummaryResponse> GetSummaryAsync(
        CancellationToken cancellationToken = default)
    {
        await Task.Delay(500, cancellationToken);

        return await httpClient.GetFromJsonAsync<InventorySummaryResponse>(
                   "api/products/summary", cancellationToken)
               ?? throw new InvalidOperationException("The inventory summary response was empty.");
    }

    public async Task<ProductResponse> GetProductAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        await Task.Delay(500, cancellationToken);

        return await httpClient.GetFromJsonAsync<ProductResponse>(
                   $"api/products/{id}", cancellationToken)
               ?? throw new InvalidOperationException("The product response was empty.");
    }

    public async Task<ProductResponse> CreateProductAsync(
        ProductUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        await Task.Delay(500, cancellationToken);

        using var response = await httpClient.PostAsJsonAsync(
            "api/products", request, cancellationToken);
        await EnsureExpectedSuccessAsync(response, cancellationToken);

        return await response.Content.ReadFromJsonAsync<ProductResponse>(cancellationToken)
               ?? throw new InvalidOperationException("The created product response was empty.");
    }

    public async Task<ProductResponse> UpdateProductAsync(
        Guid id,
        ProductUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        await Task.Delay(500, cancellationToken);

        using var response = await httpClient.PutAsJsonAsync(
            $"api/products/{id}", request, cancellationToken);
        await EnsureExpectedSuccessAsync(response, cancellationToken);

        return await response.Content.ReadFromJsonAsync<ProductResponse>(cancellationToken)
               ?? throw new InvalidOperationException("The updated product response was empty.");
    }

    public async Task DeleteProductAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        await Task.Delay(500, cancellationToken);

        using var response = await httpClient.DeleteAsync($"api/products/{id}", cancellationToken);
        await EnsureExpectedSuccessAsync(response, cancellationToken);
    }

    private static async Task EnsureExpectedSuccessAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var safeMessage = response.StatusCode is HttpStatusCode.BadRequest
            or HttpStatusCode.NotFound
            or HttpStatusCode.Conflict
            or HttpStatusCode.UnprocessableEntity
                ? await ReadProblemMessageAsync(response, cancellationToken)
                : null;

        throw new ProductApiException(
            safeMessage ?? "The API couldn't complete this request. Please try again.");
    }

    private static async Task<string?> ReadProblemMessageAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var content = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var problem = await JsonDocument.ParseAsync(content, cancellationToken: cancellationToken);
            var root = problem.RootElement;

            if (root.TryGetProperty("detail", out var detail) && !string.IsNullOrWhiteSpace(detail.GetString()))
            {
                return detail.GetString();
            }

            if (root.TryGetProperty("title", out var title) && !string.IsNullOrWhiteSpace(title.GetString()))
            {
                return title.GetString();
            }
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            // A non-ProblemDetails response falls back to the safe generic message.
        }

        return null;
    }
}
