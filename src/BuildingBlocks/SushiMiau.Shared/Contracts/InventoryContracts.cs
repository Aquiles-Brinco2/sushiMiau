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

public sealed record Supplier(
    Guid SupplierId,
    string Name,
    string ContactName,
    string Phone,
    string Email,
    string Address,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record UpsertSupplierRequest(
    string Name,
    string ContactName,
    string Phone,
    string Email,
    string Address,
    bool IsActive);

public sealed record PurchaseOrderLine(
    Guid InventoryItemId,
    string IngredientName,
    decimal Quantity,
    string Unit,
    decimal UnitPrice)
{
    public decimal Subtotal => Quantity * UnitPrice;
}

public sealed record PurchaseOrder(
    Guid PurchaseOrderId,
    string OrderNumber,
    Guid SupplierId,
    string SupplierName,
    string Status,
    IReadOnlyList<PurchaseOrderLine> Lines,
    decimal Total,
    string Notes,
    DateTimeOffset OrderedAt,
    DateTimeOffset? ReceivedAt,
    DateTimeOffset UpdatedAt);

public sealed record CreatePurchaseOrderRequest(
    Guid SupplierId,
    string SupplierName,
    IReadOnlyList<PurchaseOrderLine> Lines,
    string Notes);

public sealed record UpdatePurchaseOrderStatusRequest(string Status, string OperatorName);
