using Microsoft.AspNetCore.Mvc.RazorPages;
using SushiMiau.Shared;
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
    public SalesPeriodReport PeriodReport { get; private set; } = new(Today(), Today(), 0, 0, 0, 0, 0, 0, [], []);
    public IReadOnlyList<InventoryItem> StockAlerts { get; private set; } = [];
    public IReadOnlyList<RestaurantOrder> DeliveryOrders { get; private set; } = [];
    public IReadOnlyList<Reservation> Reservations { get; private set; } = [];
    public IReadOnlyList<DishSalesMetric> DishMetrics { get; private set; } = [];
    public IReadOnlyList<ChartGroup> DeliveryStatusGroups { get; private set; } = [];
    public IReadOnlyList<ChartGroup> ReservationStatusGroups { get; private set; } = [];
    public IReadOnlyList<string> InventoryCategories { get; private set; } = [];
    public int MaxDishQuantity { get; private set; }
    public decimal MaxDishTotal { get; private set; }
    public string FilterDate { get; private set; } = Today();
    public string Period { get; private set; } = "Diario";
    public string FromDate { get; private set; } = Today();
    public string ToDate { get; private set; } = Today();
    public string? FilterDeliveryStatus { get; private set; }
    public string? FilterReservationStatus { get; private set; }
    public string? FilterDish { get; private set; }
    public string? FilterInventoryCategory { get; private set; }
    public string? ErrorMessage { get; private set; }

    public async Task OnGetAsync(
        string? date,
        string? period,
        string? fromDate,
        string? toDate,
        string? deliveryStatus,
        string? reservationStatus,
        string? dish,
        string? inventoryCategory)
    {
        try
        {
            FilterDate = string.IsNullOrWhiteSpace(date) ? Today() : date;
            Period = string.IsNullOrWhiteSpace(period) ? "Diario" : period;
            (FromDate, ToDate) = ResolveRange(FilterDate, Period, fromDate, toDate);
            FilterDeliveryStatus = deliveryStatus;
            FilterReservationStatus = reservationStatus;
            FilterDish = dish;
            FilterInventoryCategory = inventoryCategory;

            PeriodReport = await _client.GetSalesPeriodReportAsync(FromDate, ToDate);
            var deliveries = await _client.GetDeliveryOrdersAsync(FilterDate);
            var reservations = await _client.GetReservationsAsync(FilterDate);
            var inventory = await _client.GetInventorySnapshotAsync();
            var shifts = await _client.GetShiftsAsync(FilterDate);
            var dishMetrics = PeriodReport.DishMetrics;

            DeliveryStatusGroups = BuildGroups(deliveries.Select(order => order.DeliveryStatus));
            ReservationStatusGroups = BuildGroups(reservations.Select(reservation => reservation.Status));
            InventoryCategories = inventory.Alerts
                .Select(item => item.Category)
                .Where(category => !string.IsNullOrWhiteSpace(category))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(category => category, StringComparer.OrdinalIgnoreCase)
                .ToList();

            DeliveryOrders = deliveries
                .Where(order => Matches(order.DeliveryStatus, FilterDeliveryStatus))
                .ToList();
            Reservations = reservations
                .Where(reservation => Matches(reservation.Status, FilterReservationStatus))
                .ToList();
            StockAlerts = inventory.Alerts
                .Where(item => Matches(item.Category, FilterInventoryCategory))
                .ToList();
            DishMetrics = dishMetrics
                .Where(metric => string.IsNullOrWhiteSpace(FilterDish) || metric.ItemName.Contains(FilterDish, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(metric => metric.Total)
                .ToList();

            MaxDishQuantity = DishMetrics.Count == 0 ? 0 : DishMetrics.Max(metric => metric.Quantity);
            MaxDishTotal = DishMetrics.Count == 0 ? 0 : DishMetrics.Max(metric => metric.Total);
            Report = new AdminReport(
                FilterDate,
                PeriodReport.Total,
                PeriodReport.InvoicedTotal,
                PeriodReport.PaidOrders,
                DeliveryOrders.Count,
                DeliveryOrders.Count(order => order.DeliveryStatus != "Entregado"),
                Reservations.Count,
                Reservations.Count(reservation => reservation.Status is not "Cancelada" and not "Completada"),
                StockAlerts.Count,
                shifts.Count(shift => shift.Status.Equals("Activo", StringComparison.OrdinalIgnoreCase)));
        }
        catch (Exception ex)
        {
            ErrorMessage = $"No se pudo cargar reportes: {ex.Message}";
        }
    }

    private static bool Matches(string value, string? filter) =>
        string.IsNullOrWhiteSpace(filter) || value.Equals(filter, StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<ChartGroup> BuildGroups(IEnumerable<string> values)
    {
        var groups = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .GroupBy(value => value, StringComparer.OrdinalIgnoreCase)
            .Select(group => new ChartGroup(group.Key, group.Count()))
            .OrderByDescending(group => group.Count)
            .ToList();

        var max = groups.Count == 0 ? 0 : groups.Max(group => group.Count);
        return groups.Select(group => group with { Percent = max == 0 ? 0 : group.Count * 100 / max }).ToList();
    }

    private static string Today() => BusinessClock.Today;

    private static (string From, string To) ResolveRange(string date, string period, string? fromDate, string? toDate)
    {
        var selected = DateOnly.TryParse(date, out var parsed) ? parsed : DateOnly.Parse(BusinessClock.Today);
        return period switch
        {
            "Semanal" => (selected.AddDays(-(((int)selected.DayOfWeek + 6) % 7)).ToString("yyyy-MM-dd"),
                selected.AddDays(6 - (((int)selected.DayOfWeek + 6) % 7)).ToString("yyyy-MM-dd")),
            "Mensual" => (new DateOnly(selected.Year, selected.Month, 1).ToString("yyyy-MM-dd"),
                new DateOnly(selected.Year, selected.Month, DateTime.DaysInMonth(selected.Year, selected.Month)).ToString("yyyy-MM-dd")),
            "Personalizado" when DateOnly.TryParse(fromDate, out var from) && DateOnly.TryParse(toDate, out var to) =>
                (from.ToString("yyyy-MM-dd"), to.ToString("yyyy-MM-dd")),
            _ => (selected.ToString("yyyy-MM-dd"), selected.ToString("yyyy-MM-dd"))
        };
    }
}

public sealed record ChartGroup(string Label, int Count)
{
    public int Percent { get; init; }
}
