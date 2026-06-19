using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Claims;
using SushiMiau.Shared;
using SushiMiau.Shared.Contracts;

namespace SushiMiau.Web.Services;

public sealed class RestaurantApiClient
{
    private static readonly Dictionary<string, Uri> LocalFallbacks = new()
    {
        ["Identity"] = new Uri("http://localhost:5204"),
        ["Inventory"] = new Uri("http://localhost:5201"),
        ["Operations"] = new Uri("http://localhost:5202"),
        ["Sales"] = new Uri("http://localhost:5203")
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public RestaurantApiClient(IHttpClientFactory httpClientFactory, IHttpContextAccessor httpContextAccessor)
    {
        _httpClientFactory = httpClientFactory;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<RestaurantDashboard> GetDashboardAsync()
    {
        var today = BusinessClock.Today;

        var inventorySnapshot = await GetJsonAsync<InventorySnapshot>("Inventory", "/api/inventory/snapshot")
            ?? new InventorySnapshot(0, 0, 0, []);
        var items = await GetJsonAsync<List<InventoryItem>>("Inventory", "/api/inventory/items") ?? [];
        var menu = await GetJsonAsync<List<MenuItem>>("Operations", "/api/operations/menu") ?? [];
        var tickets = await GetJsonAsync<List<KitchenTicket>>("Operations", $"/api/operations/tickets?businessDate={today}") ?? [];
        var shifts = await GetJsonAsync<List<StaffShift>>("Operations", $"/api/operations/shifts?businessDate={today}") ?? [];
        var reservations = await GetJsonAsync<List<Reservation>>("Operations", $"/api/operations/reservations?businessDate={today}") ?? [];
        var orders = await GetJsonAsync<List<RestaurantOrder>>("Sales", $"/api/sales/orders?businessDate={today}") ?? [];
        var summary = await GetJsonAsync<DailySalesSummary>("Sales", $"/api/sales/summary?businessDate={today}")
            ?? new DailySalesSummary(today, 0, 0, 0, 0, 0);

        return new RestaurantDashboard(inventorySnapshot, items, menu, tickets, shifts, reservations, orders, summary);
    }

    public async Task<LoginResponse?> LoginAsync(LoginRequest request)
    {
        var response = await SendJsonAsync("Identity", HttpMethod.Post, "/api/auth/login", request);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<LoginResponse>();
    }

    public async Task<List<AppUser>> GetUsersAsync() =>
        await GetJsonAsync<List<AppUser>>("Identity", "/api/users", CreateAdminHeaders()) ?? [];

    public Task<List<AppUser>> GetEmployeesAsync() =>
        GetListAsync<AppUser>("Identity", "/api/employees");

    public async Task AddUserAsync(CreateUserRequest request)
    {
        var response = await SendJsonAsync("Identity", HttpMethod.Post, "/api/users", request, CreateAdminHeaders());
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdateUserAsync(Guid userId, UpdateUserRequest request)
    {
        var response = await SendJsonAsync("Identity", HttpMethod.Put, $"/api/users/{userId}", request, CreateAdminHeaders());
        response.EnsureSuccessStatusCode();
    }

    public Task<List<InventoryItem>> GetInventoryItemsAsync() =>
        GetListAsync<InventoryItem>("Inventory", "/api/inventory/items");

    public Task<List<InventoryCategory>> GetInventoryCategoriesAsync() =>
        GetListAsync<InventoryCategory>("Inventory", "/api/inventory/categories");

    public async Task AddInventoryCategoryAsync(string name)
    {
        var response = await SendJsonAsync("Inventory", HttpMethod.Post, "/api/inventory/categories", new InventoryCategory(name));
        response.EnsureSuccessStatusCode();
    }

    public async Task<InventorySnapshot> GetInventorySnapshotAsync() =>
        await GetJsonAsync<InventorySnapshot>("Inventory", "/api/inventory/snapshot")
        ?? new InventorySnapshot(0, 0, 0, []);

    public Task<List<MenuItem>> GetMenuAsync() =>
        GetListAsync<MenuItem>("Operations", "/api/operations/menu");

    public Task<List<RestaurantTable>> GetTablesAsync() =>
        GetListAsync<RestaurantTable>("Operations", "/api/operations/tables");

    public async Task UpdateTableStateAsync(string tableName, UpdateTableStateRequest request)
    {
        var response = await SendJsonAsync("Operations", HttpMethod.Put, $"/api/operations/tables/{Uri.EscapeDataString(tableName)}", request);
        response.EnsureSuccessStatusCode();
    }

    public async Task AddTableAsync(UpsertRestaurantTableRequest request)
    {
        var response = await SendJsonAsync("Operations", HttpMethod.Post, "/api/operations/tables", request);
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdateTableAsync(string currentName, UpsertRestaurantTableRequest request)
    {
        var response = await SendJsonAsync(
            "Operations",
            HttpMethod.Put,
            $"/api/operations/tables/{Uri.EscapeDataString(currentName)}/details",
            request);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteTableAsync(string tableName)
    {
        var response = await SendAsync("Operations", HttpMethod.Delete, $"/api/operations/tables/{Uri.EscapeDataString(tableName)}");
        response.EnsureSuccessStatusCode();
    }

    public Task<List<MenuCategory>> GetMenuCategoriesAsync() =>
        GetListAsync<MenuCategory>("Operations", "/api/operations/menu-categories");

    public async Task AddMenuCategoryAsync(string name)
    {
        var response = await SendJsonAsync("Operations", HttpMethod.Post, "/api/operations/menu-categories", new MenuCategory(name));
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdateMenuItemAsync(Guid itemId, UpsertMenuItemRequest request)
    {
        var response = await SendJsonAsync("Operations", HttpMethod.Put, $"/api/operations/menu/{itemId}", request);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteMenuItemAsync(Guid itemId)
    {
        var response = await SendAsync("Operations", HttpMethod.Delete, $"/api/operations/menu/{itemId}");
        response.EnsureSuccessStatusCode();
    }

    public Task<List<Customer>> GetCustomersAsync() =>
        GetListAsync<Customer>("Operations", "/api/operations/customers");

    public async Task<Customer?> AddCustomerAsync(CreateCustomerRequest request)
    {
        var response = await SendJsonAsync("Operations", HttpMethod.Post, "/api/operations/customers", request);
        await EnsureSuccessWithMessageAsync(response);
        return await response.Content.ReadFromJsonAsync<Customer>();
    }

    public async Task UpdateCustomerAsync(Guid customerId, UpdateCustomerRequest request)
    {
        var response = await SendJsonAsync("Operations", HttpMethod.Put, $"/api/operations/customers/{customerId}", request);
        await EnsureSuccessWithMessageAsync(response);
    }

    public async Task DeleteCustomerAsync(Guid customerId)
    {
        var response = await SendAsync("Operations", HttpMethod.Delete, $"/api/operations/customers/{customerId}");
        response.EnsureSuccessStatusCode();
    }

    public Task<List<LoyaltyTransaction>> GetLoyaltyTransactionsAsync(Guid customerId) =>
        GetListAsync<LoyaltyTransaction>("Operations", $"/api/operations/customers/{customerId}/loyalty");

    public async Task AdjustLoyaltyPointsAsync(Guid customerId, AdjustLoyaltyPointsRequest request)
    {
        var response = await SendJsonAsync("Operations", HttpMethod.Post, $"/api/operations/customers/{customerId}/loyalty", request);
        response.EnsureSuccessStatusCode();
    }

    public Task<List<RestaurantOrder>> GetOrdersByCustomerAsync(Guid customerId) =>
        GetListAsync<RestaurantOrder>("Sales", $"/api/sales/orders/customer/{customerId}");

    public Task<List<KitchenTicket>> GetTicketsAsync(string businessDate) =>
        GetListAsync<KitchenTicket>("Operations", $"/api/operations/tickets?businessDate={businessDate}");

    public Task<List<StaffShift>> GetShiftsAsync(string businessDate) =>
        GetListAsync<StaffShift>("Operations", $"/api/operations/shifts?businessDate={businessDate}");

    public Task<List<Reservation>> GetReservationsAsync(string businessDate) =>
        GetListAsync<Reservation>("Operations", $"/api/operations/reservations?businessDate={businessDate}");

    public Task<List<NotificationMessage>> GetNotificationsAsync(string role) =>
        GetListAsync<NotificationMessage>("Operations", $"/api/operations/notifications?role={Uri.EscapeDataString(role)}");

    public Task<List<RestaurantOrder>> GetOrdersAsync(string businessDate) =>
        GetListAsync<RestaurantOrder>("Sales", $"/api/sales/orders?businessDate={businessDate}");

    public Task<List<RestaurantOrder>> GetDeliveryOrdersAsync(string businessDate) =>
        GetListAsync<RestaurantOrder>("Sales", $"/api/sales/delivery-orders?businessDate={businessDate}");

    public Task<List<PaymentRecord>> GetPaymentsAsync(string businessDate) =>
        GetListAsync<PaymentRecord>("Sales", $"/api/sales/payments?businessDate={businessDate}");

    public Task<List<Invoice>> GetInvoicesAsync(string businessDate) =>
        GetListAsync<Invoice>("Sales", $"/api/sales/invoices?businessDate={businessDate}");

    public Task<List<DishSalesMetric>> GetDishMetricsAsync(string businessDate) =>
        GetListAsync<DishSalesMetric>("Sales", $"/api/sales/dish-metrics?businessDate={businessDate}");

    public async Task<DailySalesSummary> GetSalesSummaryAsync(string businessDate) =>
        await GetJsonAsync<DailySalesSummary>("Sales", $"/api/sales/summary?businessDate={businessDate}")
        ?? new DailySalesSummary(businessDate, 0, 0, 0, 0, 0);

    public async Task AddInventoryItemAsync(UpsertInventoryItemRequest request)
    {
        var response = await SendJsonAsync("Inventory", HttpMethod.Post, "/api/inventory/items", request);
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdateInventoryItemAsync(Guid itemId, UpsertInventoryItemRequest request)
    {
        var response = await SendJsonAsync("Inventory", HttpMethod.Put, $"/api/inventory/items/{itemId}", request);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteInventoryItemAsync(Guid itemId)
    {
        var response = await SendAsync("Inventory", HttpMethod.Delete, $"/api/inventory/items/{itemId}");
        response.EnsureSuccessStatusCode();
    }

    public async Task AddStockMovementAsync(Guid itemId, RecordStockMovementRequest request)
    {
        var response = await SendJsonAsync("Inventory", HttpMethod.Post, $"/api/inventory/items/{itemId}/movements", request);
        response.EnsureSuccessStatusCode();
    }

    public async Task AddMenuItemAsync(UpsertMenuItemRequest request)
    {
        var response = await SendJsonAsync("Operations", HttpMethod.Post, "/api/operations/menu", request);
        response.EnsureSuccessStatusCode();
    }

    public async Task AddTicketAsync(CreateKitchenTicketRequest request)
    {
        var response = await SendJsonAsync("Operations", HttpMethod.Post, "/api/operations/tickets", request);
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdateTicketStatusAsync(Guid ticketId, string status)
    {
        var response = await SendJsonAsync("Operations", HttpMethod.Patch, $"/api/operations/tickets/{ticketId}/status", new UpdateTicketStatusRequest(status));
        response.EnsureSuccessStatusCode();
    }

    public async Task AddShiftAsync(CreateStaffShiftRequest request)
    {
        var response = await SendJsonAsync("Operations", HttpMethod.Post, "/api/operations/shifts", request);
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdateShiftAsync(Guid shiftId, UpdateStaffShiftRequest request)
    {
        var response = await SendJsonAsync("Operations", HttpMethod.Put, $"/api/operations/shifts/{shiftId}", request);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteShiftAsync(Guid shiftId)
    {
        var response = await SendAsync("Operations", HttpMethod.Delete, $"/api/operations/shifts/{shiftId}");
        response.EnsureSuccessStatusCode();
    }

    public async Task<RestaurantOrder?> AddOrderAsync(CreateOrderRequest request)
    {
        var response = await SendJsonAsync("Sales", HttpMethod.Post, "/api/sales/orders", request);
        response.EnsureSuccessStatusCode();
        var order = await response.Content.ReadFromJsonAsync<RestaurantOrder>();
        await DiscountInventoryForOrderAsync(order);
        await CreateKitchenTicketForOrderAsync(order);
        if (order is not null)
        {
            await UpdateTableStateAsync(order.TableOrChannel, new UpdateTableStateRequest("Ocupada", null, order.ServerName));
        }
        return order;
    }

    public async Task<RestaurantOrder?> AddDeliveryOrderAsync(CreateDeliveryOrderRequest request)
    {
        var response = await SendJsonAsync("Sales", HttpMethod.Post, "/api/sales/delivery-orders", request);
        response.EnsureSuccessStatusCode();
        var order = await response.Content.ReadFromJsonAsync<RestaurantOrder>();
        await DiscountInventoryForOrderAsync(order);
        await CreateKitchenTicketForOrderAsync(order);
        return order;
    }

    public async Task UpdateDeliveryStatusAsync(Guid orderId, string status)
    {
        var response = await SendJsonAsync("Sales", HttpMethod.Patch, $"/api/sales/delivery-orders/{orderId}/status", new UpdateDeliveryStatusRequest(status));
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdateOrderStatusAsync(Guid orderId, string status)
    {
        var response = await SendJsonAsync("Sales", HttpMethod.Patch, $"/api/sales/orders/{orderId}/status", new UpdateOrderStatusRequest(status));
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdateOrderAsync(Guid orderId, UpdateOrderRequest request)
    {
        var response = await SendJsonAsync("Sales", HttpMethod.Put, $"/api/sales/orders/{orderId}", request);
        await EnsureSuccessWithMessageAsync(response);
    }

    public async Task DeleteOrderAsync(Guid orderId)
    {
        var response = await SendAsync("Sales", HttpMethod.Delete, $"/api/sales/orders/{orderId}");
        response.EnsureSuccessStatusCode();
    }

    public async Task<RestaurantOrder?> RegisterPaymentAsync(Guid orderId, string paymentMethod, string billingName = "Consumidor Final", string taxId = "0")
    {
        var response = await SendJsonAsync("Sales", HttpMethod.Patch, $"/api/sales/orders/{orderId}/pay", new RegisterPaymentRequest(paymentMethod, billingName, taxId));
        response.EnsureSuccessStatusCode();
        var order = await response.Content.ReadFromJsonAsync<RestaurantOrder>();
        if (order?.CustomerId is { } customerId && customerId != Guid.Empty)
        {
            await AdjustLoyaltyPointsAsync(customerId, new AdjustLoyaltyPointsRequest(
                Math.Max(1, (int)Math.Floor(order.Total / 10m)),
                "Acumulacion",
                $"Puntos por pedido {order.OrderId}",
                order.OrderId));
        }

        return order;
    }

    public async Task<Reservation?> AddReservationAsync(CreateReservationRequest request)
    {
        var response = await SendJsonAsync("Operations", HttpMethod.Post, "/api/operations/reservations", request);
        await EnsureSuccessWithMessageAsync(response);
        return await response.Content.ReadFromJsonAsync<Reservation>();
    }

    public async Task UpdateReservationStatusAsync(Guid reservationId, string status)
    {
        var response = await SendJsonAsync("Operations", HttpMethod.Patch, $"/api/operations/reservations/{reservationId}/status", new UpdateReservationStatusRequest(status));
        await EnsureSuccessWithMessageAsync(response);
    }

    public async Task UpdateReservationOrderAsync(Guid reservationId, Guid? orderId)
    {
        var response = await SendJsonAsync(
            "Operations",
            HttpMethod.Patch,
            $"/api/operations/reservations/{reservationId}/order",
            new UpdateReservationOrderRequest(orderId));
        await EnsureSuccessWithMessageAsync(response);
    }

    public async Task AddNotificationAsync(CreateNotificationRequest request)
    {
        var response = await SendJsonAsync("Operations", HttpMethod.Post, "/api/operations/notifications", request);
        response.EnsureSuccessStatusCode();
    }

    public Task<List<Supplier>> GetSuppliersAsync() =>
        GetListAsync<Supplier>("Inventory", "/api/inventory/suppliers");

    public async Task AddSupplierAsync(UpsertSupplierRequest request)
    {
        var response = await SendJsonAsync("Inventory", HttpMethod.Post, "/api/inventory/suppliers", request);
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdateSupplierAsync(Guid supplierId, UpsertSupplierRequest request)
    {
        var response = await SendJsonAsync("Inventory", HttpMethod.Put, $"/api/inventory/suppliers/{supplierId}", request);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteSupplierAsync(Guid supplierId)
    {
        var response = await SendAsync("Inventory", HttpMethod.Delete, $"/api/inventory/suppliers/{supplierId}");
        response.EnsureSuccessStatusCode();
    }

    public Task<List<PurchaseOrder>> GetPurchaseOrdersAsync() =>
        GetListAsync<PurchaseOrder>("Inventory", "/api/inventory/purchase-orders");

    public async Task AddPurchaseOrderAsync(CreatePurchaseOrderRequest request)
    {
        var response = await SendJsonAsync("Inventory", HttpMethod.Post, "/api/inventory/purchase-orders", request);
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdatePurchaseOrderStatusAsync(Guid purchaseOrderId, string status, string operatorName)
    {
        var response = await SendJsonAsync(
            "Inventory",
            HttpMethod.Patch,
            $"/api/inventory/purchase-orders/{purchaseOrderId}/status",
            new UpdatePurchaseOrderStatusRequest(status, operatorName));
        response.EnsureSuccessStatusCode();
    }

    public async Task DeletePurchaseOrderAsync(Guid purchaseOrderId)
    {
        var response = await SendAsync("Inventory", HttpMethod.Delete, $"/api/inventory/purchase-orders/{purchaseOrderId}");
        response.EnsureSuccessStatusCode();
    }

    public async Task<SalesPeriodReport> GetSalesPeriodReportAsync(string fromDate, string toDate) =>
        await GetJsonAsync<SalesPeriodReport>("Sales", $"/api/sales/reports/period?fromDate={fromDate}&toDate={toDate}")
        ?? new SalesPeriodReport(fromDate, toDate, 0, 0, 0, 0, 0, 0, [], []);

    private async Task<List<T>> GetListAsync<T>(string clientName, string url) =>
        await GetJsonAsync<List<T>>(clientName, url) ?? [];

    private async Task DiscountInventoryForOrderAsync(RestaurantOrder? order)
    {
        if (order is null)
        {
            return;
        }

        var menu = await GetMenuAsync();
        foreach (var line in order.Lines)
        {
            var menuItem = line.MenuItemId.HasValue
                ? menu.FirstOrDefault(item => item.ItemId == line.MenuItemId.Value)
                : menu.FirstOrDefault(item => item.Name.Equals(line.ItemName, StringComparison.OrdinalIgnoreCase));

            if (menuItem is null)
            {
                continue;
            }

            foreach (var ingredient in menuItem.Ingredients)
            {
                await AddStockMovementAsync(ingredient.InventoryItemId, new RecordStockMovementRequest(
                    ingredient.Quantity * line.Quantity,
                    "Salida",
                    $"Consumo aproximado por pedido {order.TableOrChannel}",
                    "Sistema"));
            }
        }
    }

    private async Task CreateKitchenTicketForOrderAsync(RestaurantOrder? order)
    {
        if (order is null)
        {
            return;
        }

        await AddTicketAsync(new CreateKitchenTicketRequest(
            "Cocina",
            order.OrderKind.Equals("Delivery", StringComparison.OrdinalIgnoreCase) ? $"Delivery - {order.CustomerName}" : order.TableOrChannel,
            order.OrderId,
            order.Lines.Select(line => $"{line.Quantity}x {line.ItemName}").ToList(),
            order.Notes));
    }

    private Dictionary<string, string> CreateAdminHeaders()
    {
        var role = _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
        return new Dictionary<string, string> { ["X-SushiMiau-Role"] = role };
    }

    private async Task<T?> GetJsonAsync<T>(string clientName, string url, IReadOnlyDictionary<string, string>? headers = null)
    {
        try
        {
            return await PrepareClient(_httpClientFactory.CreateClient(clientName), headers).GetFromJsonAsync<T>(url);
        }
        catch (HttpRequestException ex) when (IsNameResolutionError(ex) && LocalFallbacks.TryGetValue(clientName, out var fallbackBase))
        {
            using var client = PrepareClient(new HttpClient { BaseAddress = fallbackBase }, headers);
            return await client.GetFromJsonAsync<T>(url);
        }
    }

    private async Task<HttpResponseMessage> SendJsonAsync<T>(
        string clientName,
        HttpMethod method,
        string url,
        T body,
        IReadOnlyDictionary<string, string>? headers = null)
    {
        try
        {
            return await PrepareClient(_httpClientFactory.CreateClient(clientName), headers)
                .SendAsync(CreateJsonRequest(method, url, body));
        }
        catch (HttpRequestException ex) when (IsNameResolutionError(ex) && LocalFallbacks.TryGetValue(clientName, out var fallbackBase))
        {
            using var client = PrepareClient(new HttpClient { BaseAddress = fallbackBase }, headers);
            return await client.SendAsync(CreateJsonRequest(method, url, body));
        }
    }

    private async Task<HttpResponseMessage> SendAsync(
        string clientName,
        HttpMethod method,
        string url,
        IReadOnlyDictionary<string, string>? headers = null)
    {
        try
        {
            return await PrepareClient(_httpClientFactory.CreateClient(clientName), headers)
                .SendAsync(new HttpRequestMessage(method, url));
        }
        catch (HttpRequestException ex) when (IsNameResolutionError(ex) && LocalFallbacks.TryGetValue(clientName, out var fallbackBase))
        {
            using var client = PrepareClient(new HttpClient { BaseAddress = fallbackBase }, headers);
            return await client.SendAsync(new HttpRequestMessage(method, url));
        }
    }

    private static HttpClient PrepareClient(HttpClient client, IReadOnlyDictionary<string, string>? headers)
    {
        if (headers is null)
        {
            return client;
        }

        foreach (var header in headers)
        {
            client.DefaultRequestHeaders.Remove(header.Key);
            client.DefaultRequestHeaders.Add(header.Key, header.Value);
        }

        return client;
    }

    private static HttpRequestMessage CreateJsonRequest<T>(HttpMethod method, string url, T body) =>
        new(method, url) { Content = JsonContent.Create(body) };

    private static bool IsNameResolutionError(HttpRequestException ex) =>
        ex.InnerException is SocketException socketException
        && socketException.SocketErrorCode is SocketError.HostNotFound or SocketError.NoData or SocketError.TryAgain;

    private static async Task EnsureSuccessWithMessageAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var error = await response.Content.ReadFromJsonAsync<ApiError>();
        throw new InvalidOperationException(
            string.IsNullOrWhiteSpace(error?.Message)
                ? $"La operacion no pudo completarse ({(int)response.StatusCode})."
                : error.Message);
    }
}

public sealed record ApiError(string Message);

public sealed record RestaurantDashboard(
    InventorySnapshot Inventory,
    IReadOnlyList<InventoryItem> Items,
    IReadOnlyList<MenuItem> Menu,
    IReadOnlyList<KitchenTicket> Tickets,
    IReadOnlyList<StaffShift> Shifts,
    IReadOnlyList<Reservation> Reservations,
    IReadOnlyList<RestaurantOrder> Orders,
    DailySalesSummary Sales);
