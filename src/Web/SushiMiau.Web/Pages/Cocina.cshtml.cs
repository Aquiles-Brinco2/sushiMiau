using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SushiMiau.Shared;
using SushiMiau.Shared.Contracts;
using SushiMiau.Web.Services;

namespace SushiMiau.Web.Pages;

public sealed class CocinaModel : PageModel
{
    private readonly RestaurantApiClient _client;

    public CocinaModel(RestaurantApiClient client)
    {
        _client = client;
    }

    public IReadOnlyList<KitchenTicket> Tickets { get; private set; } = [];
    public string FilterDate { get; private set; } = Today();
    public bool ShowHistory { get; private set; }

    [BindProperty]
    public TicketStatusForm TicketStatus { get; set; } = new();

    [TempData]
    public string? Flash { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync(string? date, bool showHistory = false)
    {
        FilterDate = string.IsNullOrWhiteSpace(date) ? Today() : date;
        ShowHistory = showHistory;
        try
        {
            var allTickets = await _client.GetTicketsAsync(FilterDate);
            Tickets = ShowHistory
                ? allTickets
                : allTickets.Where(ticket => ticket.Status is not "Listo" and not "Entregado" and not "Cancelado").ToList();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"No se pudo cargar cocina: {ex.Message}";
        }
    }

    public async Task<IActionResult> OnPostStatusAsync()
    {
        try
        {
            await _client.UpdateTicketStatusAsync(TicketStatus.TicketId, TicketStatus.Status);
            if (TicketStatus.OrderId.HasValue)
            {
                await _client.UpdateOrderStatusAsync(TicketStatus.OrderId.Value, TicketStatus.Status);
            }
            Flash = "Estado de cocina actualizado.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Operacion no completada: {ex.Message}";
        }
        return RedirectToPage();
    }

    private static string Today() => BusinessClock.Today;
}
