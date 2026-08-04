namespace ProductInventory.Contracts;

public sealed record InventorySummaryResponse(int TotalProducts, int ActiveProducts, decimal InventoryValue);
