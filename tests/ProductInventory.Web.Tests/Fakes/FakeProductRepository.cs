using ProductInventory.Contracts;
using ProductInventory.Web.Services;

namespace ProductInventory.Web.Tests.Fakes;

internal sealed class FakeProductRepository(IEnumerable<ProductResponse>? products = null) : IProductRepository
{
    private readonly TaskCompletionSource<PagedResponse<ProductResponse>> _initialLoad =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public List<ProductResponse> Products { get; } = products?.ToList() ?? [];

    public bool HoldInitialLoad { get; set; }

    public int FailingListCallsRemaining { get; set; }

    public int ListCalls { get; private set; }

    public int CreateCalls { get; private set; }

    public int UpdateCalls { get; private set; }

    public int DeleteCalls { get; private set; }

    public async Task<PagedResponse<ProductResponse>> GetProductsAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ListCalls++;

        if (FailingListCallsRemaining > 0)
        {
            FailingListCallsRemaining--;
            throw new HttpRequestException("The fake API is unavailable.");
        }

        if (HoldInitialLoad && ListCalls == 1)
        {
            return await _initialLoad.Task.WaitAsync(cancellationToken);
        }

        return CreatePage(page, pageSize);
    }

    public Task<InventorySummaryResponse> GetSummaryAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new InventorySummaryResponse(
            Products.Count,
            Products.Count(product => product.IsActive),
            Products.Sum(product => product.Price * product.Quantity)));

    public Task<ProductResponse> GetProductAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Products.Single(product => product.Id == id));

    public Task<ProductResponse> CreateProductAsync(
        ProductUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        CreateCalls++;
        var product = new ProductResponse(
            Guid.NewGuid(), request.Name, request.Price, request.Quantity, request.IsActive);
        Products.Add(product);
        return Task.FromResult(product);
    }

    public Task<ProductResponse> UpdateProductAsync(
        Guid id,
        ProductUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        UpdateCalls++;
        var index = Products.FindIndex(product => product.Id == id);
        var product = new ProductResponse(
            id, request.Name, request.Price, request.Quantity, request.IsActive);
        Products[index] = product;
        return Task.FromResult(product);
    }

    public Task DeleteProductAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        DeleteCalls++;
        Products.RemoveAll(product => product.Id == id);
        return Task.CompletedTask;
    }

    public void CompleteInitialLoad(int page = 1, int pageSize = 100) =>
        _initialLoad.TrySetResult(CreatePage(page, pageSize));

    private PagedResponse<ProductResponse> CreatePage(int page, int pageSize)
    {
        var items = Products
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToArray();

        return new PagedResponse<ProductResponse>(items, page, pageSize, Products.Count);
    }
}

