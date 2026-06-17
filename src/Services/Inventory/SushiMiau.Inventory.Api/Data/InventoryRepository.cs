using Cassandra;
using SushiMiau.Shared.Contracts;
using CassandraSession = Cassandra.ISession;

namespace SushiMiau.Inventory.Api.Data;

public sealed class InventoryRepository
{
    private const string RestaurantId = "sushi-miau-centro";
    private readonly CassandraSession _session;

    public InventoryRepository(CassandraSession session)
    {
        _session = session;
    }

    public async Task InitializeAsync()
    {
        await _session.ExecuteAsync(new SimpleStatement("""
            CREATE TABLE IF NOT EXISTS inventory_items (
                restaurant_id text,
                item_id uuid,
                name text,
                category text,
                unit text,
                stock decimal,
                minimum_stock decimal,
                supplier text,
                updated_at timestamp,
                PRIMARY KEY (restaurant_id, item_id)
            )
            """));

        await _session.ExecuteAsync(new SimpleStatement("""
            CREATE TABLE IF NOT EXISTS stock_movements_by_item (
                restaurant_id text,
                item_id uuid,
                created_at timestamp,
                movement_id uuid,
                item_name text,
                quantity decimal,
                movement_type text,
                reason text,
                operator_name text,
                PRIMARY KEY ((restaurant_id, item_id), created_at, movement_id)
            ) WITH CLUSTERING ORDER BY (created_at DESC)
            """));

        await _session.ExecuteAsync(new SimpleStatement("""
            CREATE TABLE IF NOT EXISTS inventory_categories (
                restaurant_id text,
                name text,
                PRIMARY KEY (restaurant_id, name)
            )
            """));

        if ((await GetItemsAsync()).Count == 0)
        {
            var seed = new[]
            {
                new UpsertInventoryItemRequest("Salmon fresco", "Pescados", "kg", 18.5m, 8m, "Pacific Fresh"),
                new UpsertInventoryItemRequest("Arroz sushi", "Secos", "kg", 62m, 25m, "Nippon Market"),
                new UpsertInventoryItemRequest("Alga nori", "Secos", "paquete", 14m, 10m, "Nippon Market"),
                new UpsertInventoryItemRequest("Palta Hass", "Verduras", "kg", 9m, 12m, "Verde Andino"),
                new UpsertInventoryItemRequest("Queso crema", "Lacteos", "kg", 11m, 6m, "Lacteos del Valle"),
                new UpsertInventoryItemRequest("Envases delivery", "Empaque", "unidad", 180m, 80m, "Pack Pro")
            };

            foreach (var item in seed)
            {
                await UpsertItemAsync(null, item);
                await CreateCategoryAsync(item.Category);
            }
        }
    }

    public async Task<IReadOnlyList<InventoryItem>> GetItemsAsync()
    {
        var rows = await _session.ExecuteAsync(new SimpleStatement(
            "SELECT item_id, name, category, unit, stock, minimum_stock, supplier, updated_at FROM inventory_items WHERE restaurant_id = ?",
            RestaurantId));

        return rows.Select(MapItem)
            .OrderBy(item => item.Category)
            .ThenBy(item => item.Name)
            .ToList();
    }

    public async Task<InventoryItem?> GetItemAsync(Guid itemId)
    {
        var rows = await _session.ExecuteAsync(new SimpleStatement(
            "SELECT item_id, name, category, unit, stock, minimum_stock, supplier, updated_at FROM inventory_items WHERE restaurant_id = ? AND item_id = ?",
            RestaurantId,
            itemId));

        return rows.Select(MapItem).FirstOrDefault();
    }

    public async Task<IReadOnlyList<InventoryCategory>> GetCategoriesAsync()
    {
        var rows = await _session.ExecuteAsync(new SimpleStatement(
            "SELECT name FROM inventory_categories WHERE restaurant_id = ?",
            RestaurantId));

        return rows.Select(row => new InventoryCategory(row.GetValue<string>("name"))).OrderBy(category => category.Name).ToList();
    }

    public async Task<InventoryCategory> CreateCategoryAsync(string name)
    {
        var category = new InventoryCategory(name.Trim());
        await _session.ExecuteAsync(new SimpleStatement(
            "INSERT INTO inventory_categories (restaurant_id, name) VALUES (?, ?)",
            RestaurantId,
            category.Name));

        return category;
    }

    public async Task<InventoryItem> UpsertItemAsync(Guid? itemId, UpsertInventoryItemRequest request)
    {
        var now = DateTimeOffset.UtcNow;
        var id = itemId ?? Guid.NewGuid();

        await _session.ExecuteAsync(new SimpleStatement("""
            INSERT INTO inventory_items (restaurant_id, item_id, name, category, unit, stock, minimum_stock, supplier, updated_at)
            VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)
            """,
            RestaurantId,
            id,
            request.Name.Trim(),
            request.Category.Trim(),
            request.Unit.Trim(),
            request.Stock,
            request.MinimumStock,
            request.Supplier.Trim(),
            now));

        await CreateCategoryAsync(request.Category);

        return new InventoryItem(
            id,
            request.Name.Trim(),
            request.Category.Trim(),
            request.Unit.Trim(),
            request.Stock,
            request.MinimumStock,
            request.Supplier.Trim(),
            now);
    }

    public async Task<StockMovement?> RecordMovementAsync(Guid itemId, RecordStockMovementRequest request)
    {
        var item = await GetItemAsync(itemId);
        if (item is null)
        {
            return null;
        }

        var normalizedType = string.IsNullOrWhiteSpace(request.MovementType) ? "Ajuste" : request.MovementType.Trim();
        var signedQuantity = normalizedType.Equals("Salida", StringComparison.OrdinalIgnoreCase)
            ? -Math.Abs(request.Quantity)
            : Math.Abs(request.Quantity);

        var newStock = Math.Max(0, item.Stock + signedQuantity);
        var updated = item with { Stock = newStock };

        await UpsertItemAsync(itemId, new UpsertInventoryItemRequest(
            updated.Name,
            updated.Category,
            updated.Unit,
            updated.Stock,
            updated.MinimumStock,
            updated.Supplier));

        var movement = new StockMovement(
            Guid.NewGuid(),
            itemId,
            item.Name,
            signedQuantity,
            normalizedType,
            request.Reason.Trim(),
            request.OperatorName.Trim(),
            DateTimeOffset.UtcNow);

        await _session.ExecuteAsync(new SimpleStatement("""
            INSERT INTO stock_movements_by_item
            (restaurant_id, item_id, created_at, movement_id, item_name, quantity, movement_type, reason, operator_name)
            VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)
            """,
            RestaurantId,
            itemId,
            movement.CreatedAt,
            movement.MovementId,
            movement.ItemName,
            movement.Quantity,
            movement.MovementType,
            movement.Reason,
            movement.OperatorName));

        return movement;
    }

    public async Task DeleteItemAsync(Guid itemId)
    {
        await _session.ExecuteAsync(new SimpleStatement(
            "DELETE FROM inventory_items WHERE restaurant_id = ? AND item_id = ?",
            RestaurantId,
            itemId));
    }

    private static InventoryItem MapItem(Row row)
    {
        var updatedAt = row.GetValue<DateTimeOffset>("updated_at");

        return new InventoryItem(
            row.GetValue<Guid>("item_id"),
            row.GetValue<string>("name"),
            row.GetValue<string>("category"),
            row.GetValue<string>("unit"),
            row.GetValue<decimal>("stock"),
            row.GetValue<decimal>("minimum_stock"),
            row.GetValue<string>("supplier"),
            updatedAt);
    }
}
