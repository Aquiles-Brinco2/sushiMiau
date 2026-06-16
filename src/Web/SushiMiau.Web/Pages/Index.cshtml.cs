using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SushiMiau.Shared.Contracts;
using SushiMiau.Web.Services;

namespace SushiMiau.Web.Pages;

public class IndexModel : PageModel
{
    private readonly RestaurantApiClient _client;

    public IndexModel(RestaurantApiClient client)
    {
        _client = client;
    }

    public RestaurantDashboard Dashboard { get; private set; } = EmptyDashboard();

    [TempData]
    public string? Flash { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    [BindProperty]
    public InventoryItemForm Ingredient { get; set; } = new();

    [BindProperty]
    public StockMovementForm Movement { get; set; } = new();

    [BindProperty]
    public MenuItemForm MenuItem { get; set; } = new();

    [BindProperty]
    public TicketForm Ticket { get; set; } = new();

    [BindProperty]
    public OrderForm Order { get; set; } = new();

    [BindProperty]
    public ShiftForm Shift { get; set; } = new();

    [BindProperty]
    public PaymentForm Payment { get; set; } = new();

    [BindProperty]
    public TicketStatusForm TicketStatus { get; set; } = new();

    public async Task OnGetAsync()
    {
        await LoadDashboardAsync();
    }

    public async Task<IActionResult> OnPostIngredientAsync()
    {
        await ExecuteAndRedirectAsync(async () =>
        {
            await _client.AddInventoryItemAsync(new UpsertInventoryItemRequest(
                Ingredient.Name,
                Ingredient.Category,
                Ingredient.Unit,
                Ingredient.Stock,
                Ingredient.MinimumStock,
                Ingredient.Supplier));
            Flash = "Ingrediente guardado.";
        });

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostMovementAsync()
    {
        await ExecuteAndRedirectAsync(async () =>
        {
            await _client.AddStockMovementAsync(Movement.ItemId, new RecordStockMovementRequest(
                Movement.Quantity,
                Movement.MovementType,
                Movement.Reason,
                Movement.OperatorName));
            Flash = "Movimiento de inventario registrado.";
        });

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostMenuAsync()
    {
        await ExecuteAndRedirectAsync(async () =>
        {
            await _client.AddMenuItemAsync(new UpsertMenuItemRequest(
                MenuItem.Name,
                MenuItem.Category,
                MenuItem.Price,
                MenuItem.IsAvailable,
                MenuItem.PrepMinutes));
            Flash = "Producto de carta guardado.";
        });

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostTicketAsync()
    {
        await ExecuteAndRedirectAsync(async () =>
        {
            await _client.AddTicketAsync(new CreateKitchenTicketRequest(
                Ticket.Station,
                Ticket.TableOrChannel,
                SplitLines(Ticket.ItemsText),
                Ticket.Notes));
            Flash = "Comanda enviada a cocina.";
        });

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostTicketStatusAsync()
    {
        await ExecuteAndRedirectAsync(async () =>
        {
            await _client.UpdateTicketStatusAsync(TicketStatus.TicketId, TicketStatus.Status);
            Flash = "Estado de comanda actualizado.";
        });

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostOrderAsync()
    {
        await ExecuteAndRedirectAsync(async () =>
        {
            var lines = new[]
            {
                CreateLine(Order.Item1Name, Order.Item1Quantity, Order.Item1Price),
                CreateLine(Order.Item2Name, Order.Item2Quantity, Order.Item2Price),
                CreateLine(Order.Item3Name, Order.Item3Quantity, Order.Item3Price)
            }.Where(line => line is not null).Cast<OrderLine>().ToList();

            await _client.AddOrderAsync(new CreateOrderRequest(Order.TableOrChannel, Order.ServerName, lines));
            Flash = "Pedido registrado.";
        });

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostPaymentAsync()
    {
        await ExecuteAndRedirectAsync(async () =>
        {
            await _client.RegisterPaymentAsync(Payment.OrderId, Payment.PaymentMethod);
            Flash = "Pago registrado.";
        });

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostShiftAsync()
    {
        await ExecuteAndRedirectAsync(async () =>
        {
            await _client.AddShiftAsync(new CreateStaffShiftRequest(
                Shift.EmployeeName,
                Shift.Role,
                Shift.ShiftName,
                Shift.Status,
                new DateTimeOffset(Shift.StartsAt),
                new DateTimeOffset(Shift.EndsAt)));
            Flash = "Turno agregado.";
        });

        return RedirectToPage();
    }

    private async Task LoadDashboardAsync()
    {
        try
        {
            Dashboard = await _client.GetDashboardAsync();
        }
        catch (Exception ex)
        {
            Dashboard = EmptyDashboard();
            ErrorMessage = $"No se pudo cargar la informacion de los microservicios: {ex.Message}";
        }
    }

    private async Task ExecuteAndRedirectAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Operacion no completada: {ex.Message}";
        }
    }

    private static IReadOnlyList<string> SplitLines(string value) =>
        value.Split([Environment.NewLine, "\n", ","], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

    private static OrderLine? CreateLine(string name, int quantity, decimal price)
    {
        if (string.IsNullOrWhiteSpace(name) || quantity <= 0 || price <= 0)
        {
            return null;
        }

        return new OrderLine(name.Trim(), quantity, price);
    }

    private static RestaurantDashboard EmptyDashboard()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");

        return new RestaurantDashboard(
            new InventorySnapshot(0, 0, 0, []),
            [],
            [],
            [],
            [],
            [],
            [],
            new DailySalesSummary(today, 0, 0, 0, 0, 0));
    }
}

public sealed class InventoryItemForm
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = "Pescados";
    public string Unit { get; set; } = "kg";
    public decimal Stock { get; set; }
    public decimal MinimumStock { get; set; }
    public string Supplier { get; set; } = string.Empty;
}

public sealed class StockMovementForm
{
    public Guid ItemId { get; set; }
    public decimal Quantity { get; set; }
    public string MovementType { get; set; } = "Entrada";
    public string Reason { get; set; } = string.Empty;
    public string OperatorName { get; set; } = string.Empty;
}

public sealed class MenuItemForm
{
    public Guid ItemId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = "Rolls";
    public decimal Price { get; set; }
    public bool IsAvailable { get; set; } = true;
    public int PrepMinutes { get; set; } = 10;
    public string Ingredient1Name { get; set; } = string.Empty;
    public decimal Ingredient1Quantity { get; set; }
    public string Ingredient2Name { get; set; } = string.Empty;
    public decimal Ingredient2Quantity { get; set; }
    public string Ingredient3Name { get; set; } = string.Empty;
    public decimal Ingredient3Quantity { get; set; }
}

public sealed class TicketForm
{
    public string Station { get; set; } = "Sushi bar";
    public string TableOrChannel { get; set; } = string.Empty;
    public string ItemsText { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
}

public sealed class OrderForm
{
    public string TableOrChannel { get; set; } = string.Empty;
    public string ServerName { get; set; } = string.Empty;
    public string Item1Name { get; set; } = string.Empty;
    public int Item1Quantity { get; set; } = 1;
    public decimal Item1Price { get; set; }
    public string Item2Name { get; set; } = string.Empty;
    public int Item2Quantity { get; set; }
    public decimal Item2Price { get; set; }
    public string Item3Name { get; set; } = string.Empty;
    public int Item3Quantity { get; set; }
    public decimal Item3Price { get; set; }
}

public sealed class PaymentForm
{
    public Guid OrderId { get; set; }
    public string PaymentMethod { get; set; } = "Efectivo";
    public string BillingName { get; set; } = "Consumidor Final";
    public string TaxId { get; set; } = "0";
}

public sealed class ShiftForm
{
    public string EmployeeName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string ShiftName { get; set; } = "Cena";
    public string Status { get; set; } = "Activo";
    public DateTime StartsAt { get; set; } = DateTime.Now.Date.AddHours(18);
    public DateTime EndsAt { get; set; } = DateTime.Now.Date.AddHours(23);
}

public sealed class TicketStatusForm
{
    public Guid TicketId { get; set; }
    public string Status { get; set; } = "En preparacion";
}

public sealed class ReservationForm
{
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public int PartySize { get; set; } = 2;
    public DateTime ReservationTime { get; set; } = DateTime.Now.Date.AddHours(20);
    public string Notes { get; set; } = string.Empty;
}

public sealed class ReservationStatusForm
{
    public Guid ReservationId { get; set; }
    public string Status { get; set; } = "Confirmada";
}

public sealed class DeliveryOrderForm
{
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string DeliveryAddress { get; set; } = string.Empty;
    public string ServerName { get; set; } = string.Empty;
    public string Item1Name { get; set; } = string.Empty;
    public int Item1Quantity { get; set; } = 1;
    public decimal Item1Price { get; set; }
    public string Item2Name { get; set; } = string.Empty;
    public int Item2Quantity { get; set; }
    public decimal Item2Price { get; set; }
}

public sealed class DeliveryStatusForm
{
    public Guid OrderId { get; set; }
    public string DeliveryStatus { get; set; } = "En ruta";
}

public sealed class NotificationForm
{
    public string AudienceRole { get; set; } = AppRoles.Manager;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Severity { get; set; } = "Media";
}

public sealed class UserForm
{
    public string FullName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Role { get; set; } = AppRoles.Manager;
    public bool IsActive { get; set; } = true;
}

public sealed class UserUpdateForm
{
    public Guid UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = AppRoles.Manager;
    public bool IsActive { get; set; } = true;
    public string? Password { get; set; }
}

public sealed class CustomerForm
{
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Nit { get; set; } = "0";
}

public sealed class CategoryForm
{
    public string Name { get; set; } = string.Empty;
}
