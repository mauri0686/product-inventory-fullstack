namespace ProductInventory.Application.Exceptions;

public sealed class ProductNameConflictException()
    : Exception("A product with the same name already exists.")
{
    public const string ErrorCode = "product.name_conflict";
}
