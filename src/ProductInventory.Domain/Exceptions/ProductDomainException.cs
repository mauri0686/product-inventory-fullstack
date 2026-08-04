namespace ProductInventory.Domain.Exceptions;

public sealed class ProductDomainException : Exception
{
    public ProductDomainException(string message)
        : base(message)
    {
    }
}
