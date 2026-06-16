using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Claims;
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
        var today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");

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

    public Task<List<Customer>> GetCustomersAsync() =>
        GetListAsync<Customer>("Operations", "/api/operations/customers");

    public async Task<Customer?> AddCustomerAsync(CreateCustomerRequest request)
    {
        var response = await SendJsonAsync("Operations", HttpMethod.Post, "/api/operations/customers", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Customer>();
    }

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

    public async Task<RestaurantOrder?> AddOrderAsync(CreateOrderRequest request)
    {
        var response = await SendJsonAsync("Sales", HttpMethod.Post, "/api/sales/orders", request);
        response.EnsureSuccessStatusCode();
        var order = await response.Content.ReadFromJsonAsync<RestaurantOrder>();
        await DiscountInventoryForOrderAsync(order);
        return order;
    }

    public async Task<RestaurantOrder?> AddDeliveryOrderAsync(CreateDeliveryOrderRequest request)
    {
        var response = await SendJsonAsync("Sales", HttpMethod.Post, "/api/sales/delivery-orders", request);
        response.EnsureSuccessStatusCode();
        var order = await response.Content.ReadFromJsonAsync<RestaurantOrder>();
        await DiscountInventoryForOrderAsync(order);
        return order;
    }

    public async Task UpdateDeliveryStatusAsync(Guid orderId, string status)
    {
        var response = await SendJsonAsync("Sales", HttpMethod.Patch, $"/api/sales/delivery-orders/{orderId}/status", new UpdateDeliveryStatusRequest(status));
        response.EnsureSuccessStatusCode();
    }

    public async Task RegisterPaymentAsync(Guid orderId, string paymentMethod, string billingName = "Consumidor Final", string taxId = "0")
    {
        var response = await SendJsonAsync("Sales", HttpMethod.Patch, $"/api/sales/orders/{orderId}/pay", new RegisterPaymentRequest(paymentMethod, billingName, taxId));
        response.EnsureSuccessStatusCode();
    }

    public async Task AddReservationAsync(CreateReservationRequest request)
    {
        var response = await SendJsonAsync("Operations", HttpMethod.Post, "/api/operations/reservations", request);
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdateReservationStatusAsync(Guid reservationId, string status)
    {
        var response = await SendJsonAsync("Operations", HttpMethod.Patch, $"/api/operations/reservations/{reservationId}/status", new UpdateReservationStatusRequest(status));
        response.EnsureSuccessStatusCode();
    }

    public async Task AddNotificationAsync(CreateNotificationRequest request)
    {
        var response = await SendJsonAsync("Operations", HttpMethod.Post, "/api/operations/notifications", request);
        response.EnsureSuccessStatusCode();
    }

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
}

public sealed record RestaurantDashboard(
    InventorySnapshot Inventory,
    IReadOnlyList<InventoryItem> Items,
    IReadOnlyList<MenuItem> Menu,
    IReadOnlyList<KitchenTicket> Tickets,
    IReadOnlyList<StaffShift> Shifts,
    IReadOnlyList<Reservation> Reservations,
    IReadOnlyList<RestaurantOrder> Orders,
    DailySalesSummary Sales);
