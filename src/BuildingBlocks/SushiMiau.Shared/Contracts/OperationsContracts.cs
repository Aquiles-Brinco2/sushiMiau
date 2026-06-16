namespace SushiMiau.Shared.Contracts;

public sealed record MenuItem(
    Guid ItemId,
    string Name,
    string Category,
    decimal Price,
    bool IsAvailable,
    int PrepMinutes,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<MenuIngredient> Ingredients);

public sealed record UpsertMenuItemRequest(
    string Name,
    string Category,
    decimal Price,
    bool IsAvailable,
    int PrepMinutes,
    IReadOnlyList<MenuIngredient>? Ingredients = null);

public sealed record MenuCategory(string Name);

public sealed record MenuIngredient(
    Guid InventoryItemId,
    string InventoryItemName,
    decimal Quantity,
    string Unit);

public sealed record KitchenTicket(
    Guid TicketId,
    string BusinessDate,
    string Station,
    string TableOrChannel,
    string Status,
    IReadOnlyList<string> Items,
    string Notes,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CreateKitchenTicketRequest(
    string Station,
    string TableOrChannel,
    IReadOnlyList<string> Items,
    string Notes);

public sealed record UpdateTicketStatusRequest(string Status);

public sealed record StaffShift(
    Guid ShiftId,
    string BusinessDate,
    string EmployeeName,
    string Role,
    string ShiftName,
    string Status,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt);

public sealed record CreateStaffShiftRequest(
    string EmployeeName,
    string Role,
    string ShiftName,
    string Status,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt);

public sealed record OperationsSnapshot(
    int AvailableMenuItems,
    int OpenTickets,
    int ActiveShifts,
    IReadOnlyList<KitchenTicket> LatestTickets);

public sealed record Reservation(
    Guid ReservationId,
    string BusinessDate,
    Guid CustomerId,
    string CustomerName,
    string CustomerPhone,
    int PartySize,
    DateTimeOffset ReservationTime,
    string Status,
    string Notes,
    Guid? OrderId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CreateReservationRequest(
    Guid CustomerId,
    string CustomerName,
    string CustomerPhone,
    int PartySize,
    DateTimeOffset ReservationTime,
    string Notes,
    Guid? OrderId = null);

public sealed record UpdateReservationStatusRequest(string Status);

public sealed record NotificationMessage(
    Guid NotificationId,
    string AudienceRole,
    string Title,
    string Message,
    string Severity,
    bool IsRead,
    DateTimeOffset CreatedAt);

public sealed record CreateNotificationRequest(
    string AudienceRole,
    string Title,
    string Message,
    string Severity);

public sealed record Customer(
    Guid CustomerId,
    string Name,
    string Phone,
    string Nit,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CreateCustomerRequest(
    string Name,
    string Phone,
    string Nit);
