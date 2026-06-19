using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SushiMiau.Shared;
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
    public IReadOnlyList<Reservation> PendingReservations { get; private set; } = [];
    public IReadOnlyList<Reservation> ConfirmedReservations { get; private set; } = [];
    public IReadOnlyList<Reservation> CompletedReservations { get; private set; } = [];
    public IReadOnlyList<Reservation> CancelledReservations { get; private set; } = [];
    public IReadOnlyList<Customer> Customers { get; private set; } = [];
    public IReadOnlyList<MenuItem> Menu { get; private set; } = [];
    public IReadOnlyList<RestaurantTable> Tables { get; private set; } = [];
    public string? FilterDate { get; set; }
    public string? FilterStatus { get; set; }
    public string? FilterHour { get; set; }
    public string? FilterCustomer { get; set; }
    public bool CanEditSelectedDate => FilterDate == Today();

    public bool ShowCompletedReservations => string.Equals(FilterStatus, "Completada", StringComparison.OrdinalIgnoreCase);
    public bool ShowCancelledReservations => string.Equals(FilterStatus, "Cancelada", StringComparison.OrdinalIgnoreCase);
    public bool ShowActiveReservations => !ShowCompletedReservations && !ShowCancelledReservations;

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
            var filteredReservations = reservations
                .Where(item => string.IsNullOrWhiteSpace(status) || item.Status.Equals(status, StringComparison.OrdinalIgnoreCase))
                .Where(item => string.IsNullOrWhiteSpace(hour) || item.ReservationTime.ToLocalTime().ToString("HH:mm").StartsWith(hour))
                .Where(item => string.IsNullOrWhiteSpace(customer) || item.CustomerName.Contains(customer, StringComparison.OrdinalIgnoreCase))
                .ToList();

            Reservations = filteredReservations;
            PendingReservations = filteredReservations
                .Where(item => item.Status.Equals("Pendiente", StringComparison.OrdinalIgnoreCase))
                .ToList();
            ConfirmedReservations = filteredReservations
                .Where(item => item.Status.Equals("Confirmada", StringComparison.OrdinalIgnoreCase))
                .ToList();
            CompletedReservations = filteredReservations
                .Where(item => item.Status.Equals("Completada", StringComparison.OrdinalIgnoreCase))
                .ToList();
            CancelledReservations = filteredReservations
                .Where(item => item.Status.Equals("Cancelada", StringComparison.OrdinalIgnoreCase))
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
            Tables = await _client.GetTablesAsync();
            var customer = Customers.FirstOrDefault(item => item.CustomerId == Reservation.CustomerId);
            if (customer is null)
            {
                throw new InvalidOperationException("Seleccione un cliente registrado.");
            }

            var table = Tables.FirstOrDefault(item =>
                item.TableName.Equals(Reservation.TableName, StringComparison.OrdinalIgnoreCase));
            if (table is null)
            {
                throw new InvalidOperationException("Seleccione una mesa registrada.");
            }

            if (Reservation.PartySize < 1 || Reservation.PartySize > table.Capacity)
            {
                throw new InvalidOperationException(
                    $"{table.TableName} admite como maximo {table.Capacity} personas.");
            }

            Menu = await _client.GetMenuAsync();
            var orderLines = CreateLines(OptionalOrder.Lines, Menu);
            var createdReservation = await _client.AddReservationAsync(new CreateReservationRequest(
                customer.CustomerId,
                customer.Name,
                customer.Phone,
                Reservation.TableName,
                Reservation.PartySize,
                BusinessClock.FromLocal(Reservation.ReservationTime),
                Reservation.Notes,
                null));

            if (createdReservation is not null && orderLines.Count > 0)
            {
                var order = await _client.AddOrderAsync(new CreateOrderRequest(
                    Reservation.TableName,
                    User.Identity?.Name ?? "Operador",
                    orderLines,
                    "Mesa",
                    customer.CustomerId,
                    customer.Name,
                    customer.Phone,
                    "",
                    "Pedido asociado a reserva"));
                if (order is not null)
                {
                    await _client.UpdateReservationOrderAsync(createdReservation.ReservationId, order.OrderId);
                }
            }

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

    private static string Today() => BusinessClock.Today;

    public async Task<IActionResult> OnPostCustomerAsync()
    {
        await RunAsync(async () =>
        {
            await _client.AddCustomerAsync(new CreateCustomerRequest(Customer.Name, Customer.Phone, Customer.Nit));
            Flash = "Cliente registrado.";
        });

        return RedirectToPage();
    }

    private static List<OrderLine> CreateLines(IEnumerable<OrderLineForm> lines, IReadOnlyList<MenuItem> menu) =>
        lines
            .Where(line => !string.IsNullOrWhiteSpace(line.ItemName) && line.Quantity > 0)
            .Select(line => CreateLine(line.ItemName, line.Quantity, menu))
            .Where(line => line is not null)
            .Cast<OrderLine>()
            .ToList();

    private static OrderLine? CreateLine(string name, int quantity, IReadOnlyList<MenuItem> menu)
    {
        var item = menu.FirstOrDefault(candidate => candidate.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        return item is null || quantity <= 0 ? null : new OrderLine(item.Name, quantity, item.Price, item.ItemId);
    }
}
