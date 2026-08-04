using ProductInventory.Domain.Entities;
using ProductInventory.Domain.Exceptions;

namespace ProductInventory.UnitTests.Domain;

public sealed class ProductTests
{
    [Fact]
    public void Create_WithValidValues_TrimsAndNormalizesName()
    {
        var product = Product.Create("  Premium Widget  ", 19.95m, 12, true);

        Assert.NotEqual(Guid.Empty, product.Id);
        Assert.Equal("Premium Widget", product.Name);
        Assert.Equal("PREMIUM WIDGET", product.NormalizedName);
        Assert.Equal(19.95m, product.Price);
        Assert.Equal(12, product.Quantity);
        Assert.True(product.IsActive);
    }

    [Fact]
    public void Create_WithKnownId_PreservesId()
    {
        var id = Guid.Parse("5d8465c0-65f5-454f-8018-721383ffb86e");

        var product = Product.Create(id, "Widget", 10m, 3, false);

        Assert.Equal(id, product.Id);
    }

    [Fact]
    public void Create_WithEmptyId_Throws()
    {
        Assert.Throws<ProductDomainException>(
            () => Product.Create(Guid.Empty, "Widget", 10m, 3, true));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankName_Throws(string? name)
    {
        Assert.Throws<ProductDomainException>(
            () => Product.Create(name!, 10m, 3, true));
    }

    [Fact]
    public void Create_WithNameLongerThanOneHundredCharacters_Throws()
    {
        Assert.Throws<ProductDomainException>(
            () => Product.Create(new string('x', 101), 10m, 3, true));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.01)]
    public void Create_WithNonPositivePrice_Throws(decimal price)
    {
        Assert.Throws<ProductDomainException>(
            () => Product.Create("Widget", price, 3, true));
    }

    [Fact]
    public void Create_WithNegativeQuantity_Throws()
    {
        Assert.Throws<ProductDomainException>(
            () => Product.Create("Widget", 10m, -1, true));
    }

    [Fact]
    public void Update_WithValidValues_UpdatesEveryMutableField()
    {
        var product = Product.Create("Widget", 10m, 3, true);

        product.Update("  Updated widget  ", 15.25m, 8, false);

        Assert.Equal("Updated widget", product.Name);
        Assert.Equal("UPDATED WIDGET", product.NormalizedName);
        Assert.Equal(15.25m, product.Price);
        Assert.Equal(8, product.Quantity);
        Assert.False(product.IsActive);
    }

    [Theory]
    [InlineData("Replacement", 0, 8)]
    [InlineData("Replacement", 15, -1)]
    public void Update_WithInvalidValues_PreservesAllOriginalFields(
        string name,
        decimal price,
        int quantity)
    {
        var id = Guid.Parse("885be587-a89d-4122-8ee7-29d6d73e853d");
        var product = Product.Create(id, "Original", 10m, 3, true);

        Assert.Throws<ProductDomainException>(
            () => product.Update(name, price, quantity, false));

        Assert.Equal(id, product.Id);
        Assert.Equal("Original", product.Name);
        Assert.Equal("ORIGINAL", product.NormalizedName);
        Assert.Equal(10m, product.Price);
        Assert.Equal(3, product.Quantity);
        Assert.True(product.IsActive);
    }
}
