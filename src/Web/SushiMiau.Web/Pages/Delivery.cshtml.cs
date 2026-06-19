using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SushiMiau.Shared;
using SushiMiau.Shared.Contracts;
using SushiMiau.Web.Services;

namespace SushiMiau.Web.Pages;

public sealed class DeliveryModel : PageModel
{
    private readonly RestaurantApiClient _client;

    public DeliveryModel(RestaurantApiClient client)
    {
        _client = client;
    }

    public IReadOnlyList<RestaurantOrder> Orders { get; private set; } = [];
    public IReadOnlyList<Customer> Customers { get; private set; } = [];
    public IReadOnlyList<MenuItem> Menu { get; private set; } = [];
    public string OperatorName => User.Identity?.Name ?? "Operador";

    [BindProperty]
    public DeliveryOrderForm DeliveryOrder { get; set; } = new();

    [BindProperty]
    public DeliveryStatusForm DeliveryStatus { get; set; } = new();

    [BindProperty]
    public OrderEditForm EditOrder { get; set; } = new();

    [BindProperty]
    public DeleteForm Delete { get; set; } = new();

    [TempData]
    public string? Flash { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync()
    {
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostCreateAsync()
    {
        await RunAsync(async () =>
        {
            Menu = await _client.GetMenuAsync();
            var lines = CreateLinesFromMenu(DeliveryOrder.Lines, Menu);
            Customers = await _client.GetCustomersAsync();
            var customer = Customers.First(item => item.CustomerId == DeliveryOrder.CustomerId);

            var order = await _client.AddDeliveryOrderAsync(new CreateDeliveryOrderRequest(
                customer.CustomerId,
                customer.Name,
                customer.Phone,
                DeliveryOrder.DeliveryAddress,
                DeliveryOrder.DeliveryReference,
                DeliveryOrder.DeliveryFee,
                OperatorName,
                DeliveryOrder.Notes,
                lines));

            if (order is not null && IsPaid(DeliveryOrder.PaymentMethod))
            {
                await _client.RegisterPaymentAsync(order.OrderId, DeliveryOrder.PaymentMethod, DeliveryOrder.BillingName, DeliveryOrder.TaxId);
            }

            Flash = IsPaid(DeliveryOrder.PaymentMethod)
                ? "Pedido delivery registrado y pagado."
                : "Pedido delivery registrado.";
        });

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostStatusAsync()
    {
        await RunAsync(async () =>
        {
            await _client.UpdateDeliveryStatusAsync(DeliveryStatus.OrderId, DeliveryStatus.DeliveryStatus);
            Flash = "Estado de delivery actualizado.";
        });

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostUpdateAsync()
    {
        await RunAsync(async () =>
        {
            Menu = await _client.GetMenuAsync();
            var lines = CreateLinesFromMenu(CreateLines(
                EditOrder.Item1Name, EditOrder.Item1Quantity, EditOrder.Item1Price,
                EditOrder.Item2Name, EditOrder.Item2Quantity, EditOrder.Item2Price,
                EditOrder.Item3Name, EditOrder.Item3Quantity, EditOrder.Item3Price,
                EditOrder.Item4Name, EditOrder.Item4Quantity, EditOrder.Item4Price,
                EditOrder.Item5Name, EditOrder.Item5Quantity, EditOrder.Item5Price)
                .Select(line => new OrderLineForm { ItemName = line.ItemName, Quantity = line.Quantity }), Menu);

            await _client.UpdateOrderAsync(EditOrder.OrderId, new UpdateOrderRequest(
                "Delivery",
                EditOrder.ServerName,
                EditOrder.Status,
                lines,
                EditOrder.CustomerName,
                EditOrder.CustomerPhone,
                EditOrder.DeliveryAddress,
                EditOrder.DeliveryStatus,
                EditOrder.CustomerId,
                EditOrder.Notes,
                EditOrder.DeliveryReference,
                EditOrder.DeliveryFee));
            Flash = "Delivery actualizado.";
        });

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync()
    {
        await RunAsync(async () =>
        {
            await _client.DeleteOrderAsync(Delete.Id);
            Flash = "Delivery cancelado.";
        });

        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        try
        {
            var today = Today();
            Orders = await _client.GetDeliveryOrdersAsync(today);
            Customers = await _client.GetCustomersAsync();
            Menu = await _client.GetMenuAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"No se pudo cargar delivery: {ex.Message}";
        }
    }

    private async Task RunAsync(Func<Task> action)
    {
        try { await action(); }
        catch (Exception ex) { ErrorMessage = $"Operacion no completada: {ex.Message}"; }
    }

    private static bool IsPaid(string paymentMethod) =>
        !string.IsNullOrWhiteSpace(paymentMethod) && !paymentMethod.Equals("Pendiente", StringComparison.OrdinalIgnoreCase);

    private static List<OrderLine> CreateLines(params object[] values)
    {
        var lines = new List<OrderLine>();
        for (var index = 0; index + 2 < values.Length; index += 3)
        {
            var name = values[index]?.ToString() ?? string.Empty;
            var quantity = Convert.ToInt32(values[index + 1]);
            var price = Convert.ToDecimal(values[index + 2]);
            if (!string.IsNullOrWhiteSpace(name) && quantity > 0 && price > 0)
            {
                lines.Add(new OrderLine(name.Trim(), quantity, price));
            }
        }

        return lines;
    }

    private static List<OrderLine> CreateLinesFromMenu(IEnumerable<OrderLineForm> values, IReadOnlyList<MenuItem> menu)
    {
        return values
            .Where(line => !string.IsNullOrWhiteSpace(line.ItemName) && line.Quantity > 0)
            .Select(line =>
            {
                var item = menu.FirstOrDefault(menuItem => menuItem.Name.Equals(line.ItemName, StringComparison.OrdinalIgnoreCase));
                return item is null ? null : new OrderLine(item.Name, line.Quantity, item.Price, item.ItemId);
            })
            .Where(line => line is not null)
            .Cast<OrderLine>()
            .ToList();
    }

    private static string Today() => BusinessClock.Today;
}
