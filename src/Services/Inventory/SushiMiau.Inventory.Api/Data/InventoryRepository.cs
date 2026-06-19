using Cassandra;
using System.Text.Json;
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

        await _session.ExecuteAsync(new SimpleStatement("""
            CREATE TABLE IF NOT EXISTS suppliers (
                restaurant_id text,
                supplier_id uuid,
                name text,
                contact_name text,
                phone text,
                email text,
                address text,
                is_active boolean,
                created_at timestamp,
                updated_at timestamp,
                PRIMARY KEY (restaurant_id, supplier_id)
            )
            """));

        await _session.ExecuteAsync(new SimpleStatement("""
            CREATE TABLE IF NOT EXISTS purchase_orders (
                restaurant_id text,
                purchase_order_id uuid,
                order_number text,
                supplier_id uuid,
                supplier_name text,
                status text,
                lines_json text,
                total decimal,
                notes text,
                ordered_at timestamp,
                received_at timestamp,
                updated_at timestamp,
                PRIMARY KEY (restaurant_id, purchase_order_id)
            )
            """));

        if ((await GetItemsAsync()).Count == 0)
        {
            var seed = new[]
            {
                (Guid.Parse("10000000-0000-0000-0000-000000000001"), new UpsertInventoryItemRequest("Salmon fresco", "Pescados", "kg", 18.5m, 8m, "Pacific Fresh")),
                (Guid.Parse("10000000-0000-0000-0000-000000000002"), new UpsertInventoryItemRequest("Arroz sushi", "Secos", "kg", 62m, 25m, "Nippon Market")),
                (Guid.Parse("10000000-0000-0000-0000-000000000003"), new UpsertInventoryItemRequest("Alga nori", "Secos", "paquete", 14m, 10m, "Nippon Market")),
                (Guid.Parse("10000000-0000-0000-0000-000000000004"), new UpsertInventoryItemRequest("Palta Hass", "Verduras", "kg", 9m, 12m, "Verde Andino")),
                (Guid.Parse("10000000-0000-0000-0000-000000000005"), new UpsertInventoryItemRequest("Queso crema", "Lacteos", "kg", 11m, 6m, "Lacteos del Valle")),
                (Guid.Parse("10000000-0000-0000-0000-000000000006"), new UpsertInventoryItemRequest("Envases delivery", "Empaque", "unidad", 180m, 80m, "Pack Pro"))
            };

            foreach (var (itemId, item) in seed)
            {
                await UpsertItemAsync(itemId, item);
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

    public async Task<IReadOnlyList<Supplier>> GetSuppliersAsync()
    {
        var rows = await _session.ExecuteAsync(new SimpleStatement("""
            SELECT supplier_id, name, contact_name, phone, email, address, is_active, created_at, updated_at
            FROM suppliers WHERE restaurant_id = ?
            """, RestaurantId));
        return rows.Select(MapSupplier).OrderBy(item => item.Name).ToList();
    }

    public async Task<Supplier> UpsertSupplierAsync(Guid? supplierId, UpsertSupplierRequest request)
    {
        var existing = supplierId.HasValue
            ? (await GetSuppliersAsync()).FirstOrDefault(item => item.SupplierId == supplierId.Value)
            : null;
        var now = DateTimeOffset.UtcNow;
        var supplier = new Supplier(
            supplierId ?? Guid.NewGuid(),
            request.Name.Trim(),
            request.ContactName.Trim(),
            request.Phone.Trim(),
            request.Email.Trim(),
            request.Address.Trim(),
            request.IsActive,
            existing?.CreatedAt ?? now,
            now);

        await _session.ExecuteAsync(new SimpleStatement("""
            INSERT INTO suppliers
            (restaurant_id, supplier_id, name, contact_name, phone, email, address, is_active, created_at, updated_at)
            VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
            """,
            RestaurantId,
            supplier.SupplierId,
            supplier.Name,
            supplier.ContactName,
            supplier.Phone,
            supplier.Email,
            supplier.Address,
            supplier.IsActive,
            supplier.CreatedAt,
            supplier.UpdatedAt));
        return supplier;
    }

    public async Task DeleteSupplierAsync(Guid supplierId)
    {
        await _session.ExecuteAsync(new SimpleStatement(
            "DELETE FROM suppliers WHERE restaurant_id = ? AND supplier_id = ?",
            RestaurantId,
            supplierId));
    }

    public async Task<IReadOnlyList<PurchaseOrder>> GetPurchaseOrdersAsync()
    {
        var rows = await _session.ExecuteAsync(new SimpleStatement("""
            SELECT purchase_order_id, order_number, supplier_id, supplier_name, status, lines_json,
                   total, notes, ordered_at, received_at, updated_at
            FROM purchase_orders WHERE restaurant_id = ?
            """, RestaurantId));
        return rows.Select(MapPurchaseOrder).OrderByDescending(item => item.OrderedAt).ToList();
    }

    public async Task<PurchaseOrder> CreatePurchaseOrderAsync(CreatePurchaseOrderRequest request)
    {
        var now = DateTimeOffset.UtcNow;
        var lines = request.Lines
            .Where(line => line.InventoryItemId != Guid.Empty && line.Quantity > 0 && line.UnitPrice >= 0)
            .ToList();
        var order = new PurchaseOrder(
            Guid.NewGuid(),
            $"OC-{now:yyyyMMddHHmmss}",
            request.SupplierId,
            request.SupplierName.Trim(),
            "Solicitada",
            lines,
            lines.Sum(line => line.Subtotal),
            request.Notes.Trim(),
            now,
            null,
            now);
        await SavePurchaseOrderAsync(order);
        return order;
    }

    public async Task<PurchaseOrder?> UpdatePurchaseOrderStatusAsync(Guid purchaseOrderId, UpdatePurchaseOrderStatusRequest request)
    {
        var order = (await GetPurchaseOrdersAsync()).FirstOrDefault(item => item.PurchaseOrderId == purchaseOrderId);
        if (order is null)
        {
            return null;
        }

        var status = string.IsNullOrWhiteSpace(request.Status) ? order.Status : request.Status.Trim();
        if (status.Equals("Recibida", StringComparison.OrdinalIgnoreCase)
            && !order.Status.Equals("Recibida", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var line in order.Lines)
            {
                await RecordMovementAsync(line.InventoryItemId, new RecordStockMovementRequest(
                    line.Quantity,
                    "Entrada",
                    $"Recepcion {order.OrderNumber}",
                    request.OperatorName));
            }
        }

        var updated = order with
        {
            Status = status,
            ReceivedAt = status.Equals("Recibida", StringComparison.OrdinalIgnoreCase)
                ? order.ReceivedAt ?? DateTimeOffset.UtcNow
                : order.ReceivedAt,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        await SavePurchaseOrderAsync(updated);
        return updated;
    }

    public async Task DeletePurchaseOrderAsync(Guid purchaseOrderId)
    {
        await _session.ExecuteAsync(new SimpleStatement(
            "DELETE FROM purchase_orders WHERE restaurant_id = ? AND purchase_order_id = ?",
            RestaurantId,
            purchaseOrderId));
    }

    private async Task SavePurchaseOrderAsync(PurchaseOrder order)
    {
        await _session.ExecuteAsync(new SimpleStatement("""
            INSERT INTO purchase_orders
            (restaurant_id, purchase_order_id, order_number, supplier_id, supplier_name, status, lines_json,
             total, notes, ordered_at, received_at, updated_at)
            VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
            """,
            RestaurantId,
            order.PurchaseOrderId,
            order.OrderNumber,
            order.SupplierId,
            order.SupplierName,
            order.Status,
            JsonSerializer.Serialize(order.Lines),
            order.Total,
            order.Notes,
            order.OrderedAt,
            order.ReceivedAt,
            order.UpdatedAt));
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

    private static Supplier MapSupplier(Row row)
    {
        return new Supplier(
            row.GetValue<Guid>("supplier_id"),
            row.GetValue<string>("name"),
            row.GetValue<string>("contact_name"),
            row.GetValue<string>("phone"),
            row.GetValue<string>("email"),
            row.GetValue<string>("address"),
            row.GetValue<bool>("is_active"),
            row.GetValue<DateTimeOffset>("created_at"),
            row.GetValue<DateTimeOffset>("updated_at"));
    }

    private static PurchaseOrder MapPurchaseOrder(Row row)
    {
        return new PurchaseOrder(
            row.GetValue<Guid>("purchase_order_id"),
            row.GetValue<string>("order_number"),
            row.GetValue<Guid>("supplier_id"),
            row.GetValue<string>("supplier_name"),
            row.GetValue<string>("status"),
            JsonSerializer.Deserialize<List<PurchaseOrderLine>>(row.GetValue<string>("lines_json")) ?? [],
            row.GetValue<decimal>("total"),
            row.GetValue<string>("notes"),
            row.GetValue<DateTimeOffset>("ordered_at"),
            row.GetValue<DateTimeOffset?>("received_at"),
            row.GetValue<DateTimeOffset>("updated_at"));
    }
}
