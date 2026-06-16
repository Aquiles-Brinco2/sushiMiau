using SushiMiau.Operations.Api.Data;
using SushiMiau.Shared.Cassandra;
using SushiMiau.Shared.Contracts;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSushiMiauCassandra(builder.Configuration);
builder.Services.AddSingleton<OperationsRepository>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

var repository = app.Services.GetRequiredService<OperationsRepository>();
await repository.InitializeAsync();

app.MapGet("/health", () => Results.Ok(new { service = "operations", status = "ok" }));

app.MapGet("/api/operations/menu", async (OperationsRepository repo) =>
    Results.Ok(await repo.GetMenuAsync()));

app.MapPost("/api/operations/menu", async (UpsertMenuItemRequest request, OperationsRepository repo) =>
{
    var item = await repo.UpsertMenuItemAsync(null, request);
    return Results.Created($"/api/operations/menu/{item.ItemId}", item);
});

app.MapPut("/api/operations/menu/{itemId:guid}", async (Guid itemId, UpsertMenuItemRequest request, OperationsRepository repo) =>
    Results.Ok(await repo.UpsertMenuItemAsync(itemId, request)));

app.MapGet("/api/operations/menu-categories", async (OperationsRepository repo) =>
    Results.Ok(await repo.GetMenuCategoriesAsync()));

app.MapPost("/api/operations/menu-categories", async (MenuCategory request, OperationsRepository repo) =>
{
    var category = await repo.CreateMenuCategoryAsync(request.Name);
    return Results.Created($"/api/operations/menu-categories/{Uri.EscapeDataString(category.Name)}", category);
});

app.MapGet("/api/operations/customers", async (OperationsRepository repo) =>
    Results.Ok(await repo.GetCustomersAsync()));

app.MapPost("/api/operations/customers", async (CreateCustomerRequest request, OperationsRepository repo) =>
{
    var customer = await repo.CreateCustomerAsync(request);
    return Results.Created($"/api/operations/customers/{customer.CustomerId}", customer);
});

app.MapGet("/api/operations/tickets", async (string? businessDate, OperationsRepository repo) =>
    Results.Ok(await repo.GetTicketsAsync(businessDate ?? DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"))));

app.MapPost("/api/operations/tickets", async (CreateKitchenTicketRequest request, OperationsRepository repo) =>
{
    var ticket = await repo.CreateTicketAsync(request);
    return Results.Created($"/api/operations/tickets/{ticket.TicketId}", ticket);
});

app.MapPatch("/api/operations/tickets/{ticketId:guid}/status", async (Guid ticketId, UpdateTicketStatusRequest request, OperationsRepository repo) =>
{
    var ticket = await repo.UpdateTicketStatusAsync(ticketId, request.Status);
    return ticket is null ? Results.NotFound() : Results.Ok(ticket);
});

app.MapGet("/api/operations/shifts", async (string? businessDate, OperationsRepository repo) =>
    Results.Ok(await repo.GetShiftsAsync(businessDate ?? DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"))));

app.MapPost("/api/operations/shifts", async (CreateStaffShiftRequest request, OperationsRepository repo) =>
{
    var shift = await repo.CreateShiftAsync(request);
    return Results.Created($"/api/operations/shifts/{shift.ShiftId}", shift);
});

app.MapGet("/api/operations/reservations", async (string? businessDate, OperationsRepository repo) =>
    Results.Ok(await repo.GetReservationsAsync(businessDate ?? DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"))));

app.MapPost("/api/operations/reservations", async (CreateReservationRequest request, OperationsRepository repo) =>
{
    var reservation = await repo.CreateReservationAsync(request);
    return Results.Created($"/api/operations/reservations/{reservation.ReservationId}", reservation);
});

app.MapPatch("/api/operations/reservations/{reservationId:guid}/status", async (Guid reservationId, UpdateReservationStatusRequest request, OperationsRepository repo) =>
{
    var reservation = await repo.UpdateReservationStatusAsync(reservationId, request.Status);
    return reservation is null ? Results.NotFound() : Results.Ok(reservation);
});

app.MapGet("/api/operations/notifications", async (string? role, OperationsRepository repo) =>
    Results.Ok(await repo.GetNotificationsAsync(role ?? AppRoles.Manager)));

app.MapPost("/api/operations/notifications", async (CreateNotificationRequest request, OperationsRepository repo) =>
{
    var notification = await repo.CreateNotificationAsync(request);
    return Results.Created($"/api/operations/notifications/{notification.NotificationId}", notification);
});

app.MapGet("/api/operations/snapshot", async (OperationsRepository repo) =>
{
    var date = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
    var menu = await repo.GetMenuAsync();
    var tickets = await repo.GetTicketsAsync(date);
    var shifts = await repo.GetShiftsAsync(date);

    return Results.Ok(new OperationsSnapshot(
        menu.Count(item => item.IsAvailable),
        tickets.Count(ticket => ticket.Status is not "Entregado" and not "Cancelado"),
        shifts.Count(shift => shift.Status.Equals("Activo", StringComparison.OrdinalIgnoreCase)),
        tickets.Take(5).ToList()));
});

app.Run();
