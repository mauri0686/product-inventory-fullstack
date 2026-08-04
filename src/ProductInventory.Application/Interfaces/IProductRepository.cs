using ProductInventory.Domain.Entities;

namespace ProductInventory.Application.Interfaces;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<Product>> SearchAsync(ProductSearchCriteria criteria, CancellationToken ct = default);
    Task<ProductSummary> GetSummaryAsync(CancellationToken ct = default);
    Task<bool> ExistsByNameAsync(string normalizedName, Guid? excludeId = null, CancellationToken ct = default);
    Task AddAsync(Product product, CancellationToken ct = default);
    void Update(Product product);
    void Delete(Product product);
    Task SaveChangesAsync(CancellationToken ct = default);
}

public sealed record ProductSearchCriteria(
    string? Search,
    bool? IsActive,
    decimal? MinPrice,
    decimal? MaxPrice,
    string SortBy,
    string SortDirection,
    int Page,
    int PageSize);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public sealed record ProductSummary(int TotalProducts, int ActiveProducts, decimal InventoryValue);
