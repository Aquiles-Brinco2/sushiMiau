using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SushiMiau.Shared.Contracts;
using SushiMiau.Web.Services;

namespace SushiMiau.Web.Pages;

public sealed class MesasModel : PageModel
{
    private readonly RestaurantApiClient _client;

    public MesasModel(RestaurantApiClient client)
    {
        _client = client;
    }

    public IReadOnlyList<RestaurantTable> Tables { get; private set; } = [];
    public IReadOnlyList<AppUser> Employees { get; private set; } = [];

    [BindProperty]
    public RestaurantTableForm Table { get; set; } = new();

    [TempData]
    public string? Flash { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnPostCreateAsync()
    {
        await RunAsync(async () =>
        {
            var employee = await ResolveEmployeeAsync(Table.AssignedEmployeeId);
            await _client.AddTableAsync(ToRequest(employee));
            Flash = "Mesa registrada.";
        });
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostUpdateAsync()
    {
        await RunAsync(async () =>
        {
            var employee = await ResolveEmployeeAsync(Table.AssignedEmployeeId);
            await _client.UpdateTableAsync(Table.CurrentName, ToRequest(employee));
            Flash = "Mesa actualizada.";
        });
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync()
    {
        await RunAsync(async () =>
        {
            await _client.DeleteTableAsync(Table.CurrentName);
            Flash = "Mesa eliminada.";
        });
        return RedirectToPage();
    }

    private UpsertRestaurantTableRequest ToRequest(AppUser? employee) =>
        new(Table.TableName, Table.Capacity, Table.Status, employee?.UserId, employee?.FullName ?? string.Empty);

    private async Task<AppUser?> ResolveEmployeeAsync(Guid? employeeId)
    {
        if (!employeeId.HasValue || employeeId.Value == Guid.Empty)
        {
            return null;
        }

        Employees = await _client.GetEmployeesAsync();
        return Employees.FirstOrDefault(item => item.UserId == employeeId.Value);
    }

    private async Task LoadAsync()
    {
        try
        {
            Tables = await _client.GetTablesAsync();
            Employees = await _client.GetEmployeesAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"No se pudo cargar mesas: {ex.Message}";
        }
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
