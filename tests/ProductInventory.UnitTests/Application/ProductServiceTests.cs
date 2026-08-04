using ProductInventory.Application.Exceptions;
using ProductInventory.Application.Interfaces;
using ProductInventory.Application.Services;
using ProductInventory.Contracts;
using ProductInventory.Domain.Entities;
using ProductInventory.Domain.Exceptions;
using ProductInventory.UnitTests.Fakes;

namespace ProductInventory.UnitTests.Application;

public sealed class ProductServiceTests
{
    private static readonly Guid ProductId =
        Guid.Parse("89df4f33-84d7-41cd-84ee-42d218507509");

    [Fact]
    public async Task GetProducts_MapsCriteriaItemsAndPaging()
    {
        var product = Product.Create(ProductId, "Widget", 12.50m, 7, true);
        var repository = new FakeProductRepository
        {
            SearchResult = new PagedResult<Product>([product], 2, 5, 11)
        };
        var service = new ProductService(repository);
        var request = new ProductSearchRequest
        {
            Search = "wid",
            IsActive = true,
            MinPrice = 10m,
            MaxPrice = 20m,
            SortBy = "price",
            SortDirection = "desc",
            Page = 2,
            PageSize = 5
        };

        var result = await service.GetProductsAsync(request);

        var item = Assert.Single(result.Items);
        Assert.Equal(ProductId, item.Id);
        Assert.Equal("Widget", item.Name);
        Assert.Equal(12.50m, item.Price);
        Assert.Equal(7, item.Quantity);
        Assert.True(item.IsActive);
        Assert.Equal(2, result.Page);
        Assert.Equal(5, result.PageSize);
        Assert.Equal(11, result.TotalCount);

        var criteria = Assert.IsType<ProductSearchCriteria>(repository.LastSearchCriteria);
        Assert.Equal(request.Search, criteria.Search);
        Assert.Equal(request.IsActive, criteria.IsActive);
        Assert.Equal(request.MinPrice, criteria.MinPrice);
        Assert.Equal(request.MaxPrice, criteria.MaxPrice);
        Assert.Equal(request.SortBy, criteria.SortBy);
        Assert.Equal(request.SortDirection, criteria.SortDirection);
        Assert.Equal(request.Page, criteria.Page);
        Assert.Equal(request.PageSize, criteria.PageSize);
    }

    [Fact]
    public async Task GetProducts_ForwardsCancellationToken()
    {
        using var cancellation = new CancellationTokenSource();
        var repository = new FakeProductRepository();
        var service = new ProductService(repository);

        await service.GetProductsAsync(new ProductSearchRequest(), cancellation.Token);

        Assert.Equal(cancellation.Token, repository.LastCancellationToken);
    }

    [Fact]
    public async Task GetById_MapsProduct()
    {
        var repository = new FakeProductRepository
        {
            ProductToReturn = Product.Create(ProductId, "Widget", 12.50m, 7, false)
        };

        var result = await new ProductService(repository).GetByIdAsync(ProductId);

        Assert.Equal(ProductId, result.Id);
        Assert.Equal("Widget", result.Name);
        Assert.Equal(12.50m, result.Price);
        Assert.Equal(7, result.Quantity);
        Assert.False(result.IsActive);
    }

    [Fact]
    public async Task GetById_WhenMissing_ThrowsNotFound()
    {
        var service = new ProductService(new FakeProductRepository());

        await Assert.ThrowsAsync<ProductNotFoundException>(
            () => service.GetByIdAsync(ProductId));
    }

    [Fact]
    public async Task GetSummary_MapsRepositorySummary()
    {
        var repository = new FakeProductRepository
        {
            Summary = new ProductSummary(10, 6, 1234.56m)
        };

        var result = await new ProductService(repository).GetSummaryAsync();

        Assert.Equal(10, result.TotalProducts);
        Assert.Equal(6, result.ActiveProducts);
        Assert.Equal(1234.56m, result.InventoryValue);
    }

    [Fact]
    public async Task Create_TrimsNormalizesAddsSavesAndMapsProduct()
    {
        using var cancellation = new CancellationTokenSource();
        var repository = new FakeProductRepository();
        var service = new ProductService(repository);
        var request = Request("  Premium Widget  ", 15.75m, 4, true);

        var result = await service.CreateAsync(request, cancellation.Token);

        var added = Assert.IsType<Product>(repository.AddedProduct);
        Assert.NotEqual(Guid.Empty, added.Id);
        Assert.Equal("Premium Widget", added.Name);
        Assert.Equal("PREMIUM WIDGET", added.NormalizedName);
        Assert.Equal("PREMIUM WIDGET", repository.LastNormalizedName);
        Assert.Null(repository.LastExcludedId);
        Assert.Equal(1, repository.SaveChangesCalls);
        Assert.Equal(cancellation.Token, repository.LastCancellationToken);
        Assert.Equal(added.Id, result.Id);
        Assert.Equal(added.Name, result.Name);
        Assert.Equal(added.Price, result.Price);
        Assert.Equal(added.Quantity, result.Quantity);
        Assert.Equal(added.IsActive, result.IsActive);
    }

    [Fact]
    public async Task Create_WhenNameExistsCaseInsensitively_ThrowsWithoutWriting()
    {
        var repository = new FakeProductRepository { NameExists = true };
        var service = new ProductService(repository);

        await Assert.ThrowsAsync<ProductNameConflictException>(
            () => service.CreateAsync(Request("  wIdGeT  ")));

        Assert.Equal("WIDGET", repository.LastNormalizedName);
        Assert.Null(repository.AddedProduct);
        Assert.Equal(0, repository.SaveChangesCalls);
    }

    [Fact]
    public async Task Create_WhenDomainValuesAreInvalid_DoesNotConsultOrWriteRepository()
    {
        var repository = new FakeProductRepository();
        var service = new ProductService(repository);

        await Assert.ThrowsAsync<ProductDomainException>(
            () => service.CreateAsync(Request("Widget", price: 0m)));

        Assert.Null(repository.LastNormalizedName);
        Assert.Null(repository.AddedProduct);
        Assert.Equal(0, repository.SaveChangesCalls);
    }

    [Fact]
    public async Task Update_UpdatesSavesAndMapsProduct()
    {
        var existing = Product.Create(ProductId, "Widget", 10m, 2, true);
        var repository = new FakeProductRepository { ProductToReturn = existing };
        var service = new ProductService(repository);

        var result = await service.UpdateAsync(
            ProductId,
            Request("  Updated Widget  ", 20m, 9, false));

        Assert.Equal("UPDATED WIDGET", repository.LastNormalizedName);
        Assert.Equal(ProductId, repository.LastExcludedId);
        Assert.Same(existing, repository.UpdatedProduct);
        Assert.Equal(1, repository.SaveChangesCalls);
        Assert.Equal(ProductId, result.Id);
        Assert.Equal("Updated Widget", result.Name);
        Assert.Equal(20m, result.Price);
        Assert.Equal(9, result.Quantity);
        Assert.False(result.IsActive);
    }

    [Fact]
    public async Task Update_WhenKeepingOwnName_AllowsUpdateAndExcludesOwnId()
    {
        var existing = Product.Create(ProductId, "Widget", 10m, 2, true);
        var repository = new FakeProductRepository { ProductToReturn = existing };
        var service = new ProductService(repository);

        await service.UpdateAsync(ProductId, Request("widget", 11m, 3, true));

        Assert.Equal("WIDGET", repository.LastNormalizedName);
        Assert.Equal(ProductId, repository.LastExcludedId);
        Assert.Same(existing, repository.UpdatedProduct);
        Assert.Equal(1, repository.SaveChangesCalls);
    }

    [Fact]
    public async Task Update_WhenMissing_ThrowsWithoutCheckingDuplicateOrWriting()
    {
        var repository = new FakeProductRepository();
        var service = new ProductService(repository);

        await Assert.ThrowsAsync<ProductNotFoundException>(
            () => service.UpdateAsync(ProductId, Request("Widget")));

        Assert.Null(repository.LastNormalizedName);
        Assert.Null(repository.UpdatedProduct);
        Assert.Equal(0, repository.SaveChangesCalls);
    }

    [Fact]
    public async Task Update_WhenNameExistsCaseInsensitively_ThrowsWithoutMutatingOrWriting()
    {
        var existing = Product.Create(ProductId, "Original", 10m, 2, true);
        var repository = new FakeProductRepository
        {
            ProductToReturn = existing,
            NameExists = true
        };
        var service = new ProductService(repository);

        await Assert.ThrowsAsync<ProductNameConflictException>(
            () => service.UpdateAsync(ProductId, Request("  dUpLiCaTe  ", 99m, 99, false)));

        Assert.Equal("DUPLICATE", repository.LastNormalizedName);
        Assert.Equal(ProductId, repository.LastExcludedId);
        Assert.Equal("Original", existing.Name);
        Assert.Equal(10m, existing.Price);
        Assert.Equal(2, existing.Quantity);
        Assert.True(existing.IsActive);
        Assert.Null(repository.UpdatedProduct);
        Assert.Equal(0, repository.SaveChangesCalls);
    }

    [Fact]
    public async Task Delete_WhenFound_DeletesAndSaves()
    {
        var existing = Product.Create(ProductId, "Widget", 10m, 2, true);
        var repository = new FakeProductRepository { ProductToReturn = existing };

        await new ProductService(repository).DeleteAsync(ProductId);

        Assert.Same(existing, repository.DeletedProduct);
        Assert.Equal(1, repository.SaveChangesCalls);
    }

    [Fact]
    public async Task Delete_WhenMissing_ThrowsWithoutWriting()
    {
        var repository = new FakeProductRepository();
        var service = new ProductService(repository);

        await Assert.ThrowsAsync<ProductNotFoundException>(
            () => service.DeleteAsync(ProductId));

        Assert.Null(repository.DeletedProduct);
        Assert.Equal(0, repository.SaveChangesCalls);
    }

    private static ProductUpsertRequest Request(
        string name,
        decimal price = 10m,
        int quantity = 2,
        bool isActive = true) =>
        new()
        {
            Name = name,
            Price = price,
            Quantity = quantity,
            IsActive = isActive
        };
}
