using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SushiMiau.Shared.Contracts;
using SushiMiau.Web.Services;

namespace SushiMiau.Web.Pages;

[Authorize(Roles = AppRoles.Admin)]
public sealed class UsuariosModel : PageModel
{
    private readonly RestaurantApiClient _client;

    public UsuariosModel(RestaurantApiClient client)
    {
        _client = client;
    }

    public IReadOnlyList<AppUser> Users { get; private set; } = [];
    public IReadOnlyList<string> Roles { get; } =
        [AppRoles.Admin, AppRoles.Owner, AppRoles.Manager, AppRoles.Kitchen, AppRoles.Cashier, AppRoles.Inventory, AppRoles.Waiter, AppRoles.Delivery];

    [BindProperty]
    public UserForm UserForm { get; set; } = new();

    [BindProperty]
    public UserUpdateForm UserUpdate { get; set; } = new();

    [TempData]
    public string? Flash { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync()
    {
        Users = await _client.GetUsersAsync();
    }

    public async Task<IActionResult> OnPostCreateAsync()
    {
        await RunAsync(async () =>
        {
            await _client.AddUserAsync(new CreateUserRequest(UserForm.FullName, UserForm.Username, UserForm.Password, UserForm.Role, UserForm.IsActive));
            Flash = "Usuario creado.";
        });

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostUpdateAsync()
    {
        await RunAsync(async () =>
        {
            await _client.UpdateUserAsync(UserUpdate.UserId, new UpdateUserRequest(UserUpdate.FullName, UserUpdate.Role, UserUpdate.IsActive, UserUpdate.Password));
            Flash = "Usuario actualizado.";
        });

        return RedirectToPage();
    }

    private async Task RunAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Operacion no completada: {ex.Message}";
        }
    }
}
