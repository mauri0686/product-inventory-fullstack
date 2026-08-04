using ProductInventory.Application.Interfaces;
using ProductInventory.Domain.Entities;

namespace ProductInventory.UnitTests.Fakes;

internal sealed class FakeProductRepository : IProductRepository
{
    public Product? ProductToReturn { get; set; }
    public PagedResult<Product> SearchResult { get; set; } =
        new([], 1, 20, 0);
    public ProductSummary Summary { get; set; } = new(0, 0, 0m);
    public bool NameExists { get; set; }

    public ProductSearchCriteria? LastSearchCriteria { get; private set; }
    public string? LastNormalizedName { get; private set; }
    public Guid? LastExcludedId { get; private set; }
    public Product? AddedProduct { get; private set; }
    public Product? UpdatedProduct { get; private set; }
    public Product? DeletedProduct { get; private set; }
    public int SaveChangesCalls { get; private set; }
    public CancellationToken LastCancellationToken { get; private set; }

    public Task<Product?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        LastCancellationToken = ct;
        return Task.FromResult(ProductToReturn?.Id == id ? ProductToReturn : null);
    }

    public Task<PagedResult<Product>> SearchAsync(
        ProductSearchCriteria criteria,
        CancellationToken ct = default)
    {
        LastSearchCriteria = criteria;
        LastCancellationToken = ct;
        return Task.FromResult(SearchResult);
    }

    public Task<ProductSummary> GetSummaryAsync(CancellationToken ct = default)
    {
        LastCancellationToken = ct;
        return Task.FromResult(Summary);
    }

    public Task<bool> ExistsByNameAsync(
        string normalizedName,
        Guid? excludeId = null,
        CancellationToken ct = default)
    {
        LastNormalizedName = normalizedName;
        LastExcludedId = excludeId;
        LastCancellationToken = ct;
        return Task.FromResult(NameExists);
    }

    public Task AddAsync(Product product, CancellationToken ct = default)
    {
        AddedProduct = product;
        LastCancellationToken = ct;
        return Task.CompletedTask;
    }

    public void Update(Product product)
    {
        UpdatedProduct = product;
    }

    public void Delete(Product product)
    {
        DeletedProduct = product;
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
    {
        SaveChangesCalls++;
        LastCancellationToken = ct;
        return Task.CompletedTask;
    }
}
