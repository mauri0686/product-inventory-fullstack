using System.ComponentModel.DataAnnotations;

namespace ProductInventory.Contracts;

public sealed class ProductSearchRequest : IValidatableObject
{
    [StringLength(100)]
    public string? Search { get; set; }

    public bool? IsActive { get; set; }

    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal? MinPrice { get; set; }

    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal? MaxPrice { get; set; }

    public string SortBy { get; set; } = "name";
    public string SortDirection { get; set; } = "asc";

    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 20;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (MinPrice.HasValue && MaxPrice.HasValue && MinPrice > MaxPrice)
            yield return new ValidationResult("MinPrice must be less than or equal to MaxPrice.", [nameof(MinPrice), nameof(MaxPrice)]);

        var validSortBy = new[] { "name", "price", "quantity", "isActive" };
        if (!validSortBy.Contains(SortBy, StringComparer.OrdinalIgnoreCase))
            yield return new ValidationResult($"SortBy must be one of: {string.Join(", ", validSortBy)}.", [nameof(SortBy)]);

        var validDir = new[] { "asc", "desc" };
        if (!validDir.Contains(SortDirection, StringComparer.OrdinalIgnoreCase))
            yield return new ValidationResult("SortDirection must be 'asc' or 'desc'.", [nameof(SortDirection)]);
    }
}
