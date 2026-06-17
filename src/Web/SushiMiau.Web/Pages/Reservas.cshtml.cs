using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SushiMiau.Shared.Contracts;
using SushiMiau.Web.Services;

namespace SushiMiau.Web.Pages;

public sealed class ReservasModel : PageModel
{
    private readonly RestaurantApiClient _client;

    public ReservasModel(RestaurantApiClient client)
    {
        _client = client;
    }

    public IReadOnlyList<Reservation> Reservations { get; private set; } = [];
    public IReadOnlyList<Customer> Customers { get; private set; } = [];
    public IReadOnlyList<MenuItem> Menu { get; private set; } = [];
    public IReadOnlyList<RestaurantTable> Tables { get; private set; } = [];
    public string? FilterDate { get; set; }
    public string? FilterStatus { get; set; }
    public string? FilterHour { get; set; }
    public string? FilterCustomer { get; set; }
    public bool CanEditSelectedDate => FilterDate == Today();

    [BindProperty]
    public ReservationForm Reservation { get; set; } = new();

    [BindProperty]
    public ReservationStatusForm ReservationStatus { get; set; } = new();

    [BindProperty]
    public CustomerForm Customer { get; set; } = new();

    [BindProperty]
    public OrderForm OptionalOrder { get; set; } = new();

    [TempData]
    public string? Flash { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync(string? date, string? status, string? hour, string? customer, string? table)
    {
        try
        {
            FilterDate = string.IsNullOrWhiteSpace(date) ? Today() : date;
            FilterStatus = status;
            FilterHour = hour;
            FilterCustomer = customer;
            Customers = await _client.GetCustomersAsync();
            Menu = await _client.GetMenuAsync();
            Tables = await _client.GetTablesAsync();
            if (!string.IsNullOrWhiteSpace(table))
            {
                Reservation.TableName = table;
            }
            var reservations = await _client.GetReservationsAsync(FilterDate);
            Reservations = reservations
                .Where(item => string.IsNullOrWhiteSpace(status) || item.Status.Equals(status, StringComparison.OrdinalIgnoreCase))
                .Where(item => string.IsNullOrWhiteSpace(hour) || item.ReservationTime.ToLocalTime().ToString("HH:mm").StartsWith(hour))
                .Where(item => string.IsNullOrWhiteSpace(customer) || item.CustomerName.Contains(customer, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"No se pudo cargar reservas: {ex.Message}";
        }
    }

    public async Task<IActionResult> OnPostCreateAsync()
    {
        await RunAsync(async () =>
        {
            Customers = await _client.GetCustomersAsync();
            var customer = Customers.FirstOrDefault(item => item.Name.Equals(Reservation.CustomerName, StringComparison.OrdinalIgnoreCase));
            if (customer is null)
            {
                customer = await _client.AddCustomerAsync(new CreateCustomerRequest(Reservation.CustomerName, Reservation.CustomerPhone, "0"));
            }

            Guid? orderId = null;
            if (!string.IsNullOrWhiteSpace(OptionalOrder.Item1Name))
            {
                var order = await _client.AddOrderAsync(new CreateOrderRequest(
                    Reservation.TableName,
                    OptionalOrder.ServerName,
                    CreateLines(OptionalOrder.Item1Name, OptionalOrder.Item1Quantity, OptionalOrder.Item1Price, OptionalOrder.Item2Name, OptionalOrder.Item2Quantity, OptionalOrder.Item2Price),
                    "Mesa",
                    Reservation.CustomerName,
                    Reservation.CustomerPhone));
                orderId = order?.OrderId;
            }

            await _client.AddReservationAsync(new CreateReservationRequest(
                customer?.CustomerId ?? Guid.Empty,
                Reservation.CustomerName,
                Reservation.CustomerPhone,
                Reservation.TableName,
                Reservation.PartySize,
                new DateTimeOffset(Reservation.ReservationTime),
                Reservation.Notes,
                orderId));
            Flash = "Reserva registrada.";
        });

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostStatusAsync()
    {
        await RunAsync(async () =>
        {
            await _client.UpdateReservationStatusAsync(ReservationStatus.ReservationId, ReservationStatus.Status);
            Flash = "Reserva actualizada.";
        });

        return RedirectToPage();
    }

    private async Task RunAsync(Func<Task> action)
    {
        try { await action(); }
        catch (Exception ex) { ErrorMessage = $"Operacion no completada: {ex.Message}"; }
    }

    private static string Today() => DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");

    public async Task<IActionResult> OnPostCustomerAsync()
    {
        await RunAsync(async () =>
        {
            await _client.AddCustomerAsync(new CreateCustomerRequest(Customer.Name, Customer.Phone, Customer.Nit));
            Flash = "Cliente registrado.";
        });

        return RedirectToPage();
    }

    private static List<OrderLine> CreateLines(string item1, int qty1, decimal price1, string item2, int qty2, decimal price2) =>
        new[] { CreateLine(item1, qty1, price1), CreateLine(item2, qty2, price2) }
            .Where(line => line is not null)
            .Cast<OrderLine>()
            .ToList();

    private static OrderLine? CreateLine(string name, int quantity, decimal price) =>
        string.IsNullOrWhiteSpace(name) || quantity <= 0 || price <= 0 ? null : new OrderLine(name.Trim(), quantity, price);
}
