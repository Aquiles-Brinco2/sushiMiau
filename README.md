# Sushi Miau Gestion

Aplicacion web interna para trabajadores y duenos de un restaurante de sushi. Incluye frontend Razor Pages, microservicios ASP.NET Core y Cassandra como base de datos real.

## Modulos

- **Usuarios/autenticacion y roles:** login interno, roles y gestion de usuarios solo para administradores.
- **Menu:** carta administrable para sala y delivery.
- **Reservas:** agenda de mesas creada por el personal.
- **Pedidos:** pedidos delivery gestionados internamente.
- **Inventario:** ingredientes, proveedores, stock minimo y movimientos de entrada/salida.
- **Pagos:** cobros, registro de pagos y facturacion.
- **Notificaciones:** comunicados internos por rol.
- **Reportes:** resumen administrativo de ventas, facturacion, delivery, reservas, stock y personal.
- **Web interna:** tablero operativo que consume los cuatro microservicios por HTTP.

## Arquitectura

```text
SushiMiau.Web
  -> SushiMiau.Identity.Api   -> Cassandra
  -> SushiMiau.Inventory.Api  -> Cassandra
  -> SushiMiau.Operations.Api -> Cassandra
  -> SushiMiau.Sales.Api      -> Cassandra
```

Los contratos y la conexion comun a Cassandra viven en `src/BuildingBlocks/SushiMiau.Shared`.

## Ejecutar con Docker

Requisitos: Docker Desktop.

```powershell
docker compose up --build
```

Luego abre:

- Web interna: http://localhost:5200
- Identity API Swagger: http://localhost:5204/swagger
- Inventory API Swagger: http://localhost:5201/swagger
- Operations API Swagger: http://localhost:5202/swagger
- Sales API Swagger: http://localhost:5203/swagger

Cassandra queda publicado en `localhost:9042`. Los microservicios crean el keyspace `sushi_miau`, sus tablas y datos semilla al arrancar.

Usuario inicial:

```text
usuario: admin
clave: Admin123!
rol: Administrador
```

## Ejecutar en local sin contenedores de apps

Levanta Cassandra:

```powershell
docker compose up cassandra
```

En terminales separadas:

```powershell
dotnet run --project src\Services\Inventory\SushiMiau.Inventory.Api
dotnet run --project src\Services\Identity\SushiMiau.Identity.Api
dotnet run --project src\Services\Operations\SushiMiau.Operations.Api
dotnet run --project src\Services\Sales\SushiMiau.Sales.Api
dotnet run --project src\Web\SushiMiau.Web
```

## Compilar

```powershell
dotnet build SushiMiau.slnx
```

## Base de datos

El proyecto usa Cassandra con tablas orientadas a consulta:

- `inventory_items`
- `stock_movements_by_item`
- `app_users_by_username`
- `app_users_by_id`
- `menu_items`
- `kitchen_tickets_by_day`
- `kitchen_tickets_by_id`
- `staff_shifts_by_day`
- `reservations_by_day`
- `reservations_by_id`
- `notifications_by_role`
- `orders_by_day`
- `orders_by_id`
- `payments_by_day`
- `invoices_by_day`

Esta estructura evita una base monolitica y mantiene cada microservicio como propietario de su modelo de lectura/escritura.
