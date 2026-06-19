using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SushiMiau.Shared.Contracts;
using SushiMiau.Web.Services;

namespace SushiMiau.Web.Pages;

public sealed class MenuModel : PageModel
{
    private readonly RestaurantApiClient _client;

    public MenuModel(RestaurantApiClient client)
    {
        _client = client;
    }

    public IReadOnlyList<MenuItem> Items { get; private set; } = [];
    public IReadOnlyList<MenuCategory> Categories { get; private set; } = [];
    public IReadOnlyList<InventoryItem> InventoryItems { get; private set; } = [];

    [BindProperty]
    public MenuItemForm MenuItem { get; set; } = new();

    [BindProperty]
    public CategoryForm Category { get; set; } = new();

    [BindProperty]
    public InventoryItemForm Ingredient { get; set; } = new();

    [BindProperty]
    public DeleteForm Delete { get; set; } = new();

    [TempData]
    public string? Flash { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync()
    {
        Items = await _client.GetMenuAsync();
        Categories = await _client.GetMenuCategoriesAsync();
        InventoryItems = await _client.GetInventoryItemsAsync();
    }

    public async Task<IActionResult> OnPostCreateAsync()
    {
        try
        {
            await _client.AddMenuItemAsync(ToRequest(MenuItem));
            Flash = "Producto guardado.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Operacion no completada: {ex.Message}";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostUpdateAsync()
    {
        try
        {
            await _client.UpdateMenuItemAsync(MenuItem.ItemId, ToRequest(MenuItem));
            Flash = "Producto actualizado.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Operacion no completada: {ex.Message}";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync()
    {
        try
        {
            await _client.DeleteMenuItemAsync(Delete.Id);
            Flash = "Producto eliminado.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Operacion no completada: {ex.Message}";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostCategoryAsync()
    {
        try
        {
            await _client.AddMenuCategoryAsync(Category.Name);
            Flash = "Categoria creada.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Operacion no completada: {ex.Message}";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostIngredientAsync()
    {
        try
        {
            await _client.AddInventoryItemAsync(new UpsertInventoryItemRequest(
                Ingredient.Name,
                Ingredient.Category,
                Ingredient.Unit,
                Ingredient.Stock,
                Ingredient.MinimumStock,
                Ingredient.Supplier));
            Flash = "Ingrediente creado.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Operacion no completada: {ex.Message}";
        }

        return RedirectToPage();
    }

    private UpsertMenuItemRequest ToRequest(MenuItemForm form)
    {
        var ingredients = new[]
        {
            CreateIngredient(form.Ingredient1Name, form.Ingredient1Quantity),
            CreateIngredient(form.Ingredient2Name, form.Ingredient2Quantity),
            CreateIngredient(form.Ingredient3Name, form.Ingredient3Quantity)
        }.Where(ingredient => ingredient is not null).Cast<MenuIngredient>().ToList();

        if (ingredients.Count == 0)
        {
            throw new InvalidOperationException("Agregue al menos un ingrediente del inventario.");
        }

        return new UpsertMenuItemRequest(
            form.Name,
            form.Category,
            form.Price,
            form.IsAvailable,
            form.PrepMinutes,
            ingredients);
    }

    private MenuIngredient? CreateIngredient(string name, decimal quantity)
    {
        if (string.IsNullOrWhiteSpace(name) || quantity <= 0)
        {
            return null;
        }

        var item = InventoryItems.FirstOrDefault(candidate => candidate.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase));
        return item is null ? null : new MenuIngredient(item.ItemId, item.Name, quantity, item.Unit);
    }
}
