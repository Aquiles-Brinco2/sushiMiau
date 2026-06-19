using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SushiMiau.Shared;
using SushiMiau.Shared.Contracts;
using SushiMiau.Web.Services;

namespace SushiMiau.Web.Pages;

public sealed class PagosModel : PageModel
{
    private readonly RestaurantApiClient _client;

    public PagosModel(RestaurantApiClient client)
    {
        _client = client;
    }

    public IReadOnlyList<RestaurantOrder> OpenOrders { get; private set; } = [];
    public IReadOnlyList<PaymentRecord> Payments { get; private set; } = [];
    public IReadOnlyList<Invoice> Invoices { get; private set; } = [];
    public DailySalesSummary Summary { get; private set; } = new(Today(), 0, 0, 0, 0, 0);
    public bool ShowHistory { get; private set; }

    [BindProperty]
    public PaymentForm Payment { get; set; } = new();

    [TempData]
    public string? Flash { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync(bool showHistory = false)
    {
        ShowHistory = showHistory;
        try
        {
            var today = Today();
            var orders = await _client.GetOrdersAsync(today);
            OpenOrders = orders.Where(order => !order.Status.Equals("Pagado", StringComparison.OrdinalIgnoreCase)
                    && !order.Status.Equals("Cancelado", StringComparison.OrdinalIgnoreCase)).ToList();
            Payments = await _client.GetPaymentsAsync(today);
            Invoices = await _client.GetInvoicesAsync(today);
            Summary = await _client.GetSalesSummaryAsync(today);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"No se pudo cargar pagos: {ex.Message}";
        }
    }

    public async Task<IActionResult> OnPostPayAsync()
    {
        try
        {
            await _client.RegisterPaymentAsync(Payment.OrderId, Payment.PaymentMethod, Payment.BillingName, Payment.TaxId);
            Flash = "Pago registrado y factura emitida.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Operacion no completada: {ex.Message}";
        }

        return RedirectToPage();
    }

    private static string Today() => BusinessClock.Today;
}
