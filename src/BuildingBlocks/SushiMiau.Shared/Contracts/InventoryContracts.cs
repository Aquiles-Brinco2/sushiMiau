namespace SushiMiau.Shared.Contracts;

public sealed record InventoryItem(
    Guid ItemId,
    string Name,
    string Category,
    string Unit,
    decimal Stock,
    decimal MinimumStock,
    string Supplier,
    DateTimeOffset UpdatedAt)
{
    public bool IsLowStock => Stock <= MinimumStock;
}

public sealed record UpsertInventoryItemRequest(
    string Name,
    string Category,
    string Unit,
    decimal Stock,
    decimal MinimumStock,
    string Supplier);

public sealed record InventoryCategory(string Name);

public sealed record StockMovement(
    Guid MovementId,
    Guid ItemId,
    string ItemName,
    decimal Quantity,
    string MovementType,
    string Reason,
    string OperatorName,
    DateTimeOffset CreatedAt);

public sealed record RecordStockMovementRequest(
    decimal Quantity,
    string MovementType,
    string Reason,
    string OperatorName);

public sealed record InventorySnapshot(
    int TotalItems,
    int LowStockItems,
    decimal TotalStockUnits,
    IReadOnlyList<InventoryItem> Alerts);
