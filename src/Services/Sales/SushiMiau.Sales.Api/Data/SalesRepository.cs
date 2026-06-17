using System.Text.Json;
using Cassandra;
using SushiMiau.Shared.Contracts;
using CassandraSession = Cassandra.ISession;

namespace SushiMiau.Sales.Api.Data;

public sealed class SalesRepository
{
    private const string RestaurantId = "sushi-miau-centro";
    private const decimal TaxRate = 0.13m;
    private readonly CassandraSession _session;

    public SalesRepository(CassandraSession session)
    {
        _session = session;
    }

    public async Task InitializeAsync()
    {
        await _session.ExecuteAsync(new SimpleStatement("""
            CREATE TABLE IF NOT EXISTS orders_by_day (
                restaurant_id text,
                business_date text,
                created_at timestamp,
                order_id uuid,
                order_kind text,
                table_or_channel text,
                customer_name text,
                customer_phone text,
                delivery_address text,
                delivery_status text,
                server_name text,
                status text,
                payment_method text,
                lines_json text,
                subtotal decimal,
                tax decimal,
                total decimal,
                updated_at timestamp,
                PRIMARY KEY ((restaurant_id, business_date), created_at, order_id)
            ) WITH CLUSTERING ORDER BY (created_at DESC)
            """));
        await TryExecuteAsync("ALTER TABLE orders_by_day ADD order_kind text");
        await TryExecuteAsync("ALTER TABLE orders_by_day ADD customer_name text");
        await TryExecuteAsync("ALTER TABLE orders_by_day ADD customer_phone text");
        await TryExecuteAsync("ALTER TABLE orders_by_day ADD delivery_address text");
        await TryExecuteAsync("ALTER TABLE orders_by_day ADD delivery_status text");

        await _session.ExecuteAsync(new SimpleStatement("""
            CREATE TABLE IF NOT EXISTS orders_by_id (
                restaurant_id text,
                order_id uuid,
                business_date text,
                created_at timestamp,
                order_kind text,
                table_or_channel text,
                customer_name text,
                customer_phone text,
                delivery_address text,
                delivery_status text,
                server_name text,
                status text,
                payment_method text,
                lines_json text,
                subtotal decimal,
                tax decimal,
                total decimal,
                updated_at timestamp,
                PRIMARY KEY (restaurant_id, order_id)
            )
            """));
        await TryExecuteAsync("ALTER TABLE orders_by_id ADD order_kind text");
        await TryExecuteAsync("ALTER TABLE orders_by_id ADD customer_name text");
        await TryExecuteAsync("ALTER TABLE orders_by_id ADD customer_phone text");
        await TryExecuteAsync("ALTER TABLE orders_by_id ADD delivery_address text");
        await TryExecuteAsync("ALTER TABLE orders_by_id ADD delivery_status text");

        await _session.ExecuteAsync(new SimpleStatement("""
            CREATE TABLE IF NOT EXISTS payments_by_day (
                restaurant_id text,
                business_date text,
                paid_at timestamp,
                payment_id uuid,
                order_id uuid,
                payment_method text,
                amount decimal,
                billing_name text,
                tax_id text,
                PRIMARY KEY ((restaurant_id, business_date), paid_at, payment_id)
            ) WITH CLUSTERING ORDER BY (paid_at DESC)
            """));

        await _session.ExecuteAsync(new SimpleStatement("""
            CREATE TABLE IF NOT EXISTS invoices_by_day (
                restaurant_id text,
                business_date text,
                issued_at timestamp,
                invoice_id uuid,
                order_id uuid,
                invoice_number text,
                billing_name text,
                tax_id text,
                subtotal decimal,
                tax decimal,
                total decimal,
                PRIMARY KEY ((restaurant_id, business_date), issued_at, invoice_id)
            ) WITH CLUSTERING ORDER BY (issued_at DESC)
            """));

        var today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
        if ((await GetOrdersAsync(today)).Count == 0)
        {
            var order = await CreateOrderAsync(new CreateOrderRequest(
                "Mesa 4",
                "Diego",
                [
                    new OrderLine("Miau Roll", 2, 48m),
                    new OrderLine("Nigiri salmon", 1, 32m),
                    new OrderLine("Limonada yuzu", 2, 18m)
                ]));

            await RegisterPaymentAsync(order.OrderId, new RegisterPaymentRequest("Tarjeta", "Sushi Miau Demo", "1234567"));

            await CreateDeliveryOrderAsync(new CreateDeliveryOrderRequest(
                "Rafael Quiroga",
                "71234567",
                "Av. America #420",
                "Lucia",
                [
                    new OrderLine("Combo itamae", 1, 118m),
                    new OrderLine("Mochi matcha", 2, 24m)
                ]));
        }
    }

    public async Task<IReadOnlyList<RestaurantOrder>> GetOrdersAsync(string businessDate)
    {
        var rows = await _session.ExecuteAsync(new SimpleStatement("""
            SELECT order_id, business_date, order_kind, table_or_channel, customer_name, customer_phone,
                   delivery_address, delivery_status, server_name, status, payment_method, lines_json,
                   subtotal, tax, total, created_at, updated_at
            FROM orders_by_day
            WHERE restaurant_id = ? AND business_date = ?
            """,
            RestaurantId,
            businessDate));

        return rows.Select(MapOrder).ToList();
    }

    public async Task<RestaurantOrder?> GetOrderAsync(Guid orderId)
    {
        var rows = await _session.ExecuteAsync(new SimpleStatement("""
            SELECT order_id, business_date, order_kind, table_or_channel, customer_name, customer_phone,
                   delivery_address, delivery_status, server_name, status, payment_method, lines_json,
                   subtotal, tax, total, created_at, updated_at
            FROM orders_by_id
            WHERE restaurant_id = ? AND order_id = ?
            """,
            RestaurantId,
            orderId));

        return rows.Select(MapOrder).FirstOrDefault();
    }

    public async Task<RestaurantOrder> CreateOrderAsync(CreateOrderRequest request)
    {
        var lines = request.Lines
            .Where(line => !string.IsNullOrWhiteSpace(line.ItemName) && line.Quantity > 0)
            .Select(line => line with { ItemName = line.ItemName.Trim() })
            .ToList();

        var subtotal = lines.Sum(line => line.Quantity * line.UnitPrice);
        var tax = Math.Round(subtotal * TaxRate, 2);
        var total = subtotal + tax;
        var now = DateTimeOffset.UtcNow;

        var order = new RestaurantOrder(
            Guid.NewGuid(),
            DateOnly.FromDateTime(now.UtcDateTime).ToString("yyyy-MM-dd"),
            request.TableOrChannel.Trim(),
            request.ServerName.Trim(),
            "Preparando",
            "Pendiente",
            lines,
            subtotal,
            tax,
            total,
            now,
            now,
            request.OrderKind,
            request.CustomerName.Trim(),
            request.CustomerPhone.Trim(),
            request.DeliveryAddress.Trim(),
            request.OrderKind.Equals("Delivery", StringComparison.OrdinalIgnoreCase) ? "Preparando" : "");

        await SaveOrderAsync(order);
        return order;
    }

    public Task<RestaurantOrder> CreateDeliveryOrderAsync(CreateDeliveryOrderRequest request)
    {
        return CreateOrderAsync(new CreateOrderRequest(
            "Delivery",
            request.ServerName,
            request.Lines,
            "Delivery",
            request.CustomerName,
            request.CustomerPhone,
            request.DeliveryAddress));
    }

    public async Task<RestaurantOrder?> UpdateDeliveryStatusAsync(Guid orderId, string deliveryStatus)
    {
        var order = await GetOrderAsync(orderId);
        if (order is null)
        {
            return null;
        }

        var updated = order with
        {
            DeliveryStatus = string.IsNullOrWhiteSpace(deliveryStatus) ? order.DeliveryStatus : deliveryStatus.Trim(),
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await SaveOrderAsync(updated);
        return updated;
    }

    public async Task<RestaurantOrder?> UpdateOrderStatusAsync(Guid orderId, string status)
    {
        var order = await GetOrderAsync(orderId);
        if (order is null)
        {
            return null;
        }

        var updated = order with
        {
            Status = string.IsNullOrWhiteSpace(status) ? order.Status : status.Trim(),
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await SaveOrderAsync(updated);
        return updated;
    }

    public async Task<RestaurantOrder?> UpdateOrderAsync(Guid orderId, UpdateOrderRequest request)
    {
        var order = await GetOrderAsync(orderId);
        if (order is null)
        {
            return null;
        }

        var lines = request.Lines
            .Where(line => !string.IsNullOrWhiteSpace(line.ItemName) && line.Quantity > 0)
            .Select(line => line with { ItemName = line.ItemName.Trim() })
            .ToList();
        var subtotal = lines.Sum(line => line.Quantity * line.UnitPrice);
        var tax = Math.Round(subtotal * TaxRate, 2);
        var total = subtotal + tax;

        var updated = order with
        {
            TableOrChannel = string.IsNullOrWhiteSpace(request.TableOrChannel) ? order.TableOrChannel : request.TableOrChannel.Trim(),
            ServerName = string.IsNullOrWhiteSpace(request.ServerName) ? order.ServerName : request.ServerName.Trim(),
            Status = string.IsNullOrWhiteSpace(request.Status) ? order.Status : request.Status.Trim(),
            Lines = lines.Count == 0 ? order.Lines : lines,
            Subtotal = lines.Count == 0 ? order.Subtotal : subtotal,
            Tax = lines.Count == 0 ? order.Tax : tax,
            Total = lines.Count == 0 ? order.Total : total,
            CustomerName = request.CustomerName.Trim(),
            CustomerPhone = request.CustomerPhone.Trim(),
            DeliveryAddress = request.DeliveryAddress.Trim(),
            DeliveryStatus = string.IsNullOrWhiteSpace(request.DeliveryStatus) ? order.DeliveryStatus : request.DeliveryStatus.Trim(),
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await SaveOrderAsync(updated);
        return updated;
    }

    public async Task DeleteOrderAsync(Guid orderId)
    {
        var order = await GetOrderAsync(orderId);
        if (order is null)
        {
            return;
        }

        await _session.ExecuteAsync(new SimpleStatement("""
            DELETE FROM orders_by_day
            WHERE restaurant_id = ? AND business_date = ? AND created_at = ? AND order_id = ?
            """,
            RestaurantId,
            order.BusinessDate,
            order.CreatedAt,
            order.OrderId));

        await _session.ExecuteAsync(new SimpleStatement(
            "DELETE FROM orders_by_id WHERE restaurant_id = ? AND order_id = ?",
            RestaurantId,
            order.OrderId));
    }

    public async Task<RestaurantOrder?> RegisterPaymentAsync(Guid orderId, RegisterPaymentRequest request)
    {
        var order = await GetOrderAsync(orderId);
        if (order is null)
        {
            return null;
        }

        var updated = order with
        {
            Status = "Pagado",
            PaymentMethod = string.IsNullOrWhiteSpace(request.PaymentMethod) ? "Efectivo" : request.PaymentMethod.Trim(),
            DeliveryStatus = order.OrderKind.Equals("Delivery", StringComparison.OrdinalIgnoreCase) ? "Pagado" : order.DeliveryStatus,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await SaveOrderAsync(updated);
        await SavePaymentAsync(updated, request);
        await SaveInvoiceAsync(updated, request);
        return updated;
    }

    public async Task<DailySalesSummary> GetSummaryAsync(string businessDate)
    {
        var orders = await GetOrdersAsync(businessDate);
        var paidOrders = orders.Where(order => order.Status.Equals("Pagado", StringComparison.OrdinalIgnoreCase)).ToList();

        return new DailySalesSummary(
            businessDate,
            orders.Count,
            paidOrders.Count,
            paidOrders.Sum(order => order.Subtotal),
            paidOrders.Sum(order => order.Tax),
            paidOrders.Sum(order => order.Total),
            (await GetInvoicesAsync(businessDate)).Sum(invoice => invoice.Total));
    }

    public async Task<IReadOnlyList<DishSalesMetric>> GetDishSalesMetricsAsync(string businessDate)
    {
        var orders = await GetOrdersAsync(businessDate);

        return orders
            .Where(order => order.Status.Equals("Pagado", StringComparison.OrdinalIgnoreCase))
            .SelectMany(order => order.Lines)
            .GroupBy(line => line.ItemName)
            .Select(group => new DishSalesMetric(
                group.Key,
                group.Sum(line => line.Quantity),
                group.Sum(line => line.Quantity * line.UnitPrice)))
            .OrderByDescending(metric => metric.Quantity)
            .ToList();
    }

    public async Task<IReadOnlyList<PaymentRecord>> GetPaymentsAsync(string businessDate)
    {
        var rows = await _session.ExecuteAsync(new SimpleStatement("""
            SELECT payment_id, order_id, business_date, payment_method, amount, billing_name, tax_id, paid_at
            FROM payments_by_day
            WHERE restaurant_id = ? AND business_date = ?
            """, RestaurantId, businessDate));

        return rows.Select(MapPayment).ToList();
    }

    public async Task<IReadOnlyList<Invoice>> GetInvoicesAsync(string businessDate)
    {
        var rows = await _session.ExecuteAsync(new SimpleStatement("""
            SELECT invoice_id, order_id, business_date, invoice_number, billing_name, tax_id, subtotal, tax, total, issued_at
            FROM invoices_by_day
            WHERE restaurant_id = ? AND business_date = ?
            """, RestaurantId, businessDate));

        return rows.Select(MapInvoice).ToList();
    }

    private async Task SaveOrderAsync(RestaurantOrder order)
    {
        var linesJson = JsonSerializer.Serialize(order.Lines);

        await _session.ExecuteAsync(new SimpleStatement("""
            INSERT INTO orders_by_day
            (restaurant_id, business_date, created_at, order_id, order_kind, table_or_channel, customer_name, customer_phone,
             delivery_address, delivery_status, server_name, status, payment_method, lines_json, subtotal, tax, total, updated_at)
            VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
            """,
            RestaurantId,
            order.BusinessDate,
            order.CreatedAt,
            order.OrderId,
            order.OrderKind,
            order.TableOrChannel,
            order.CustomerName,
            order.CustomerPhone,
            order.DeliveryAddress,
            order.DeliveryStatus,
            order.ServerName,
            order.Status,
            order.PaymentMethod,
            linesJson,
            order.Subtotal,
            order.Tax,
            order.Total,
            order.UpdatedAt));

        await _session.ExecuteAsync(new SimpleStatement("""
            INSERT INTO orders_by_id
            (restaurant_id, order_id, business_date, created_at, order_kind, table_or_channel, customer_name, customer_phone,
             delivery_address, delivery_status, server_name, status, payment_method, lines_json, subtotal, tax, total, updated_at)
            VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
            """,
            RestaurantId,
            order.OrderId,
            order.BusinessDate,
            order.CreatedAt,
            order.OrderKind,
            order.TableOrChannel,
            order.CustomerName,
            order.CustomerPhone,
            order.DeliveryAddress,
            order.DeliveryStatus,
            order.ServerName,
            order.Status,
            order.PaymentMethod,
            linesJson,
            order.Subtotal,
            order.Tax,
            order.Total,
            order.UpdatedAt));
    }

    private async Task SavePaymentAsync(RestaurantOrder order, RegisterPaymentRequest request)
    {
        var payment = new PaymentRecord(
            Guid.NewGuid(),
            order.OrderId,
            order.BusinessDate,
            string.IsNullOrWhiteSpace(request.PaymentMethod) ? "Efectivo" : request.PaymentMethod.Trim(),
            order.Total,
            string.IsNullOrWhiteSpace(request.BillingName) ? "Consumidor Final" : request.BillingName.Trim(),
            string.IsNullOrWhiteSpace(request.TaxId) ? "0" : request.TaxId.Trim(),
            DateTimeOffset.UtcNow);

        await _session.ExecuteAsync(new SimpleStatement("""
            INSERT INTO payments_by_day
            (restaurant_id, business_date, paid_at, payment_id, order_id, payment_method, amount, billing_name, tax_id)
            VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)
            """,
            RestaurantId,
            payment.BusinessDate,
            payment.PaidAt,
            payment.PaymentId,
            payment.OrderId,
            payment.PaymentMethod,
            payment.Amount,
            payment.BillingName,
            payment.TaxId));
    }

    private async Task SaveInvoiceAsync(RestaurantOrder order, RegisterPaymentRequest request)
    {
        var issuedAt = DateTimeOffset.UtcNow;
        var invoice = new Invoice(
            Guid.NewGuid(),
            order.OrderId,
            order.BusinessDate,
            $"SM-{issuedAt:yyyyMMddHHmmss}-{Random.Shared.Next(100, 999)}",
            string.IsNullOrWhiteSpace(request.BillingName) ? "Consumidor Final" : request.BillingName.Trim(),
            string.IsNullOrWhiteSpace(request.TaxId) ? "0" : request.TaxId.Trim(),
            order.Subtotal,
            order.Tax,
            order.Total,
            issuedAt);

        await _session.ExecuteAsync(new SimpleStatement("""
            INSERT INTO invoices_by_day
            (restaurant_id, business_date, issued_at, invoice_id, order_id, invoice_number, billing_name, tax_id, subtotal, tax, total)
            VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
            """,
            RestaurantId,
            invoice.BusinessDate,
            invoice.IssuedAt,
            invoice.InvoiceId,
            invoice.OrderId,
            invoice.InvoiceNumber,
            invoice.BillingName,
            invoice.TaxId,
            invoice.Subtotal,
            invoice.Tax,
            invoice.Total));
    }

    private static RestaurantOrder MapOrder(Row row)
    {
        var createdAt = row.GetValue<DateTimeOffset>("created_at");
        var updatedAt = row.GetValue<DateTimeOffset>("updated_at");
        var lines = JsonSerializer.Deserialize<List<OrderLine>>(row.GetValue<string>("lines_json")) ?? [];

        return new RestaurantOrder(
            row.GetValue<Guid>("order_id"),
            row.GetValue<string>("business_date"),
            row.GetValue<string>("table_or_channel"),
            row.GetValue<string>("server_name"),
            row.GetValue<string>("status"),
            row.GetValue<string>("payment_method"),
            lines,
            row.GetValue<decimal>("subtotal"),
            row.GetValue<decimal>("tax"),
            row.GetValue<decimal>("total"),
            createdAt,
            updatedAt,
            GetOptionalString(row, "order_kind", "Mesa"),
            GetOptionalString(row, "customer_name", string.Empty),
            GetOptionalString(row, "customer_phone", string.Empty),
            GetOptionalString(row, "delivery_address", string.Empty),
            GetOptionalString(row, "delivery_status", string.Empty));
    }

    private static PaymentRecord MapPayment(Row row)
    {
        return new PaymentRecord(
            row.GetValue<Guid>("payment_id"),
            row.GetValue<Guid>("order_id"),
            row.GetValue<string>("business_date"),
            row.GetValue<string>("payment_method"),
            row.GetValue<decimal>("amount"),
            row.GetValue<string>("billing_name"),
            row.GetValue<string>("tax_id"),
            row.GetValue<DateTimeOffset>("paid_at"));
    }

    private static Invoice MapInvoice(Row row)
    {
        return new Invoice(
            row.GetValue<Guid>("invoice_id"),
            row.GetValue<Guid>("order_id"),
            row.GetValue<string>("business_date"),
            row.GetValue<string>("invoice_number"),
            row.GetValue<string>("billing_name"),
            row.GetValue<string>("tax_id"),
            row.GetValue<decimal>("subtotal"),
            row.GetValue<decimal>("tax"),
            row.GetValue<decimal>("total"),
            row.GetValue<DateTimeOffset>("issued_at"));
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
}
