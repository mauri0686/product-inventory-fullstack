namespace ProductInventory.Contracts;

public sealed record ProductResponse(Guid Id, string Name, decimal Price, int Quantity, bool IsActive);
