using System.Text.Json;
using Cassandra;
using SushiMiau.Shared.Contracts;
using CassandraSession = Cassandra.ISession;

namespace SushiMiau.Operations.Api.Data;

public sealed class OperationsRepository
{
    private const string RestaurantId = "sushi-miau-centro";
    private readonly CassandraSession _session;

    public OperationsRepository(CassandraSession session)
    {
        _session = session;
    }

    public async Task InitializeAsync()
    {
        await _session.ExecuteAsync(new SimpleStatement("""
            CREATE TABLE IF NOT EXISTS restaurant_tables (
                restaurant_id text,
                table_name text,
                capacity int,
                status text,
                assigned_employee text,
                updated_at timestamp,
                PRIMARY KEY (restaurant_id, table_name)
            )
            """));

        await _session.ExecuteAsync(new SimpleStatement("""
            CREATE TABLE IF NOT EXISTS menu_items (
                restaurant_id text,
                item_id uuid,
                name text,
                category text,
                price decimal,
                is_available boolean,
                prep_minutes int,
                ingredients_json text,
                updated_at timestamp,
                PRIMARY KEY (restaurant_id, item_id)
            )
            """));
        await TryExecuteAsync("ALTER TABLE menu_items ADD ingredients_json text");

        await _session.ExecuteAsync(new SimpleStatement("""
            CREATE TABLE IF NOT EXISTS menu_categories (
                restaurant_id text,
                name text,
                PRIMARY KEY (restaurant_id, name)
            )
            """));

        await _session.ExecuteAsync(new SimpleStatement("""
            CREATE TABLE IF NOT EXISTS kitchen_tickets_by_day (
                restaurant_id text,
                business_date text,
                created_at timestamp,
                ticket_id uuid,
                station text,
                table_or_channel text,
                status text,
                items_json text,
                notes text,
                updated_at timestamp,
                PRIMARY KEY ((restaurant_id, business_date), created_at, ticket_id)
            ) WITH CLUSTERING ORDER BY (created_at DESC)
            """));

        await _session.ExecuteAsync(new SimpleStatement("""
            CREATE TABLE IF NOT EXISTS kitchen_tickets_by_id (
                restaurant_id text,
                ticket_id uuid,
                business_date text,
                created_at timestamp,
                station text,
                table_or_channel text,
                status text,
                items_json text,
                notes text,
                updated_at timestamp,
                PRIMARY KEY (restaurant_id, ticket_id)
            )
            """));

        await _session.ExecuteAsync(new SimpleStatement("""
            CREATE TABLE IF NOT EXISTS staff_shifts_by_day (
                restaurant_id text,
                business_date text,
                starts_at timestamp,
                shift_id uuid,
                employee_name text,
                role text,
                shift_name text,
                status text,
                ends_at timestamp,
                PRIMARY KEY ((restaurant_id, business_date), starts_at, shift_id)
            ) WITH CLUSTERING ORDER BY (starts_at ASC)
            """));

        await _session.ExecuteAsync(new SimpleStatement("""
            CREATE TABLE IF NOT EXISTS reservations_by_day (
                restaurant_id text,
                business_date text,
                customer_id uuid,
                reservation_time timestamp,
                reservation_id uuid,
                customer_name text,
                customer_phone text,
                table_name text,
                party_size int,
                status text,
                notes text,
                order_id uuid,
                created_at timestamp,
                updated_at timestamp,
                PRIMARY KEY ((restaurant_id, business_date), reservation_time, reservation_id)
            ) WITH CLUSTERING ORDER BY (reservation_time ASC)
            """));

        await _session.ExecuteAsync(new SimpleStatement("""
            CREATE TABLE IF NOT EXISTS reservations_by_id (
                restaurant_id text,
                reservation_id uuid,
                business_date text,
                customer_id uuid,
                reservation_time timestamp,
                customer_name text,
                customer_phone text,
                table_name text,
                party_size int,
                status text,
                notes text,
                order_id uuid,
                created_at timestamp,
                updated_at timestamp,
                PRIMARY KEY (restaurant_id, reservation_id)
            )
            """));

        await TryExecuteAsync("ALTER TABLE reservations_by_day ADD customer_id uuid");
        await TryExecuteAsync("ALTER TABLE reservations_by_day ADD order_id uuid");
        await TryExecuteAsync("ALTER TABLE reservations_by_day ADD table_name text");
        await TryExecuteAsync("ALTER TABLE reservations_by_id ADD customer_id uuid");
        await TryExecuteAsync("ALTER TABLE reservations_by_id ADD order_id uuid");
        await TryExecuteAsync("ALTER TABLE reservations_by_id ADD table_name text");

        await _session.ExecuteAsync(new SimpleStatement("""
            CREATE TABLE IF NOT EXISTS customers (
                restaurant_id text,
                customer_id uuid,
                name text,
                phone text,
                nit text,
                created_at timestamp,
                updated_at timestamp,
                PRIMARY KEY (restaurant_id, customer_id)
            )
            """));

        await _session.ExecuteAsync(new SimpleStatement("""
            CREATE TABLE IF NOT EXISTS notifications_by_role (
                restaurant_id text,
                audience_role text,
                created_at timestamp,
                notification_id uuid,
                title text,
                message text,
                severity text,
                is_read boolean,
                PRIMARY KEY ((restaurant_id, audience_role), created_at, notification_id)
            ) WITH CLUSTERING ORDER BY (created_at DESC)
            """));

        await SeedTablesAsync();

        if ((await GetMenuAsync()).Count == 0)
        {
            var menuSeed = new[]
            {
                new UpsertMenuItemRequest("Miau Roll", "Rolls", 48m, true, 12),
                new UpsertMenuItemRequest("Nigiri salmon", "Nigiri", 32m, true, 8),
                new UpsertMenuItemRequest("Tempura ebi", "Calientes", 42m, true, 14),
                new UpsertMenuItemRequest("Combo itamae", "Combos", 118m, true, 18),
                new UpsertMenuItemRequest("Mochi matcha", "Postres", 24m, true, 5)
            };

            foreach (var item in menuSeed)
            {
                await UpsertMenuItemAsync(null, item);
            }

            foreach (var category in menuSeed.Select(item => item.Category).Distinct())
            {
                await CreateMenuCategoryAsync(category);
            }

            var customer = await CreateCustomerAsync(new CreateCustomerRequest("Laura Medina", "76543210", "0"));

            await CreateTicketAsync(new CreateKitchenTicketRequest(
                "Sushi bar",
                "Mesa 4",
                ["2x Miau Roll", "1x Nigiri salmon"],
                "Sin ajonjoli en un roll"));

            await CreateShiftAsync(new CreateStaffShiftRequest(
                "Camila Rojas",
                "Jefa de sala",
                "Cena",
                "Activo",
                DateTimeOffset.UtcNow.Date.AddHours(18),
                DateTimeOffset.UtcNow.Date.AddHours(23)));

            await CreateReservationAsync(new CreateReservationRequest(
                customer.CustomerId,
                customer.Name,
                customer.Phone,
                "Mesa 4",
                4,
                DateTimeOffset.UtcNow.Date.AddHours(20),
                "Mesa cerca de barra"));

            await CreateNotificationAsync(new CreateNotificationRequest(
                AppRoles.Manager,
                "Palta bajo minimo",
                "Inventario marco palta Hass bajo minimo. Revisar compra antes del turno cena.",
                "Media"));
        }
    }

    public async Task<IReadOnlyList<MenuItem>> GetMenuAsync()
    {
        var rows = await _session.ExecuteAsync(new SimpleStatement(
            "SELECT item_id, name, category, price, is_available, prep_minutes, ingredients_json, updated_at FROM menu_items WHERE restaurant_id = ?",
            RestaurantId));

        return rows.Select(MapMenuItem)
            .OrderBy(item => item.Category)
            .ThenBy(item => item.Name)
            .ToList();
    }

    public async Task<IReadOnlyList<RestaurantTable>> GetTablesAsync()
    {
        var rows = await _session.ExecuteAsync(new SimpleStatement("""
            SELECT table_name, capacity, status, assigned_employee, updated_at
            FROM restaurant_tables
            WHERE restaurant_id = ?
            """, RestaurantId));

        return rows.Select(MapTable)
            .OrderBy(table => TableSortKey(table.TableName))
            .ToList();
    }

    public async Task<RestaurantTable> UpdateTableStateAsync(string tableName, UpdateTableStateRequest request)
    {
        var existing = (await GetTablesAsync()).FirstOrDefault(table => table.TableName.Equals(tableName, StringComparison.OrdinalIgnoreCase));
        var updated = new RestaurantTable(
            existing?.TableName ?? tableName.Trim(),
            existing?.Capacity ?? 4,
            string.IsNullOrWhiteSpace(request.Status) ? "Disponible" : request.Status.Trim(),
            string.IsNullOrWhiteSpace(request.AssignedEmployee) ? string.Empty : request.AssignedEmployee.Trim(),
            DateTimeOffset.UtcNow);

        await SaveTableAsync(updated);
        return updated;
    }

    private async Task SeedTablesAsync()
    {
        if ((await GetTablesAsync()).Count > 0)
        {
            return;
        }

        var seed = new[]
        {
            new RestaurantTable("Mesa 1", 2, "Disponible", "", DateTimeOffset.UtcNow),
            new RestaurantTable("Mesa 2", 2, "Disponible", "", DateTimeOffset.UtcNow),
            new RestaurantTable("Mesa 3", 4, "Disponible", "", DateTimeOffset.UtcNow),
            new RestaurantTable("Mesa 4", 4, "Disponible", "", DateTimeOffset.UtcNow),
            new RestaurantTable("Mesa 5", 4, "Disponible", "", DateTimeOffset.UtcNow),
            new RestaurantTable("Mesa 6", 6, "Disponible", "", DateTimeOffset.UtcNow),
            new RestaurantTable("Mesa 7", 6, "Disponible", "", DateTimeOffset.UtcNow),
            new RestaurantTable("Mesa 8", 8, "Disponible", "", DateTimeOffset.UtcNow),
            new RestaurantTable("Barra 1", 2, "Disponible", "", DateTimeOffset.UtcNow),
            new RestaurantTable("Barra 2", 2, "Fuera de servicio", "", DateTimeOffset.UtcNow)
        };

        foreach (var table in seed)
        {
            await SaveTableAsync(table);
        }
    }

    private async Task SaveTableAsync(RestaurantTable table)
    {
        await _session.ExecuteAsync(new SimpleStatement("""
            INSERT INTO restaurant_tables (restaurant_id, table_name, capacity, status, assigned_employee, updated_at)
            VALUES (?, ?, ?, ?, ?, ?)
            """,
            RestaurantId,
            table.TableName,
            table.Capacity,
            table.Status,
            table.AssignedEmployee,
            table.UpdatedAt));
    }

    public async Task<MenuItem> UpsertMenuItemAsync(Guid? itemId, UpsertMenuItemRequest request)
    {
        var now = DateTimeOffset.UtcNow;
        var id = itemId ?? Guid.NewGuid();

        await _session.ExecuteAsync(new SimpleStatement("""
            INSERT INTO menu_items (restaurant_id, item_id, name, category, price, is_available, prep_minutes, ingredients_json, updated_at)
            VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)
            """,
            RestaurantId,
            id,
            request.Name.Trim(),
            request.Category.Trim(),
            request.Price,
            request.IsAvailable,
            request.PrepMinutes,
            JsonSerializer.Serialize(request.Ingredients ?? []),
            now));

        return new MenuItem(
            id,
            request.Name.Trim(),
            request.Category.Trim(),
            request.Price,
            request.IsAvailable,
            request.PrepMinutes,
            now,
            request.Ingredients ?? []);
    }

    public async Task DeleteMenuItemAsync(Guid itemId)
    {
        await _session.ExecuteAsync(new SimpleStatement(
            "DELETE FROM menu_items WHERE restaurant_id = ? AND item_id = ?",
            RestaurantId,
            itemId));
    }

    public async Task<IReadOnlyList<MenuCategory>> GetMenuCategoriesAsync()
    {
        var rows = await _session.ExecuteAsync(new SimpleStatement(
            "SELECT name FROM menu_categories WHERE restaurant_id = ?",
            RestaurantId));

        return rows.Select(row => new MenuCategory(row.GetValue<string>("name"))).OrderBy(item => item.Name).ToList();
    }

    public async Task<MenuCategory> CreateMenuCategoryAsync(string name)
    {
        var category = new MenuCategory(name.Trim());
        await _session.ExecuteAsync(new SimpleStatement(
            "INSERT INTO menu_categories (restaurant_id, name) VALUES (?, ?)",
            RestaurantId,
            category.Name));

        return category;
    }

    public async Task<IReadOnlyList<KitchenTicket>> GetTicketsAsync(string businessDate)
    {
        var rows = await _session.ExecuteAsync(new SimpleStatement("""
            SELECT ticket_id, business_date, station, table_or_channel, status, items_json, notes, created_at, updated_at
            FROM kitchen_tickets_by_day
            WHERE restaurant_id = ? AND business_date = ?
            """,
            RestaurantId,
            businessDate));

        return rows.Select(MapTicket).ToList();
    }

    public async Task<KitchenTicket?> GetTicketAsync(Guid ticketId)
    {
        var rows = await _session.ExecuteAsync(new SimpleStatement("""
            SELECT ticket_id, business_date, station, table_or_channel, status, items_json, notes, created_at, updated_at
            FROM kitchen_tickets_by_id
            WHERE restaurant_id = ? AND ticket_id = ?
            """,
            RestaurantId,
            ticketId));

        return rows.Select(MapTicket).FirstOrDefault();
    }

    public async Task<KitchenTicket> CreateTicketAsync(CreateKitchenTicketRequest request)
    {
        var now = DateTimeOffset.UtcNow;
        var ticket = new KitchenTicket(
            Guid.NewGuid(),
            DateOnly.FromDateTime(now.UtcDateTime).ToString("yyyy-MM-dd"),
            request.Station.Trim(),
            request.TableOrChannel.Trim(),
            "Pendiente",
            request.Items.Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => item.Trim()).ToList(),
            request.Notes.Trim(),
            now,
            now);

        await SaveTicketAsync(ticket);
        return ticket;
    }

    public async Task<KitchenTicket?> UpdateTicketStatusAsync(Guid ticketId, string status)
    {
        var ticket = await GetTicketAsync(ticketId);
        if (ticket is null)
        {
            return null;
        }

        var updated = ticket with
        {
            Status = string.IsNullOrWhiteSpace(status) ? ticket.Status : status.Trim(),
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await SaveTicketAsync(updated);
        return updated;
    }

    public async Task<IReadOnlyList<StaffShift>> GetShiftsAsync(string businessDate)
    {
        var rows = await _session.ExecuteAsync(new SimpleStatement("""
            SELECT shift_id, business_date, employee_name, role, shift_name, status, starts_at, ends_at
            FROM staff_shifts_by_day
            WHERE restaurant_id = ? AND business_date = ?
            """,
            RestaurantId,
            businessDate));

        return rows.Select(MapShift).ToList();
    }

    public async Task<StaffShift> CreateShiftAsync(CreateStaffShiftRequest request)
    {
        var shift = new StaffShift(
            Guid.NewGuid(),
            DateOnly.FromDateTime(request.StartsAt.UtcDateTime).ToString("yyyy-MM-dd"),
            request.EmployeeName.Trim(),
            request.Role.Trim(),
            request.ShiftName.Trim(),
            request.Status.Trim(),
            request.StartsAt.ToUniversalTime(),
            request.EndsAt.ToUniversalTime());

        await _session.ExecuteAsync(new SimpleStatement("""
            INSERT INTO staff_shifts_by_day
            (restaurant_id, business_date, starts_at, shift_id, employee_name, role, shift_name, status, ends_at)
            VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)
            """,
            RestaurantId,
            shift.BusinessDate,
            shift.StartsAt,
            shift.ShiftId,
            shift.EmployeeName,
            shift.Role,
            shift.ShiftName,
            shift.Status,
            shift.EndsAt));

        return shift;
    }

    public async Task<IReadOnlyList<Reservation>> GetReservationsAsync(string businessDate)
    {
        var rows = await _session.ExecuteAsync(new SimpleStatement("""
            SELECT reservation_id, business_date, customer_id, customer_name, customer_phone, table_name, party_size, reservation_time,
                   status, notes, order_id, created_at, updated_at
            FROM reservations_by_day
            WHERE restaurant_id = ? AND business_date = ?
            """, RestaurantId, businessDate));

        return rows.Select(MapReservation).ToList();
    }

    public async Task<Reservation?> GetReservationAsync(Guid reservationId)
    {
        var rows = await _session.ExecuteAsync(new SimpleStatement("""
            SELECT reservation_id, business_date, customer_id, customer_name, customer_phone, table_name, party_size, reservation_time,
                   status, notes, order_id, created_at, updated_at
            FROM reservations_by_id
            WHERE restaurant_id = ? AND reservation_id = ?
            """, RestaurantId, reservationId));

        return rows.Select(MapReservation).FirstOrDefault();
    }

    public async Task<Reservation> CreateReservationAsync(CreateReservationRequest request)
    {
        var now = DateTimeOffset.UtcNow;
        var reservationTime = request.ReservationTime.ToUniversalTime();
        var reservation = new Reservation(
            Guid.NewGuid(),
            DateOnly.FromDateTime(reservationTime.UtcDateTime).ToString("yyyy-MM-dd"),
            request.CustomerId,
            request.CustomerName.Trim(),
            request.CustomerPhone.Trim(),
            request.TableName.Trim(),
            request.PartySize,
            reservationTime,
            "Pendiente",
            request.Notes.Trim(),
            request.OrderId,
            now,
            now);

        await SaveReservationAsync(reservation);
        return reservation;
    }

    public async Task<Reservation?> UpdateReservationStatusAsync(Guid reservationId, string status)
    {
        var reservation = await GetReservationAsync(reservationId);
        if (reservation is null)
        {
            return null;
        }

        var updated = reservation with
        {
            Status = string.IsNullOrWhiteSpace(status) ? reservation.Status : status.Trim(),
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await SaveReservationAsync(updated);
        return updated;
    }

    public async Task<IReadOnlyList<NotificationMessage>> GetNotificationsAsync(string audienceRole)
    {
        var rows = await _session.ExecuteAsync(new SimpleStatement("""
            SELECT notification_id, audience_role, title, message, severity, is_read, created_at
            FROM notifications_by_role
            WHERE restaurant_id = ? AND audience_role = ?
            """, RestaurantId, audienceRole));

        return rows.Select(MapNotification).ToList();
    }

    public async Task<NotificationMessage> CreateNotificationAsync(CreateNotificationRequest request)
    {
        var notification = new NotificationMessage(
            Guid.NewGuid(),
            string.IsNullOrWhiteSpace(request.AudienceRole) ? AppRoles.Manager : request.AudienceRole.Trim(),
            request.Title.Trim(),
            request.Message.Trim(),
            string.IsNullOrWhiteSpace(request.Severity) ? "Baja" : request.Severity.Trim(),
            false,
            DateTimeOffset.UtcNow);

        await _session.ExecuteAsync(new SimpleStatement("""
            INSERT INTO notifications_by_role
            (restaurant_id, audience_role, created_at, notification_id, title, message, severity, is_read)
            VALUES (?, ?, ?, ?, ?, ?, ?, ?)
            """,
            RestaurantId,
            notification.AudienceRole,
            notification.CreatedAt,
            notification.NotificationId,
            notification.Title,
            notification.Message,
            notification.Severity,
            notification.IsRead));

        return notification;
    }

    public async Task<IReadOnlyList<Customer>> GetCustomersAsync()
    {
        var rows = await _session.ExecuteAsync(new SimpleStatement("""
            SELECT customer_id, name, phone, nit, created_at, updated_at
            FROM customers
            WHERE restaurant_id = ?
            """, RestaurantId));

        return rows.Select(MapCustomer).OrderBy(customer => customer.Name).ToList();
    }

    public async Task<Customer> CreateCustomerAsync(CreateCustomerRequest request)
    {
        var now = DateTimeOffset.UtcNow;
        var customer = new Customer(
            Guid.NewGuid(),
            request.Name.Trim(),
            request.Phone.Trim(),
            string.IsNullOrWhiteSpace(request.Nit) ? "0" : request.Nit.Trim(),
            now,
            now);

        await _session.ExecuteAsync(new SimpleStatement("""
            INSERT INTO customers (restaurant_id, customer_id, name, phone, nit, created_at, updated_at)
            VALUES (?, ?, ?, ?, ?, ?, ?)
            """,
            RestaurantId,
            customer.CustomerId,
            customer.Name,
            customer.Phone,
            customer.Nit,
            customer.CreatedAt,
            customer.UpdatedAt));

        return customer;
    }

    public async Task<Customer?> UpdateCustomerAsync(Guid customerId, UpdateCustomerRequest request)
    {
        var existing = (await GetCustomersAsync()).FirstOrDefault(customer => customer.CustomerId == customerId);
        if (existing is null)
        {
            return null;
        }

        var updated = existing with
        {
            Name = request.Name.Trim(),
            Phone = request.Phone.Trim(),
            Nit = string.IsNullOrWhiteSpace(request.Nit) ? "0" : request.Nit.Trim(),
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await _session.ExecuteAsync(new SimpleStatement("""
            INSERT INTO customers (restaurant_id, customer_id, name, phone, nit, created_at, updated_at)
            VALUES (?, ?, ?, ?, ?, ?, ?)
            """,
            RestaurantId,
            updated.CustomerId,
            updated.Name,
            updated.Phone,
            updated.Nit,
            updated.CreatedAt,
            updated.UpdatedAt));

        return updated;
    }

    public async Task DeleteCustomerAsync(Guid customerId)
    {
        await _session.ExecuteAsync(new SimpleStatement(
            "DELETE FROM customers WHERE restaurant_id = ? AND customer_id = ?",
            RestaurantId,
            customerId));
    }

    private async Task SaveTicketAsync(KitchenTicket ticket)
    {
        var itemsJson = JsonSerializer.Serialize(ticket.Items);

        await _session.ExecuteAsync(new SimpleStatement("""
            INSERT INTO kitchen_tickets_by_day
            (restaurant_id, business_date, created_at, ticket_id, station, table_or_channel, status, items_json, notes, updated_at)
            VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
            """,
            RestaurantId,
            ticket.BusinessDate,
            ticket.CreatedAt,
            ticket.TicketId,
            ticket.Station,
            ticket.TableOrChannel,
            ticket.Status,
            itemsJson,
            ticket.Notes,
            ticket.UpdatedAt));

        await _session.ExecuteAsync(new SimpleStatement("""
            INSERT INTO kitchen_tickets_by_id
            (restaurant_id, ticket_id, business_date, created_at, station, table_or_channel, status, items_json, notes, updated_at)
            VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
            """,
            RestaurantId,
            ticket.TicketId,
            ticket.BusinessDate,
            ticket.CreatedAt,
            ticket.Station,
            ticket.TableOrChannel,
            ticket.Status,
            itemsJson,
            ticket.Notes,
            ticket.UpdatedAt));
    }

    private async Task SaveReservationAsync(Reservation reservation)
    {
        await _session.ExecuteAsync(new SimpleStatement("""
            INSERT INTO reservations_by_day
            (restaurant_id, business_date, reservation_time, reservation_id, customer_id, customer_name, customer_phone,
             table_name, party_size, status, notes, order_id, created_at, updated_at)
            VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
            """,
            RestaurantId,
            reservation.BusinessDate,
            reservation.ReservationTime,
            reservation.ReservationId,
            reservation.CustomerId,
            reservation.CustomerName,
            reservation.CustomerPhone,
            reservation.TableName,
            reservation.PartySize,
            reservation.Status,
            reservation.Notes,
            reservation.OrderId,
            reservation.CreatedAt,
            reservation.UpdatedAt));

        await _session.ExecuteAsync(new SimpleStatement("""
            INSERT INTO reservations_by_id
            (restaurant_id, reservation_id, business_date, reservation_time, customer_id, customer_name, customer_phone,
             table_name, party_size, status, notes, order_id, created_at, updated_at)
            VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
            """,
            RestaurantId,
            reservation.ReservationId,
            reservation.BusinessDate,
            reservation.ReservationTime,
            reservation.CustomerId,
            reservation.CustomerName,
            reservation.CustomerPhone,
            reservation.TableName,
            reservation.PartySize,
            reservation.Status,
            reservation.Notes,
            reservation.OrderId,
            reservation.CreatedAt,
            reservation.UpdatedAt));
    }

    private static MenuItem MapMenuItem(Row row)
    {
        var updatedAt = row.GetValue<DateTimeOffset>("updated_at");
        var ingredientsJson = row.GetValue<string>("ingredients_json") ?? "[]";
        var ingredients = JsonSerializer.Deserialize<List<MenuIngredient>>(ingredientsJson) ?? [];

        return new MenuItem(
            row.GetValue<Guid>("item_id"),
            row.GetValue<string>("name"),
            row.GetValue<string>("category"),
            row.GetValue<decimal>("price"),
            row.GetValue<bool>("is_available"),
            row.GetValue<int>("prep_minutes"),
            updatedAt,
            ingredients);
    }

    private static KitchenTicket MapTicket(Row row)
    {
        var createdAt = row.GetValue<DateTimeOffset>("created_at");
        var updatedAt = row.GetValue<DateTimeOffset>("updated_at");
        var items = JsonSerializer.Deserialize<List<string>>(row.GetValue<string>("items_json")) ?? [];

        return new KitchenTicket(
            row.GetValue<Guid>("ticket_id"),
            row.GetValue<string>("business_date"),
            row.GetValue<string>("station"),
            row.GetValue<string>("table_or_channel"),
            row.GetValue<string>("status"),
            items,
            row.GetValue<string>("notes"),
            createdAt,
            updatedAt);
    }

    private static StaffShift MapShift(Row row)
    {
        var startsAt = row.GetValue<DateTimeOffset>("starts_at");
        var endsAt = row.GetValue<DateTimeOffset>("ends_at");

        return new StaffShift(
            row.GetValue<Guid>("shift_id"),
            row.GetValue<string>("business_date"),
            row.GetValue<string>("employee_name"),
            row.GetValue<string>("role"),
            row.GetValue<string>("shift_name"),
            row.GetValue<string>("status"),
            startsAt,
            endsAt);
    }

    private static Reservation MapReservation(Row row)
    {
        return new Reservation(
            row.GetValue<Guid>("reservation_id"),
            row.GetValue<string>("business_date"),
            GetOptionalGuid(row, "customer_id"),
            row.GetValue<string>("customer_name"),
            row.GetValue<string>("customer_phone"),
            GetOptionalString(row, "table_name", string.Empty),
            row.GetValue<int>("party_size"),
            row.GetValue<DateTimeOffset>("reservation_time"),
            row.GetValue<string>("status"),
            row.GetValue<string>("notes"),
            GetNullableGuid(row, "order_id"),
            row.GetValue<DateTimeOffset>("created_at"),
            row.GetValue<DateTimeOffset>("updated_at"));
    }

    private static RestaurantTable MapTable(Row row)
    {
        return new RestaurantTable(
            row.GetValue<string>("table_name"),
            row.GetValue<int>("capacity"),
            row.GetValue<string>("status"),
            row.GetValue<string>("assigned_employee") ?? string.Empty,
            row.GetValue<DateTimeOffset>("updated_at"));
    }

    private static NotificationMessage MapNotification(Row row)
    {
        return new NotificationMessage(
            row.GetValue<Guid>("notification_id"),
            row.GetValue<string>("audience_role"),
            row.GetValue<string>("title"),
            row.GetValue<string>("message"),
            row.GetValue<string>("severity"),
            row.GetValue<bool>("is_read"),
            row.GetValue<DateTimeOffset>("created_at"));
    }

    private static Customer MapCustomer(Row row)
    {
        return new Customer(
            row.GetValue<Guid>("customer_id"),
            row.GetValue<string>("name"),
            row.GetValue<string>("phone"),
            row.GetValue<string>("nit"),
            row.GetValue<DateTimeOffset>("created_at"),
            row.GetValue<DateTimeOffset>("updated_at"));
    }

    private async Task TryExecuteAsync(string cql)
    {
        try
        {
            await _session.ExecuteAsync(new SimpleStatement(cql));
        }
        catch (InvalidQueryException)
        {
        }
    }

    private static Guid GetOptionalGuid(Row row, string column)
    {
        try
        {
            return row.GetValue<Guid>(column);
        }
        catch
        {
            return Guid.Empty;
        }
    }

    private static Guid? GetNullableGuid(Row row, string column)
    {
        try
        {
            return row.GetValue<Guid?>(column);
        }
        catch
        {
            return null;
        }
    }

    private static string GetOptionalString(Row row, string column, string fallback)
    {
        try
        {
            return row.GetValue<string>(column) ?? fallback;
        }
        catch
        {
            return fallback;
        }
    }

    private static int TableSortKey(string tableName)
    {
        var digits = new string(tableName.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out var result) ? result : 1000;
    }
}
