namespace ProductInventory.Web.Services;

public sealed class ProductApiException(string message) : Exception(message);
