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
    Guid? OrderId,
    string Status,
    IReadOnlyList<string> Items,
    string Notes,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CreateKitchenTicketRequest(
    string Station,
    string TableOrChannel,
    Guid? OrderId,
    IReadOnlyList<string> Items,
    string Notes);

public sealed record UpdateTicketStatusRequest(string Status);

public sealed record StaffShift(
    Guid ShiftId,
    string BusinessDate,
    Guid EmployeeId,
    string EmployeeName,
    string Role,
    string ShiftName,
    string Status,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt);

public sealed record CreateStaffShiftRequest(
    Guid EmployeeId,
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
    string TableName,
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
    string TableName,
    int PartySize,
    DateTimeOffset ReservationTime,
    string Notes,
    Guid? OrderId = null);

public sealed record UpdateReservationStatusRequest(string Status);

public sealed record UpdateReservationOrderRequest(Guid? OrderId);

public sealed record RestaurantTable(
    string TableName,
    int Capacity,
    string Status,
    Guid? AssignedEmployeeId,
    string AssignedEmployee,
    DateTimeOffset UpdatedAt);

public sealed record UpdateTableStateRequest(
    string Status,
    Guid? AssignedEmployeeId = null,
    string AssignedEmployee = "");

public sealed record UpsertRestaurantTableRequest(
    string TableName,
    int Capacity,
    string Status,
    Guid? AssignedEmployeeId,
    string AssignedEmployee);

public sealed record UpdateStaffShiftRequest(
    Guid EmployeeId,
    string EmployeeName,
    string Role,
    string ShiftName,
    string Status,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt);

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
    DateTimeOffset UpdatedAt,
    int LoyaltyPoints = 0);

public sealed record CreateCustomerRequest(
    string Name,
    string Phone,
    string Nit);

public sealed record UpdateCustomerRequest(
    string Name,
    string Phone,
    string Nit);

public sealed record LoyaltyTransaction(
    Guid TransactionId,
    Guid CustomerId,
    int Points,
    string MovementType,
    string Reason,
    Guid? OrderId,
    DateTimeOffset CreatedAt);

public sealed record AdjustLoyaltyPointsRequest(
    int Points,
    string MovementType,
    string Reason,
    Guid? OrderId = null);
