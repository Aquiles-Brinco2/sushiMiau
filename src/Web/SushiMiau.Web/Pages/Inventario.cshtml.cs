using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SushiMiau.Shared.Contracts;
using SushiMiau.Web.Services;

namespace SushiMiau.Web.Pages;

public sealed class InventarioModel : PageModel
{
    private readonly RestaurantApiClient _client;

    public InventarioModel(RestaurantApiClient client)
    {
        _client = client;
    }

    public IReadOnlyList<InventoryItem> Items { get; private set; } = [];
    public IReadOnlyList<InventoryCategory> Categories { get; private set; } = [];
    public IReadOnlyList<string> Units { get; } = ["kg", "g", "unidad", "paquete", "litro", "ml"];
    public InventorySnapshot Snapshot { get; private set; } = new(0, 0, 0, []);

    [BindProperty]
    public InventoryItemForm Ingredient { get; set; } = new();

    [BindProperty]
    public CategoryForm Category { get; set; } = new();

    [BindProperty]
    public NotificationForm Notification { get; set; } = new();

    [TempData]
    public string? Flash { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync()
    {
        Items = await _client.GetInventoryItemsAsync();
        Categories = await _client.GetInventoryCategoriesAsync();
        Snapshot = await _client.GetInventorySnapshotAsync();
    }

    public async Task<IActionResult> OnPostIngredientAsync()
    {
        await RunAsync(async () =>
        {
            await _client.AddInventoryItemAsync(new UpsertInventoryItemRequest(Ingredient.Name, Ingredient.Category, Ingredient.Unit, Ingredient.Stock, Ingredient.MinimumStock, Ingredient.Supplier));
            Flash = "Ingrediente guardado.";
        });

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostCategoryAsync()
    {
        await RunAsync(async () =>
        {
            await _client.AddInventoryCategoryAsync(Category.Name);
            Flash = "Categoria creada.";
        });

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostNotifyAsync()
    {
        await RunAsync(async () =>
        {
            await _client.AddNotificationAsync(new CreateNotificationRequest(Notification.AudienceRole, Notification.Title, Notification.Message, Notification.Severity));
            Flash = "Notificacion creada.";
        });

        return RedirectToPage();
    }

    private async Task RunAsync(Func<Task> action)
    {
        try { await action(); }
        catch (Exception ex) { ErrorMessage = $"Operacion no completada: {ex.Message}"; }
    }
}
