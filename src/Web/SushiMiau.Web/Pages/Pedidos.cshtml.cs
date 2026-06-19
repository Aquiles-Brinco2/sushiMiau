using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SushiMiau.Shared;
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

    public IReadOnlyList<RestaurantOrder> TableOrders { get; private set; } = [];
    public IReadOnlyList<RestaurantOrder> ActiveTableOrders { get; private set; } = [];
    public IReadOnlyList<RestaurantOrder> ClosedTableOrders { get; private set; } = [];
    public IReadOnlyList<MenuItem> Menu { get; private set; } = [];
    public IReadOnlyList<RestaurantTable> Tables { get; private set; } = [];
    public IReadOnlyList<Customer> Customers { get; private set; } = [];
    public string OperatorName => User.Identity?.Name ?? "Operador";
    public bool ShowClosedOrders { get; private set; }

    [BindProperty]
    public OrderForm TableOrder { get; set; } = new();

    [BindProperty]
    public OrderEditForm EditOrder { get; set; } = new();

    [BindProperty]
    public DeleteForm Delete { get; set; } = new();

    [TempData]
    public string? Flash { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync(string? table, bool showClosed = false)
    {
        ShowClosedOrders = showClosed;
        if (!string.IsNullOrWhiteSpace(table))
        {
            TableOrder.TableOrChannel = table;
        }

        await LoadAsync();
    }

    public async Task<IActionResult> OnPostCreateTableAsync()
    {
        await RunAsync(async () =>
        {
            Menu = await _client.GetMenuAsync();
            var lines = CreateLinesFromMenu(TableOrder.Lines, Menu);
            Customers = await _client.GetCustomersAsync();
            var customer = Customers.FirstOrDefault(item => item.CustomerId == TableOrder.CustomerId);

            var order = await _client.AddOrderAsync(new CreateOrderRequest(
                TableOrder.TableOrChannel,
                OperatorName,
                lines,
                "Mesa",
                customer?.CustomerId,
                customer?.Name ?? string.Empty,
                customer?.Phone ?? string.Empty,
                "",
                TableOrder.Notes));

            if (order is not null && IsPaid(TableOrder.PaymentMethod))
            {
                await _client.RegisterPaymentAsync(order.OrderId, TableOrder.PaymentMethod, TableOrder.BillingName, TableOrder.TaxId);
            }

            Flash = IsPaid(TableOrder.PaymentMethod)
                ? "Pedido de mesa registrado y pagado."
                : "Pedido de mesa registrado.";
        });

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostUpdateAsync()
    {
        await RunAsync(async () =>
        {
            var order = await GetTableOrderAsync(EditOrder.OrderId);
            if (!CanModifyOrder(order))
            {
                throw new InvalidOperationException("Solo se pueden editar pedidos pendientes de mesa.");
            }

            await _client.UpdateOrderAsync(EditOrder.OrderId, new UpdateOrderRequest(
                EditOrder.TableOrChannel,
                EditOrder.ServerName,
                EditOrder.Status,
                Array.Empty<OrderLine>(),
                Notes: EditOrder.Notes));
            Flash = "Pedido actualizado.";
        });

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostStatusAsync()
    {
        await RunAsync(async () =>
        {
            var order = await GetTableOrderAsync(EditOrder.OrderId);
            if (!CanModifyOrder(order))
            {
                throw new InvalidOperationException("Solo se pueden actualizar pedidos pendientes de mesa.");
            }

            await _client.UpdateOrderStatusAsync(EditOrder.OrderId, EditOrder.Status);
            Flash = "Estado de pedido actualizado.";
        });

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync()
    {
        await RunAsync(async () =>
        {
            var order = await GetTableOrderAsync(Delete.Id);
            if (!CanModifyOrder(order))
            {
                throw new InvalidOperationException("Solo se pueden cancelar pedidos pendientes de mesa.");
            }

            await _client.DeleteOrderAsync(Delete.Id);
            Flash = "Pedido cancelado.";
        });

        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        try
        {
            var today = Today();
            var orders = await _client.GetOrdersAsync(today);
            TableOrders = orders.Where(order => !order.OrderKind.Equals("Delivery", StringComparison.OrdinalIgnoreCase)).ToList();
            ActiveTableOrders = TableOrders
                .Where(order => !order.Status.Equals("Pagado", StringComparison.OrdinalIgnoreCase)
                    && !order.Status.Equals("Cancelado", StringComparison.OrdinalIgnoreCase))
                .ToList();
            ClosedTableOrders = TableOrders
                .Where(order => order.Status.Equals("Pagado", StringComparison.OrdinalIgnoreCase)
                    || order.Status.Equals("Cancelado", StringComparison.OrdinalIgnoreCase))
                .ToList();
            Menu = await _client.GetMenuAsync();
            Tables = await _client.GetTablesAsync();
            Customers = await _client.GetCustomersAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"No se pudo cargar pedidos de mesa: {ex.Message}";
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

    private static bool CanModifyOrder(RestaurantOrder order) =>
        order.Status.Equals("Pendiente", StringComparison.OrdinalIgnoreCase);

    private async Task<RestaurantOrder> GetTableOrderAsync(Guid orderId)
    {
        var today = Today();
        var order = (await _client.GetOrdersAsync(today))
            .FirstOrDefault(item => item.OrderId == orderId
                && !item.OrderKind.Equals("Delivery", StringComparison.OrdinalIgnoreCase));

        return order ?? throw new InvalidOperationException("Pedido de mesa no encontrado.");
    }

    private static string Today() => BusinessClock.Today;
}
