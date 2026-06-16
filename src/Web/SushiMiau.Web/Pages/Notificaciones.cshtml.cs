using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SushiMiau.Shared.Contracts;
using SushiMiau.Web.Services;

namespace SushiMiau.Web.Pages;

public sealed class NotificacionesModel : PageModel
{
    private readonly RestaurantApiClient _client;

    public NotificacionesModel(RestaurantApiClient client)
    {
        _client = client;
    }

    public IReadOnlyList<NotificationMessage> Notifications { get; private set; } = [];
    public IReadOnlyList<string> Roles { get; } = [AppRoles.Admin, AppRoles.Owner, AppRoles.Manager, AppRoles.Kitchen, AppRoles.Cashier, AppRoles.Inventory];
    public string SelectedRole { get; private set; } = AppRoles.Manager;

    [BindProperty]
    public NotificationForm Notification { get; set; } = new();

    [TempData]
    public string? Flash { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync(string? role)
    {
        SelectedRole = string.IsNullOrWhiteSpace(role) ? AppRoles.Manager : role;
        Notifications = await _client.GetNotificationsAsync(SelectedRole);
    }

    public async Task<IActionResult> OnPostAsync()
    {
        try
        {
            await _client.AddNotificationAsync(new CreateNotificationRequest(Notification.AudienceRole, Notification.Title, Notification.Message, Notification.Severity));
            Flash = "Notificacion creada.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Operacion no completada: {ex.Message}";
        }

        return RedirectToPage(new { role = Notification.AudienceRole });
    }
}
