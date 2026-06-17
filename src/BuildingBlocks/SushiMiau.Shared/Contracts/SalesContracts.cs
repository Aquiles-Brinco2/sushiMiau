namespace SushiMiau.Shared.Contracts;

public sealed record OrderLine(string ItemName, int Quantity, decimal UnitPrice, Guid? MenuItemId = null);

public sealed record RestaurantOrder(
    Guid OrderId,
    string BusinessDate,
    string TableOrChannel,
    string ServerName,
    string Status,
    string PaymentMethod,
    IReadOnlyList<OrderLine> Lines,
    decimal Subtotal,
    decimal Tax,
    decimal Total,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string OrderKind = "Mesa",
    string CustomerName = "",
    string CustomerPhone = "",
    string DeliveryAddress = "",
    string DeliveryStatus = "");

public sealed record CreateOrderRequest(
    string TableOrChannel,
    string ServerName,
    IReadOnlyList<OrderLine> Lines,
    string OrderKind = "Mesa",
    string CustomerName = "",
    string CustomerPhone = "",
    string DeliveryAddress = "");

public sealed record CreateDeliveryOrderRequest(
    string CustomerName,
    string CustomerPhone,
    string DeliveryAddress,
    string ServerName,
    IReadOnlyList<OrderLine> Lines);

public sealed record UpdateDeliveryStatusRequest(string DeliveryStatus);

public sealed record UpdateOrderRequest(
    string TableOrChannel,
    string ServerName,
    string Status,
    IReadOnlyList<OrderLine> Lines,
    string CustomerName = "",
    string CustomerPhone = "",
    string DeliveryAddress = "",
    string DeliveryStatus = "");

public sealed record UpdateOrderStatusRequest(string Status);

public sealed record RegisterPaymentRequest(
    string PaymentMethod,
    string BillingName = "Consumidor Final",
    string TaxId = "0");

public sealed record PaymentRecord(
    Guid PaymentId,
    Guid OrderId,
    string BusinessDate,
    string PaymentMethod,
    decimal Amount,
    string BillingName,
    string TaxId,
    DateTimeOffset PaidAt);

public sealed record Invoice(
    Guid InvoiceId,
    Guid OrderId,
    string BusinessDate,
    string InvoiceNumber,
    string BillingName,
    string TaxId,
    decimal Subtotal,
    decimal Tax,
    decimal Total,
    DateTimeOffset IssuedAt);

public sealed record DailySalesSummary(
    string BusinessDate,
    int Orders,
    int PaidOrders,
    decimal Subtotal,
    decimal Tax,
    decimal Total,
    decimal InvoicedTotal = 0);

public sealed record AdminReport(
    string BusinessDate,
    decimal SalesTotal,
    decimal InvoicedTotal,
    int PaidOrders,
    int DeliveryOrders,
    int OpenDeliveryOrders,
    int Reservations,
    int OpenReservations,
    int LowStockItems,
    int ActiveStaff);

public sealed record DishSalesMetric(
    string ItemName,
    int Quantity,
    decimal Total);
