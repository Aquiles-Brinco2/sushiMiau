using SushiMiau.Sales.Api.Data;
using SushiMiau.Shared.Cassandra;
using SushiMiau.Shared.Contracts;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSushiMiauCassandra(builder.Configuration);
builder.Services.AddSingleton<SalesRepository>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

var repository = app.Services.GetRequiredService<SalesRepository>();
await repository.InitializeAsync();

app.MapGet("/health", () => Results.Ok(new { service = "sales", status = "ok" }));

app.MapGet("/api/sales/orders", async (string? businessDate, SalesRepository repo) =>
    Results.Ok(await repo.GetOrdersAsync(businessDate ?? DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"))));

app.MapPost("/api/sales/orders", async (CreateOrderRequest request, SalesRepository repo) =>
{
    var order = await repo.CreateOrderAsync(request);
    return Results.Created($"/api/sales/orders/{order.OrderId}", order);
});

app.MapPut("/api/sales/orders/{orderId:guid}", async (Guid orderId, UpdateOrderRequest request, SalesRepository repo) =>
{
    var order = await repo.UpdateOrderAsync(orderId, request);
    return order is null ? Results.NotFound() : Results.Ok(order);
});

app.MapPatch("/api/sales/orders/{orderId:guid}/status", async (Guid orderId, UpdateOrderStatusRequest request, SalesRepository repo) =>
{
    var order = await repo.UpdateOrderStatusAsync(orderId, request.Status);
    return order is null ? Results.NotFound() : Results.Ok(order);
});

app.MapDelete("/api/sales/orders/{orderId:guid}", async (Guid orderId, SalesRepository repo) =>
{
    await repo.DeleteOrderAsync(orderId);
    return Results.NoContent();
});

app.MapGet("/api/sales/delivery-orders", async (string? businessDate, SalesRepository repo) =>
{
    var orders = await repo.GetOrdersAsync(businessDate ?? DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"));
    return Results.Ok(orders.Where(order => order.OrderKind.Equals("Delivery", StringComparison.OrdinalIgnoreCase)));
});

app.MapPost("/api/sales/delivery-orders", async (CreateDeliveryOrderRequest request, SalesRepository repo) =>
{
    var order = await repo.CreateDeliveryOrderAsync(request);
    return Results.Created($"/api/sales/orders/{order.OrderId}", order);
});

app.MapPatch("/api/sales/delivery-orders/{orderId:guid}/status", async (Guid orderId, UpdateDeliveryStatusRequest request, SalesRepository repo) =>
{
    var order = await repo.UpdateDeliveryStatusAsync(orderId, request.DeliveryStatus);
    return order is null ? Results.NotFound() : Results.Ok(order);
});

app.MapPatch("/api/sales/orders/{orderId:guid}/pay", async (Guid orderId, RegisterPaymentRequest request, SalesRepository repo) =>
{
    var order = await repo.RegisterPaymentAsync(orderId, request);
    return order is null ? Results.NotFound() : Results.Ok(order);
});

app.MapGet("/api/sales/payments", async (string? businessDate, SalesRepository repo) =>
    Results.Ok(await repo.GetPaymentsAsync(businessDate ?? DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"))));

app.MapGet("/api/sales/invoices", async (string? businessDate, SalesRepository repo) =>
    Results.Ok(await repo.GetInvoicesAsync(businessDate ?? DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"))));

app.MapGet("/api/sales/summary", async (string? businessDate, SalesRepository repo) =>
    Results.Ok(await repo.GetSummaryAsync(businessDate ?? DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"))));

app.MapGet("/api/sales/dish-metrics", async (string? businessDate, SalesRepository repo) =>
    Results.Ok(await repo.GetDishSalesMetricsAsync(businessDate ?? DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"))));

app.Run();
