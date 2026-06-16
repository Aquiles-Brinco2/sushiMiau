using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SushiMiau.Shared.Contracts;
using SushiMiau.Web.Services;

namespace SushiMiau.Web.Pages;

public sealed class PedidosModel : PageModel
{
    private readonly RestaurantApiClient _client;

    public PedidosModel(RestaurantApiClient client)
    {
        _client = client;
    }

    public IReadOnlyList<RestaurantOrder> Orders { get; private set; } = [];
    public IReadOnlyList<RestaurantOrder> TableOrders { get; private set; } = [];
    public IReadOnlyList<Customer> Customers { get; private set; } = [];
    public IReadOnlyList<MenuItem> Menu { get; private set; } = [];

    [BindProperty]
    public OrderForm TableOrder { get; set; } = new();

    [BindProperty]
    public DeliveryOrderForm DeliveryOrder { get; set; } = new();

    [BindProperty]
    public DeliveryStatusForm DeliveryStatus { get; set; } = new();

    [TempData]
    public string? Flash { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync()
    {
        try
        {
            var today = Today();
            var orders = await _client.GetOrdersAsync(today);
            TableOrders = orders.Where(order => !order.OrderKind.Equals("Delivery", StringComparison.OrdinalIgnoreCase)).ToList();
            Orders = orders.Where(order => order.OrderKind.Equals("Delivery", StringComparison.OrdinalIgnoreCase)).ToList();
            Customers = await _client.GetCustomersAsync();
            Menu = await _client.GetMenuAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"No se pudo cargar pedidos: {ex.Message}";
        }
    }

    public async Task<IActionResult> OnPostCreateTableAsync()
    {
        await RunAsync(async () =>
        {
            var customer = Customers.FirstOrDefault(item => item.Name.Equals(TableOrder.TableOrChannel, StringComparison.OrdinalIgnoreCase));
            var lines = CreateLines(TableOrder.Item1Name, TableOrder.Item1Quantity, TableOrder.Item1Price, TableOrder.Item2Name, TableOrder.Item2Quantity, TableOrder.Item2Price);
            await _client.AddOrderAsync(new CreateOrderRequest(
                TableOrder.TableOrChannel,
                TableOrder.ServerName,
                lines,
                "Mesa"));
            Flash = "Pedido de mesa registrado.";
        });

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostCreateAsync()
    {
        await RunAsync(async () =>
        {
            var lines = CreateLines(DeliveryOrder.Item1Name, DeliveryOrder.Item1Quantity, DeliveryOrder.Item1Price, DeliveryOrder.Item2Name, DeliveryOrder.Item2Quantity, DeliveryOrder.Item2Price);

            await _client.AddDeliveryOrderAsync(new CreateDeliveryOrderRequest(
                DeliveryOrder.CustomerName,
                DeliveryOrder.CustomerPhone,
                DeliveryOrder.DeliveryAddress,
                DeliveryOrder.ServerName,
                lines));
            Flash = "Pedido delivery registrado.";
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

    private async Task RunAsync(Func<Task> action)
    {
        try { await action(); }
        catch (Exception ex) { ErrorMessage = $"Operacion no completada: {ex.Message}"; }
    }

    private static OrderLine? CreateLine(string name, int quantity, decimal price) =>
        string.IsNullOrWhiteSpace(name) || quantity <= 0 || price <= 0 ? null : new OrderLine(name.Trim(), quantity, price);

    private static List<OrderLine> CreateLines(string item1, int qty1, decimal price1, string item2, int qty2, decimal price2) =>
        new[] { CreateLine(item1, qty1, price1), CreateLine(item2, qty2, price2) }
            .Where(line => line is not null)
            .Cast<OrderLine>()
            .ToList();

    private static string Today() => DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
}
