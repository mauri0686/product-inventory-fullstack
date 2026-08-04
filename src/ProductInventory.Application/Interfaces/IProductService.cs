using ProductInventory.Contracts;

namespace ProductInventory.Application.Interfaces;

public interface IProductService
{
    Task<PagedResponse<ProductResponse>> GetProductsAsync(
        ProductSearchRequest request,
        CancellationToken cancellationToken = default);

    Task<ProductResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<InventorySummaryResponse> GetSummaryAsync(CancellationToken cancellationToken = default);
    Task<ProductResponse> CreateAsync(
        ProductUpsertRequest request,
        CancellationToken cancellationToken = default);

    Task<ProductResponse> UpdateAsync(
        Guid id,
        ProductUpsertRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
