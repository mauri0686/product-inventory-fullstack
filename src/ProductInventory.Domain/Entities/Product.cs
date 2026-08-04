using ProductInventory.Domain.Exceptions;

namespace ProductInventory.Domain.Entities;

public sealed class Product
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public string NormalizedName { get; private set; } = null!;
    public decimal Price { get; private set; }
    public int Quantity { get; private set; }
    public bool IsActive { get; private set; }

    private Product() { } // EF Core

    public static Product Create(string name, decimal price, int quantity, bool isActive)
    {
        var product = new Product { Id = Guid.NewGuid() };
        product.SetName(name);
        product.SetPrice(price);
        product.SetQuantity(quantity);
        product.IsActive = isActive;
        return product;
    }

    public static Product Create(Guid id, string name, decimal price, int quantity, bool isActive)
    {
        if (id == Guid.Empty)
            throw new ProductDomainException("Id must be non-empty.");
        var product = new Product { Id = id };
        product.SetName(name);
        product.SetPrice(price);
        product.SetQuantity(quantity);
        product.IsActive = isActive;
        return product;
    }

    public void Update(string name, decimal price, int quantity, bool isActive)
    {
        SetName(name);
        SetPrice(price);
        SetQuantity(quantity);
        IsActive = isActive;
    }

    private void SetName(string name)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            throw new ProductDomainException("Product name cannot be empty.");
        if (trimmed.Length > 100)
            throw new ProductDomainException("Product name cannot exceed 100 characters.");
        Name = trimmed;
        NormalizedName = trimmed.ToUpperInvariant();
    }

    private void SetPrice(decimal price)
    {
        if (price <= 0)
            throw new ProductDomainException("Price must be greater than zero.");
        Price = price;
    }

    private void SetQuantity(int quantity)
    {
        if (quantity < 0)
            throw new ProductDomainException("Quantity cannot be negative.");
        Quantity = quantity;
    }
}
