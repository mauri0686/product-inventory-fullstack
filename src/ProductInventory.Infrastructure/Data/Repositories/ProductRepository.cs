using Microsoft.EntityFrameworkCore;
using ProductInventory.Application.Exceptions;
using ProductInventory.Application.Interfaces;
using ProductInventory.Domain.Entities;

namespace ProductInventory.Infrastructure.Data.Repositories;

public class ProductRepository : IProductRepository
{
    private readonly ProductDbContext _context;

    public ProductRepository(ProductDbContext context)
    {
        _context = context;
    }

    public async Task<Product?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    public async Task<PagedResult<Product>> SearchAsync(ProductSearchCriteria criteria, CancellationToken ct = default)
    {
        var query = _context.Products.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(criteria.Search))
        {
            var pattern = criteria.Search.Trim();
            query = query.Where(p => EF.Functions.ILike(
                p.NormalizedName,
                $"%{EscapeLikePattern(pattern)}%",
                "\\"));
        }

        if (criteria.IsActive.HasValue)
            query = query.Where(p => p.IsActive == criteria.IsActive.Value);

        if (criteria.MinPrice.HasValue)
            query = query.Where(p => p.Price >= criteria.MinPrice.Value);

        if (criteria.MaxPrice.HasValue)
            query = query.Where(p => p.Price <= criteria.MaxPrice.Value);

        var totalCount = await query.CountAsync(ct);

        query = criteria.SortBy.ToLowerInvariant() switch
        {
            "price" => criteria.SortDirection == "desc"
                ? query.OrderByDescending(p => p.Price).ThenBy(p => p.Id)
                : query.OrderBy(p => p.Price).ThenBy(p => p.Id),
            "quantity" => criteria.SortDirection == "desc"
                ? query.OrderByDescending(p => p.Quantity).ThenBy(p => p.Id)
                : query.OrderBy(p => p.Quantity).ThenBy(p => p.Id),
            "isactive" => criteria.SortDirection == "desc"
                ? query.OrderByDescending(p => p.IsActive).ThenBy(p => p.Id)
                : query.OrderBy(p => p.IsActive).ThenBy(p => p.Id),
            _ => criteria.SortDirection == "desc"
                ? query.OrderByDescending(p => p.Name).ThenBy(p => p.Id)
                : query.OrderBy(p => p.Name).ThenBy(p => p.Id),
        };

        var items = await query
            .Skip((criteria.Page - 1) * criteria.PageSize)
            .Take(criteria.PageSize)
            .ToListAsync(ct);

        return new PagedResult<Product>(items, criteria.Page, criteria.PageSize, totalCount);
    }

    public async Task<ProductSummary> GetSummaryAsync(CancellationToken ct = default)
    {
        var query = _context.Products.AsNoTracking();
        var totalProducts = await query.CountAsync(ct);
        var activeProducts = totalProducts > 0 ? await query.CountAsync(p => p.IsActive, ct) : 0;
        var inventoryValue = totalProducts > 0
            ? await query.SumAsync(p => p.Price * p.Quantity, ct)
            : 0;
        return new ProductSummary(totalProducts, activeProducts, inventoryValue);
    }

    public async Task<bool> ExistsByNameAsync(string normalizedName, Guid? excludeId = null, CancellationToken ct = default)
    {
        var query = _context.Products.Where(p => p.NormalizedName == normalizedName);
        if (excludeId.HasValue)
            query = query.Where(p => p.Id != excludeId.Value);
        return await query.AnyAsync(ct);
    }

    public async Task AddAsync(Product product, CancellationToken ct = default)
    {
        await _context.Products.AddAsync(product, ct);
    }

    public void Update(Product product)
    {
        _context.Products.Update(product);
    }

    public void Delete(Product product)
    {
        _context.Products.Remove(product);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException pgEx && pgEx.SqlState == "23505")
        {
            throw new ProductNameConflictException();
        }
    }

    private static string EscapeLikePattern(string pattern)
    {
        return pattern
            .Replace("\\", "\\\\")
            .Replace("%", "\\%")
            .Replace("_", "\\_");
    }
}
