using ProductInventory.Contracts;

namespace ProductInventory.Web.Services;

public interface IProductRepository
{
    Task<PagedResponse<ProductResponse>> GetProductsAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<InventorySummaryResponse> GetSummaryAsync(
        CancellationToken cancellationToken = default);

    Task<ProductResponse> GetProductAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<ProductResponse> CreateProductAsync(
        ProductUpsertRequest request,
        CancellationToken cancellationToken = default);

    Task<ProductResponse> UpdateProductAsync(
        Guid id,
        ProductUpsertRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteProductAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}

