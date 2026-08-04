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
        return Create(Guid.NewGuid(), name, price, quantity, isActive);
    }

    public static Product Create(Guid id, string name, decimal price, int quantity, bool isActive)
    {
        if (id == Guid.Empty)
            throw new ProductDomainException("Id must be non-empty.");
        var (trimmedName, normalizedName) = ValidateName(name);
        ValidatePrice(price);
        ValidateQuantity(quantity);

        return new Product
        {
            Id = id,
            Name = trimmedName,
            NormalizedName = normalizedName,
            Price = price,
            Quantity = quantity,
            IsActive = isActive
        };
    }

    public void Update(string name, decimal price, int quantity, bool isActive)
    {
        var (trimmedName, normalizedName) = ValidateName(name);
        ValidatePrice(price);
        ValidateQuantity(quantity);

        Name = trimmedName;
        NormalizedName = normalizedName;
        Price = price;
        Quantity = quantity;
        IsActive = isActive;
    }

    private static (string Name, string NormalizedName) ValidateName(string name)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            throw new ProductDomainException("Product name cannot be empty.");
        if (trimmed.Length > 100)
            throw new ProductDomainException("Product name cannot exceed 100 characters.");

        return (trimmed, trimmed.ToUpperInvariant());
    }

    private static void ValidatePrice(decimal price)
    {
        if (price <= 0)
            throw new ProductDomainException("Price must be greater than zero.");
    }

    private static void ValidateQuantity(int quantity)
    {
        if (quantity < 0)
            throw new ProductDomainException("Quantity cannot be negative.");
    }
}
