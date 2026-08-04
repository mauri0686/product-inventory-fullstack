namespace ProductInventory.Application.Exceptions;

public sealed class ProductNotFoundException(Guid id)
    : Exception($"Product with Id '{id}' was not found.")
{
    public const string ErrorCode = "product.not_found";
}
