using ProductInventory.Application.Exceptions;
using ProductInventory.Application.Interfaces;
using ProductInventory.Contracts;
using ProductInventory.Domain.Entities;

namespace ProductInventory.Application.Services;

public sealed class ProductService(IProductRepository repository) : IProductService
{
    public async Task<PagedResponse<ProductResponse>> GetProductsAsync(
        ProductSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var criteria = new ProductSearchCriteria(
            request.Search,
            request.IsActive,
            request.MinPrice,
            request.MaxPrice,
            request.SortBy,
            request.SortDirection,
            request.Page,
            request.PageSize);

        var result = await repository.SearchAsync(criteria, cancellationToken);
        var items = result.Items.Select(MapToResponse).ToList();

        return new PagedResponse<ProductResponse>(
            items,
            result.Page,
            result.PageSize,
            result.TotalCount);
    }

    public async Task<ProductResponse> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var product = await repository.GetByIdAsync(id, cancellationToken);
        return product is null
            ? throw new ProductNotFoundException(id)
            : MapToResponse(product);
    }

    public async Task<InventorySummaryResponse> GetSummaryAsync(
        CancellationToken cancellationToken = default)
    {
        var summary = await repository.GetSummaryAsync(cancellationToken);
        return new InventorySummaryResponse(
            summary.TotalProducts,
            summary.ActiveProducts,
            summary.InventoryValue);
    }

    public async Task<ProductResponse> CreateAsync(
        ProductUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        var product = Product.Create(
            request.Name,
            request.Price,
            request.Quantity,
            request.IsActive);

        if (await repository.ExistsByNameAsync(
                product.NormalizedName,
                ct: cancellationToken))
        {
            throw new ProductNameConflictException();
        }

        await repository.AddAsync(product, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        return MapToResponse(product);
    }

    public async Task<ProductResponse> UpdateAsync(
        Guid id,
        ProductUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        var existing = await repository.GetByIdAsync(id, cancellationToken)
            ?? throw new ProductNotFoundException(id);

        var normalizedCandidate = request.Name?.Trim().ToUpperInvariant() ?? string.Empty;
        if (await repository.ExistsByNameAsync(
                normalizedCandidate,
                id,
                cancellationToken))
        {
            throw new ProductNameConflictException();
        }

        existing.Update(request.Name ?? string.Empty, request.Price, request.Quantity, request.IsActive);
        repository.Update(existing);
        await repository.SaveChangesAsync(cancellationToken);

        return MapToResponse(existing);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var existing = await repository.GetByIdAsync(id, cancellationToken)
            ?? throw new ProductNotFoundException(id);

        repository.Delete(existing);
        await repository.SaveChangesAsync(cancellationToken);
    }

    private static ProductResponse MapToResponse(Product product) =>
        new(product.Id, product.Name, product.Price, product.Quantity, product.IsActive);
}
