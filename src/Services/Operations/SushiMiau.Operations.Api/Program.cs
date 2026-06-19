using SushiMiau.Operations.Api.Data;
using SushiMiau.Shared;
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

app.MapGet("/api/operations/tables", async (OperationsRepository repo) =>
    Results.Ok(await repo.GetTablesAsync()));

app.MapPut("/api/operations/tables/{tableName}", async (string tableName, UpdateTableStateRequest request, OperationsRepository repo) =>
    Results.Ok(await repo.UpdateTableStateAsync(Uri.UnescapeDataString(tableName), request)));

app.MapPost("/api/operations/tables", async (UpsertRestaurantTableRequest request, OperationsRepository repo) =>
{
    var table = await repo.UpsertTableAsync(null, request);
    return Results.Created($"/api/operations/tables/{Uri.EscapeDataString(table.TableName)}", table);
});

app.MapPut("/api/operations/tables/{tableName}/details", async (string tableName, UpsertRestaurantTableRequest request, OperationsRepository repo) =>
    Results.Ok(await repo.UpsertTableAsync(Uri.UnescapeDataString(tableName), request)));

app.MapDelete("/api/operations/tables/{tableName}", async (string tableName, OperationsRepository repo) =>
{
    await repo.DeleteTableAsync(Uri.UnescapeDataString(tableName));
    return Results.NoContent();
});

app.MapPost("/api/operations/menu", async (UpsertMenuItemRequest request, OperationsRepository repo) =>
{
    var item = await repo.UpsertMenuItemAsync(null, request);
    return Results.Created($"/api/operations/menu/{item.ItemId}", item);
});

app.MapPut("/api/operations/menu/{itemId:guid}", async (Guid itemId, UpsertMenuItemRequest request, OperationsRepository repo) =>
    Results.Ok(await repo.UpsertMenuItemAsync(itemId, request)));

app.MapDelete("/api/operations/menu/{itemId:guid}", async (Guid itemId, OperationsRepository repo) =>
{
    await repo.DeleteMenuItemAsync(itemId);
    return Results.NoContent();
});

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
    try
    {
        var customer = await repo.CreateCustomerAsync(request);
        return Results.Created($"/api/operations/customers/{customer.CustomerId}", customer);
    }
    catch (DuplicateCustomerTaxIdException ex)
    {
        return Results.Conflict(new { message = ex.Message });
    }
});

app.MapPut("/api/operations/customers/{customerId:guid}", async (Guid customerId, UpdateCustomerRequest request, OperationsRepository repo) =>
{
    try
    {
        var customer = await repo.UpdateCustomerAsync(customerId, request);
        return customer is null ? Results.NotFound() : Results.Ok(customer);
    }
    catch (DuplicateCustomerTaxIdException ex)
    {
        return Results.Conflict(new { message = ex.Message });
    }
});

app.MapDelete("/api/operations/customers/{customerId:guid}", async (Guid customerId, OperationsRepository repo) =>
{
    await repo.DeleteCustomerAsync(customerId);
    return Results.NoContent();
});

app.MapGet("/api/operations/customers/{customerId:guid}/loyalty", async (Guid customerId, OperationsRepository repo) =>
    Results.Ok(await repo.GetLoyaltyTransactionsAsync(customerId)));

app.MapPost("/api/operations/customers/{customerId:guid}/loyalty", async (Guid customerId, AdjustLoyaltyPointsRequest request, OperationsRepository repo) =>
{
    var customer = await repo.AdjustLoyaltyPointsAsync(customerId, request);
    return customer is null ? Results.NotFound() : Results.Ok(customer);
});

app.MapGet("/api/operations/tickets", async (string? businessDate, OperationsRepository repo) =>
    Results.Ok(await repo.GetTicketsAsync(businessDate ?? BusinessClock.Today)));

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
    Results.Ok(await repo.GetShiftsAsync(businessDate ?? BusinessClock.Today)));

app.MapPost("/api/operations/shifts", async (CreateStaffShiftRequest request, OperationsRepository repo) =>
{
    var shift = await repo.CreateShiftAsync(request);
    return Results.Created($"/api/operations/shifts/{shift.ShiftId}", shift);
});

app.MapPut("/api/operations/shifts/{shiftId:guid}", async (Guid shiftId, UpdateStaffShiftRequest request, OperationsRepository repo) =>
{
    var shift = await repo.UpdateShiftAsync(shiftId, request);
    return shift is null ? Results.NotFound() : Results.Ok(shift);
});

app.MapDelete("/api/operations/shifts/{shiftId:guid}", async (Guid shiftId, OperationsRepository repo) =>
{
    await repo.DeleteShiftAsync(shiftId);
    return Results.NoContent();
});

app.MapGet("/api/operations/reservations", async (string? businessDate, OperationsRepository repo) =>
    Results.Ok(await repo.GetReservationsAsync(businessDate ?? BusinessClock.Today)));

app.MapPost("/api/operations/reservations", async (CreateReservationRequest request, OperationsRepository repo) =>
{
    var table = (await repo.GetTablesAsync()).FirstOrDefault(item =>
        item.TableName.Equals(request.TableName, StringComparison.OrdinalIgnoreCase));
    if (table is null)
    {
        return Results.BadRequest(new { message = "La mesa seleccionada no existe." });
    }

    if (request.PartySize < 1 || request.PartySize > table.Capacity)
    {
        return Results.BadRequest(new
        {
            message = $"{table.TableName} admite como maximo {table.Capacity} personas."
        });
    }

    try
    {
        var reservation = await repo.CreateReservationAsync(request);
        return Results.Created($"/api/operations/reservations/{reservation.ReservationId}", reservation);
    }
    catch (TableAlreadyReservedException ex)
    {
        return Results.Conflict(new { message = ex.Message });
    }
});

app.MapPatch("/api/operations/reservations/{reservationId:guid}/status", async (Guid reservationId, UpdateReservationStatusRequest request, OperationsRepository repo) =>
{
    try
    {
        var reservation = await repo.UpdateReservationStatusAsync(reservationId, request.Status);
        return reservation is null ? Results.NotFound() : Results.Ok(reservation);
    }
    catch (TableAlreadyReservedException ex)
    {
        return Results.Conflict(new { message = ex.Message });
    }
});

app.MapPatch("/api/operations/reservations/{reservationId:guid}/order", async (
    Guid reservationId,
    UpdateReservationOrderRequest request,
    OperationsRepository repo) =>
{
    var reservation = await repo.UpdateReservationOrderAsync(reservationId, request.OrderId);
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
    var date = BusinessClock.Today;
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
