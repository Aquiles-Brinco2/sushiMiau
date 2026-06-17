using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SushiMiau.Shared.Contracts;
using SushiMiau.Web.Services;

namespace SushiMiau.Web.Pages;

public sealed class ClientesModel : PageModel
{
    private readonly RestaurantApiClient _client;

    public ClientesModel(RestaurantApiClient client)
    {
        _client = client;
    }

    public IReadOnlyList<Customer> Customers { get; private set; } = [];

    [BindProperty]
    public CustomerForm Customer { get; set; } = new();

    [BindProperty]
    public DeleteForm Delete { get; set; } = new();

    [TempData]
    public string? Flash { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync()
    {
        try
        {
            Customers = await _client.GetCustomersAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"No se pudo cargar clientes: {ex.Message}";
        }
    }

    public async Task<IActionResult> OnPostCreateAsync()
    {
        try
        {
            await _client.AddCustomerAsync(new CreateCustomerRequest(Customer.Name, Customer.Phone, Customer.Nit));
            Flash = "Cliente registrado.";
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
            await _client.UpdateCustomerAsync(Customer.CustomerId, new UpdateCustomerRequest(Customer.Name, Customer.Phone, Customer.Nit));
            Flash = "Cliente actualizado.";
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
            await _client.DeleteCustomerAsync(Delete.Id);
            Flash = "Cliente eliminado.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Operacion no completada: {ex.Message}";
        }

        return RedirectToPage();
    }
}
