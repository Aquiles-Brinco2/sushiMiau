using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SushiMiau.Shared;
using SushiMiau.Shared.Contracts;
using SushiMiau.Web.Services;

namespace SushiMiau.Web.Pages;

public sealed class PersonalModel : PageModel
{
    private readonly RestaurantApiClient _client;

    public PersonalModel(RestaurantApiClient client)
    {
        _client = client;
    }

    public IReadOnlyList<AppUser> Employees { get; private set; } = [];
    public IReadOnlyList<StaffShift> Shifts { get; private set; } = [];
    public string FilterDate { get; private set; } = Today();

    [BindProperty]
    public ShiftForm Shift { get; set; } = new();

    [TempData]
    public string? Flash { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync(string? date)
    {
        FilterDate = string.IsNullOrWhiteSpace(date) ? Today() : date;
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostCreateAsync()
    {
        await RunAsync(async () =>
        {
            var employee = await GetEmployeeAsync();
            await _client.AddShiftAsync(new CreateStaffShiftRequest(
                employee.UserId,
                employee.FullName,
                employee.Role,
                Shift.ShiftName,
                Shift.Status,
                BusinessClock.FromLocal(Shift.StartsAt),
                BusinessClock.FromLocal(Shift.EndsAt)));
            Flash = "Turno registrado.";
        });
        return RedirectToPage(new { date = Shift.StartsAt.ToString("yyyy-MM-dd") });
    }

    public async Task<IActionResult> OnPostUpdateAsync()
    {
        await RunAsync(async () =>
        {
            var employee = await GetEmployeeAsync();
            await _client.UpdateShiftAsync(Shift.ShiftId, new UpdateStaffShiftRequest(
                employee.UserId,
                employee.FullName,
                employee.Role,
                Shift.ShiftName,
                Shift.Status,
                BusinessClock.FromLocal(Shift.StartsAt),
                BusinessClock.FromLocal(Shift.EndsAt)));
            Flash = "Turno actualizado.";
        });
        return RedirectToPage(new { date = Shift.StartsAt.ToString("yyyy-MM-dd") });
    }

    public async Task<IActionResult> OnPostDeleteAsync()
    {
        await RunAsync(async () =>
        {
            await _client.DeleteShiftAsync(Shift.ShiftId);
            Flash = "Turno eliminado.";
        });
        return RedirectToPage();
    }

    private async Task<AppUser> GetEmployeeAsync()
    {
        Employees = await _client.GetEmployeesAsync();
        return Employees.First(item => item.UserId == Shift.EmployeeId);
    }

    private async Task LoadAsync()
    {
        try
        {
            Employees = await _client.GetEmployeesAsync();
            Shifts = await _client.GetShiftsAsync(FilterDate);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"No se pudo cargar personal: {ex.Message}";
        }
    }

    private async Task RunAsync(Func<Task> action)
    {
        try { await action(); }
        catch (Exception ex) { ErrorMessage = $"Operacion no completada: {ex.Message}"; }
    }

    private static string Today() => BusinessClock.Today;
}
