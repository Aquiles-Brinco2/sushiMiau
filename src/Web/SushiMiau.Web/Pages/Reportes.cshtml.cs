using Microsoft.AspNetCore.Mvc.RazorPages;
using SushiMiau.Shared.Contracts;
using SushiMiau.Web.Services;

namespace SushiMiau.Web.Pages;

public sealed class ReportesModel : PageModel
{
    private readonly RestaurantApiClient _client;

    public ReportesModel(RestaurantApiClient client)
    {
        _client = client;
    }

    public AdminReport Report { get; private set; } = new(Today(), 0, 0, 0, 0, 0, 0, 0, 0, 0);
    public IReadOnlyList<InventoryItem> StockAlerts { get; private set; } = [];
    public IReadOnlyList<RestaurantOrder> DeliveryOrders { get; private set; } = [];
    public IReadOnlyList<Reservation> Reservations { get; private set; } = [];
    public IReadOnlyList<DishSalesMetric> DishMetrics { get; private set; } = [];
    public string? ErrorMessage { get; private set; }

    public async Task OnGetAsync()
    {
        try
        {
            var today = Today();
            var summary = await _client.GetSalesSummaryAsync(today);
            var deliveries = await _client.GetDeliveryOrdersAsync(today);
            var reservations = await _client.GetReservationsAsync(today);
            var inventory = await _client.GetInventorySnapshotAsync();
            var shifts = await _client.GetShiftsAsync(today);
            DishMetrics = await _client.GetDishMetricsAsync(today);

            DeliveryOrders = deliveries;
            Reservations = reservations;
            StockAlerts = inventory.Alerts;
            Report = new AdminReport(
                today,
                summary.Total,
                summary.InvoicedTotal,
                summary.PaidOrders,
                deliveries.Count,
                deliveries.Count(order => order.DeliveryStatus != "Entregado"),
                reservations.Count,
                reservations.Count(reservation => reservation.Status is not "Cancelada" and not "Completada"),
                inventory.LowStockItems,
                shifts.Count(shift => shift.Status.Equals("Activo", StringComparison.OrdinalIgnoreCase)));
        }
        catch (Exception ex)
        {
            ErrorMessage = $"No se pudo cargar reportes: {ex.Message}";
        }
    }

    private static string Today() => DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
}
