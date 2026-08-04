using System.ComponentModel.DataAnnotations;
using ProductInventory.Contracts;

namespace ProductInventory.Web.Models;

public sealed class ProductFormModel
{
    public Guid Id { get; set; }

    [Required(ErrorMessage = "Name is required.")]
    [RegularExpression(@".*\S.*", ErrorMessage = "Name is required.")]
    [StringLength(100, ErrorMessage = "Name must be 100 characters or fewer.")]
    public string Name { get; set; } = string.Empty;

    [Range(0.01, double.MaxValue, ErrorMessage = "Price must be greater than zero.")]
    public decimal Price { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Quantity can't be negative.")]
    public int Quantity { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsEditing => Id != Guid.Empty;

    public ProductUpsertRequest ToRequest() => new()
    {
        Name = Name.Trim(),
        Price = Price,
        Quantity = Quantity,
        IsActive = IsActive
    };

    public static ProductFormModel FromProduct(ProductResponse product) => new()
    {
        Id = product.Id,
        Name = product.Name,
        Price = product.Price,
        Quantity = product.Quantity,
        IsActive = product.IsActive
    };
}
