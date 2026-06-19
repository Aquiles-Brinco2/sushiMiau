using SushiMiau.Inventory.Api.Data;
using SushiMiau.Shared.Cassandra;
using SushiMiau.Shared.Contracts;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSushiMiauCassandra(builder.Configuration);
builder.Services.AddSingleton<InventoryRepository>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

var repository = app.Services.GetRequiredService<InventoryRepository>();
await repository.InitializeAsync();

app.MapGet("/health", () => Results.Ok(new { service = "inventory", status = "ok" }));

app.MapGet("/api/inventory/items", async (InventoryRepository repo) =>
    Results.Ok(await repo.GetItemsAsync()));

app.MapGet("/api/inventory/items/low", async (InventoryRepository repo) =>
    Results.Ok((await repo.GetItemsAsync()).Where(item => item.IsLowStock)));

app.MapGet("/api/inventory/categories", async (InventoryRepository repo) =>
    Results.Ok(await repo.GetCategoriesAsync()));

app.MapPost("/api/inventory/categories", async (InventoryCategory request, InventoryRepository repo) =>
{
    var category = await repo.CreateCategoryAsync(request.Name);
    return Results.Created($"/api/inventory/categories/{Uri.EscapeDataString(category.Name)}", category);
});

app.MapGet("/api/inventory/snapshot", async (InventoryRepository repo) =>
{
    var items = await repo.GetItemsAsync();
    var alerts = items.Where(item => item.IsLowStock)
        .OrderBy(item => item.Stock - item.MinimumStock)
        .Take(6)
        .ToList();

    return Results.Ok(new InventorySnapshot(
        items.Count,
        alerts.Count,
        items.Sum(item => item.Stock),
        alerts));
});

app.MapPost("/api/inventory/items", async (UpsertInventoryItemRequest request, InventoryRepository repo) =>
{
    var item = await repo.UpsertItemAsync(null, request);
    return Results.Created($"/api/inventory/items/{item.ItemId}", item);
});

app.MapPut("/api/inventory/items/{itemId:guid}", async (Guid itemId, UpsertInventoryItemRequest request, InventoryRepository repo) =>
    Results.Ok(await repo.UpsertItemAsync(itemId, request)));

app.MapDelete("/api/inventory/items/{itemId:guid}", async (Guid itemId, InventoryRepository repo) =>
{
    await repo.DeleteItemAsync(itemId);
    return Results.NoContent();
});

app.MapPost("/api/inventory/items/{itemId:guid}/movements", async (Guid itemId, RecordStockMovementRequest request, InventoryRepository repo) =>
{
    var movement = await repo.RecordMovementAsync(itemId, request);
    return movement is null ? Results.NotFound() : Results.Created($"/api/inventory/items/{itemId}/movements/{movement.MovementId}", movement);
});

app.MapGet("/api/inventory/suppliers", async (InventoryRepository repo) =>
    Results.Ok(await repo.GetSuppliersAsync()));

app.MapPost("/api/inventory/suppliers", async (UpsertSupplierRequest request, InventoryRepository repo) =>
{
    var supplier = await repo.UpsertSupplierAsync(null, request);
    return Results.Created($"/api/inventory/suppliers/{supplier.SupplierId}", supplier);
});

app.MapPut("/api/inventory/suppliers/{supplierId:guid}", async (Guid supplierId, UpsertSupplierRequest request, InventoryRepository repo) =>
    Results.Ok(await repo.UpsertSupplierAsync(supplierId, request)));

app.MapDelete("/api/inventory/suppliers/{supplierId:guid}", async (Guid supplierId, InventoryRepository repo) =>
{
    await repo.DeleteSupplierAsync(supplierId);
    return Results.NoContent();
});

app.MapGet("/api/inventory/purchase-orders", async (InventoryRepository repo) =>
    Results.Ok(await repo.GetPurchaseOrdersAsync()));

app.MapPost("/api/inventory/purchase-orders", async (CreatePurchaseOrderRequest request, InventoryRepository repo) =>
{
    var order = await repo.CreatePurchaseOrderAsync(request);
    return Results.Created($"/api/inventory/purchase-orders/{order.PurchaseOrderId}", order);
});

app.MapPatch("/api/inventory/purchase-orders/{purchaseOrderId:guid}/status", async (
    Guid purchaseOrderId,
    UpdatePurchaseOrderStatusRequest request,
    InventoryRepository repo) =>
{
    var order = await repo.UpdatePurchaseOrderStatusAsync(purchaseOrderId, request);
    return order is null ? Results.NotFound() : Results.Ok(order);
});

app.MapDelete("/api/inventory/purchase-orders/{purchaseOrderId:guid}", async (Guid purchaseOrderId, InventoryRepository repo) =>
{
    await repo.DeletePurchaseOrderAsync(purchaseOrderId);
    return Results.NoContent();
});

app.Run();
