using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SushiMiau.Shared.Contracts;
using SushiMiau.Web.Services;

namespace SushiMiau.Web.Pages;

public sealed class ProveedoresModel : PageModel
{
    private readonly RestaurantApiClient _client;

    public ProveedoresModel(RestaurantApiClient client)
    {
        _client = client;
    }

    public IReadOnlyList<Supplier> Suppliers { get; private set; } = [];
    public IReadOnlyList<PurchaseOrder> PurchaseOrders { get; private set; } = [];
    public IReadOnlyList<InventoryItem> InventoryItems { get; private set; } = [];

    [BindProperty]
    public SupplierForm Supplier { get; set; } = new();

    [BindProperty]
    public PurchaseOrderForm PurchaseOrder { get; set; } = new();

    [BindProperty]
    public DeleteForm Delete { get; set; } = new();

    [TempData]
    public string? Flash { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnPostCreateSupplierAsync()
    {
        await RunAsync(async () =>
        {
            await _client.AddSupplierAsync(ToSupplierRequest());
            Flash = "Proveedor registrado.";
        });
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostUpdateSupplierAsync()
    {
        await RunAsync(async () =>
        {
            await _client.UpdateSupplierAsync(Supplier.SupplierId, ToSupplierRequest());
            Flash = "Proveedor actualizado.";
        });
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteSupplierAsync()
    {
        await RunAsync(async () =>
        {
            await _client.DeleteSupplierAsync(Delete.Id);
            Flash = "Proveedor eliminado.";
        });
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostCreateOrderAsync()
    {
        await RunAsync(async () =>
        {
            Suppliers = await _client.GetSuppliersAsync();
            InventoryItems = await _client.GetInventoryItemsAsync();
            var supplier = Suppliers.First(item => item.SupplierId == PurchaseOrder.SupplierId);
            var lines = PurchaseOrder.Lines
                .Where(line => line.InventoryItemId != Guid.Empty && line.Quantity > 0)
                .Select(line =>
                {
                    var item = InventoryItems.First(inventory => inventory.ItemId == line.InventoryItemId);
                    return new PurchaseOrderLine(item.ItemId, item.Name, line.Quantity, item.Unit, line.UnitPrice);
                })
                .ToList();
            if (lines.Count == 0)
            {
                throw new InvalidOperationException("Agregue al menos un ingrediente a la orden.");
            }

            await _client.AddPurchaseOrderAsync(new CreatePurchaseOrderRequest(
                supplier.SupplierId,
                supplier.Name,
                lines,
                PurchaseOrder.Notes));
            Flash = "Orden de compra registrada.";
        });
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostOrderStatusAsync(Guid purchaseOrderId, string status)
    {
        await RunAsync(async () =>
        {
            await _client.UpdatePurchaseOrderStatusAsync(purchaseOrderId, status, User.Identity?.Name ?? "Operador");
            Flash = status == "Recibida" ? "Orden recibida e inventario actualizado." : "Estado de orden actualizado.";
        });
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteOrderAsync()
    {
        await RunAsync(async () =>
        {
            await _client.DeletePurchaseOrderAsync(Delete.Id);
            Flash = "Orden eliminada.";
        });
        return RedirectToPage();
    }

    private UpsertSupplierRequest ToSupplierRequest() =>
        new(Supplier.Name, Supplier.ContactName, Supplier.Phone, Supplier.Email, Supplier.Address, Supplier.IsActive);

    private async Task LoadAsync()
    {
        try
        {
            Suppliers = await _client.GetSuppliersAsync();
            PurchaseOrders = await _client.GetPurchaseOrdersAsync();
            InventoryItems = await _client.GetInventoryItemsAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"No se pudo cargar compras: {ex.Message}";
        }
    }

    private async Task RunAsync(Func<Task> action)
    {
        try { await action(); }
        catch (Exception ex) { ErrorMessage = $"Operacion no completada: {ex.Message}"; }
    }
}
