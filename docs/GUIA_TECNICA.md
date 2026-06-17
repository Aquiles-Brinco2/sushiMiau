# Guia tecnica de codigo - Sushi Miau

Este documento explica como esta organizado el sistema a nivel de codigo: que archivo controla cada pagina, donde estan los consumos HTTP hacia los microservicios y donde cada servicio lee o escribe en Cassandra.

## 1. Arquitectura general

El sistema esta dividido en una app web administrativa y cuatro microservicios. La comunicacion normal es:

```text
Navegador
  -> Razor Page (.cshtml)
  -> PageModel (.cshtml.cs)
  -> RestaurantApiClient
  -> Microservicio HTTP
  -> Repositorio
  -> Cassandra
```

Proyectos principales:

| Proyecto | Ruta | Responsabilidad |
| --- | --- | --- |
| Web | `src/Web/SushiMiau.Web` | Interfaz administrativa en Razor Pages, formularios, layout, estilos y JavaScript. |
| Shared | `src/BuildingBlocks/SushiMiau.Shared` | Contratos DTO compartidos y configuracion comun de Cassandra. |
| Identity | `src/Services/Identity/SushiMiau.Identity.Api` | Login, usuarios, roles y permisos administrativos. |
| Inventory | `src/Services/Inventory/SushiMiau.Inventory.Api` | Ingredientes, categorias de inventario, stock y movimientos internos. |
| Operations | `src/Services/Operations/SushiMiau.Operations.Api` | Menu, categorias del menu, clientes, reservas, mesas, turnos, tickets y notificaciones. |
| Sales | `src/Services/Sales/SushiMiau.Sales.Api` | Pedidos de mesa, pedidos delivery, pagos, facturas y reportes de ventas. |

## 2. Configuracion y arranque

### Docker y solucion

- `docker-compose.yml`: levanta Cassandra, los microservicios y la web.
- `Dockerfile`: build multi-stage para la solucion .NET.
- `SushiMiau.slnx`: solucion principal.
- `README.md`: documentacion general de ejecucion.

### Web

- `src/Web/SushiMiau.Web/Program.cs`
  - Configura Razor Pages.
  - Configura sesion/autenticacion simple.
  - Registra los `HttpClient` para cada microservicio.
  - Variables de configuracion usadas por la web:
    - `Services:Identity`
    - `Services:Inventory`
    - `Services:Operations`
    - `Services:Sales`

Valores por defecto usados en desarrollo:

```text
Identity   http://localhost:5204
Inventory  http://localhost:5201
Operations http://localhost:5202
Sales      http://localhost:5203
```

### Cassandra compartido

La conexion comun a Cassandra esta en:

- `src/BuildingBlocks/SushiMiau.Shared/Cassandra/CassandraOptions.cs`
- `src/BuildingBlocks/SushiMiau.Shared/Cassandra/CassandraServiceCollectionExtensions.cs`
- `src/BuildingBlocks/SushiMiau.Shared/Cassandra/CassandraSessionFactory.cs`

Cada microservicio llama:

```csharp
builder.Services.AddSushiMiauCassandra(builder.Configuration);
```

Luego crea su repositorio, ejecuta `InitializeAsync()` y ese repositorio crea las tablas que necesita.

## 3. Contratos compartidos

Los DTO usados entre la web y los microservicios estan en:

| Archivo | Dominio |
| --- | --- |
| `src/BuildingBlocks/SushiMiau.Shared/Contracts/IdentityContracts.cs` | Login, usuario, roles. |
| `src/BuildingBlocks/SushiMiau.Shared/Contracts/InventoryContracts.cs` | Ingredientes, categorias, movimientos, snapshot de inventario. |
| `src/BuildingBlocks/SushiMiau.Shared/Contracts/OperationsContracts.cs` | Menu, clientes, reservas, mesas, turnos, notificaciones. |
| `src/BuildingBlocks/SushiMiau.Shared/Contracts/SalesContracts.cs` | Pedidos, lineas, delivery, pagos, facturas, metricas. |

Cuando una pagina necesita enviar o recibir datos, normalmente usa uno de estos contratos.

## 4. Cliente HTTP central de la web

Archivo principal:

- `src/Web/SushiMiau.Web/Services/RestaurantApiClient.cs`

Este archivo concentra los consumos desde la web hacia todos los microservicios. Las paginas Razor no llaman directamente a Cassandra ni a los repositorios; llaman a metodos de este cliente.

Responsabilidades importantes:

- Login y usuarios contra Identity.
- Ingredientes/categorias contra Inventory.
- Menu, reservas, clientes, mesas y notificaciones contra Operations.
- Pedidos, pagos, facturas y reportes contra Sales.
- Manejo de errores para no mostrar directamente excepciones tecnicas al usuario.
- Fallback local para errores de DNS/`SocketException` cuando el nombre de servicio Docker no resuelve en ejecucion local.
- Descuento aproximado de inventario despues de registrar pedidos mediante `DiscountInventoryForOrderAsync`.

Si una pagina falla al consultar un microservicio, este es el primer archivo que conviene revisar.

## 5. Paginas web y ubicacion de cada apartado

Todas las paginas estan en:

```text
src/Web/SushiMiau.Web/Pages
```

Cada pagina tiene:

- `.cshtml`: vista, formularios, tablas y modales.
- `.cshtml.cs`: PageModel, carga de datos, validaciones de formulario y llamadas a `RestaurantApiClient`.

### Login

| Elemento | Archivo |
| --- | --- |
| Vista | `src/Web/SushiMiau.Web/Pages/Login.cshtml` |
| Logica | `src/Web/SushiMiau.Web/Pages/Login.cshtml.cs` |
| API consumida | `RestaurantApiClient.LoginAsync` |
| Backend | `src/Services/Identity/SushiMiau.Identity.Api/Program.cs` |
| Cassandra | `IdentityRepository` |

El login guarda la informacion del usuario en sesion. El layout revisa esa sesion para mostrar u ocultar navegacion.

### Panel principal y mesas

| Elemento | Archivo |
| --- | --- |
| Vista | `src/Web/SushiMiau.Web/Pages/Index.cshtml` |
| Logica | `src/Web/SushiMiau.Web/Pages/Index.cshtml.cs` |
| Estilos | `src/Web/SushiMiau.Web/wwwroot/css/site.css` |
| Interaccion JS | `src/Web/SushiMiau.Web/wwwroot/js/site.js` |
| APIs consumidas | Dashboard, mesas, reservas, pedidos |
| Backend principal | Operations y Sales |

Aqui vive el panel interactivo de mesas. El PageModel arma el estado actual de cada mesa cruzando:

- mesas registradas,
- reservas del dia,
- pedidos de mesa del dia,
- estado manual de la mesa.

La mesa se pinta segun su estado: disponible, ocupada, reservada o fuera de servicio. Al hacer click, el panel lateral muestra informacion de capacidad, reserva, empleado asignado y pedido asociado.

### Usuarios y roles

| Elemento | Archivo |
| --- | --- |
| Vista | `src/Web/SushiMiau.Web/Pages/Usuarios.cshtml` |
| Logica | `src/Web/SushiMiau.Web/Pages/Usuarios.cshtml.cs` |
| API consumida | `GetUsersAsync`, `AddUserAsync`, `UpdateUserAsync` |
| Backend | Identity API |
| Cassandra | `app_users_by_username`, `app_users_by_id` |

Las acciones por empleado se manejan desde la pagina y pasan por validaciones de rol administrativo en el microservicio Identity.

### Menu

| Elemento | Archivo |
| --- | --- |
| Vista | `src/Web/SushiMiau.Web/Pages/Menu.cshtml` |
| Logica | `src/Web/SushiMiau.Web/Pages/Menu.cshtml.cs` |
| Formulario reusable | `src/Web/SushiMiau.Web/Pages/Shared/_MenuProductForm.cshtml` |
| APIs consumidas | Menu/categorias en Operations, ingredientes en Inventory |
| Backend | Operations API e Inventory API |
| Cassandra | `menu_items`, `menu_categories`, `inventory_items` |

Los productos del menu usan ingredientes del inventario. La relacion entre plato e ingredientes viaja en los contratos de menu y se guarda desde Operations.

Tambien desde esta pagina se puede:

- crear categoria de menu,
- crear producto,
- editar producto,
- cambiar disponibilidad,
- eliminar producto con confirmacion,
- abrir el formulario para crear ingrediente de inventario cuando haga falta.

### Clientes

| Elemento | Archivo |
| --- | --- |
| Vista | `src/Web/SushiMiau.Web/Pages/Clientes.cshtml` |
| Logica | `src/Web/SushiMiau.Web/Pages/Clientes.cshtml.cs` |
| API consumida | Clientes en Operations |
| Backend | Operations API |
| Cassandra | `customers` |

El cliente guarda datos minimos: nombre, telefono y NIT. La pagina incluye acciones de crear, editar y eliminar.

### Reservas

| Elemento | Archivo |
| --- | --- |
| Vista | `src/Web/SushiMiau.Web/Pages/Reservas.cshtml` |
| Logica | `src/Web/SushiMiau.Web/Pages/Reservas.cshtml.cs` |
| APIs consumidas | Reservas, clientes, mesas y menu |
| Backend | Operations API |
| Cassandra | `reservations_by_day`, `reservations_by_id`, `customers`, `restaurant_tables` |

Esta pagina permite ver reservas del dia, consultar reservas pasadas y filtrar por estado, hora, cliente y fecha. Las reservas que no son del dia se muestran con su ultimo estado y no se editan.

Una reserva puede terminar asociada a un pedido de mesa, pero no es obligatorio.

### Pedidos de mesa

| Elemento | Archivo |
| --- | --- |
| Vista | `src/Web/SushiMiau.Web/Pages/Pedidos.cshtml` |
| Logica | `src/Web/SushiMiau.Web/Pages/Pedidos.cshtml.cs` |
| Filas dinamicas | `src/Web/SushiMiau.Web/Pages/Shared/_DynamicOrderLines.cshtml` |
| Edicion de lineas | `src/Web/SushiMiau.Web/Pages/Shared/_EditableOrderLines.cshtml` |
| JS de filas/totales | `src/Web/SushiMiau.Web/wwwroot/js/site.js` |
| APIs consumidas | Pedidos Sales, menu Operations, mesas Operations |
| Backend | Sales API y Operations API |
| Cassandra | `orders_by_day`, `orders_by_id`, `menu_items`, `restaurant_tables` |

Detalles importantes:

- La mesa es un dropdown, no texto libre.
- El operador se toma del usuario logueado.
- El usuario puede agregar varias filas de productos.
- El precio se calcula desde el menu y no se modifica desde el formulario.
- El subtotal y total se muestran antes de registrar el pedido.
- El historial del dia se puede editar o cancelar.
- Al registrar el pedido se descuenta inventario de forma aproximada segun ingredientes del plato.

### Delivery

| Elemento | Archivo |
| --- | --- |
| Vista | `src/Web/SushiMiau.Web/Pages/Delivery.cshtml` |
| Logica | `src/Web/SushiMiau.Web/Pages/Delivery.cshtml.cs` |
| Filas dinamicas | `src/Web/SushiMiau.Web/Pages/Shared/_DynamicOrderLines.cshtml` |
| Edicion de lineas | `src/Web/SushiMiau.Web/Pages/Shared/_EditableOrderLines.cshtml` |
| APIs consumidas | Delivery Sales, menu Operations |
| Backend | Sales API y Operations API |
| Cassandra | `orders_by_day`, `orders_by_id` |

Delivery usa el mismo modelo base de pedidos, pero con datos de entrega. El estado puede usarse para reportes y para diferenciar flujo: preparando, en camino, pagado, cancelado, etc.

### Inventario

| Elemento | Archivo |
| --- | --- |
| Vista | `src/Web/SushiMiau.Web/Pages/Inventario.cshtml` |
| Logica | `src/Web/SushiMiau.Web/Pages/Inventario.cshtml.cs` |
| APIs consumidas | Ingredientes/categorias Inventory, notificaciones Operations |
| Backend | Inventory API y Operations API |
| Cassandra | `inventory_items`, `inventory_categories`, `stock_movements_by_item`, `notifications_by_role` |

La pagina permite crear, editar y eliminar ingredientes. Tambien permite crear categorias de ingredientes y crear notificaciones relacionadas con stock.

El color del inventario se calcula desde el stock actual y el minimo:

- rojo: en minimo o por debajo,
- verde: stock mayor o igual al minimo por dos,
- amarillo: estado intermedio.

### Pagos y facturacion

| Elemento | Archivo |
| --- | --- |
| Vista | `src/Web/SushiMiau.Web/Pages/Pagos.cshtml` |
| Logica | `src/Web/SushiMiau.Web/Pages/Pagos.cshtml.cs` |
| APIs consumidas | Pedidos, pagos, facturas y resumen Sales |
| Backend | Sales API |
| Cassandra | `payments_by_day`, `invoices_by_day`, `orders_by_day`, `orders_by_id` |

Desde esta pagina se registran metodos de pago y facturacion asociada a pedidos.

### Notificaciones

| Elemento | Archivo |
| --- | --- |
| Vista | `src/Web/SushiMiau.Web/Pages/Notificaciones.cshtml` |
| Logica | `src/Web/SushiMiau.Web/Pages/Notificaciones.cshtml.cs` |
| API consumida | Notificaciones Operations |
| Backend | Operations API |
| Cassandra | `notifications_by_role` |

Las notificaciones tienen color por severidad/tipo y se pueden originar tambien desde inventario.

### Reportes

| Elemento | Archivo |
| --- | --- |
| Vista | `src/Web/SushiMiau.Web/Pages/Reportes.cshtml` |
| Logica | `src/Web/SushiMiau.Web/Pages/Reportes.cshtml.cs` |
| APIs consumidas | Summary, dish metrics, delivery/orders, reservas, inventario |
| Backend | Sales, Operations e Inventory |
| Cassandra | Lee tablas de ventas, reservas e inventario |

Reportes cruza informacion de varios servicios para mostrar ventas, platos vendidos, pedidos, inventario y metricas filtrables.

## 6. Layout, estilos y JavaScript

Archivos compartidos de UI:

| Archivo | Funcion |
| --- | --- |
| `src/Web/SushiMiau.Web/Pages/Shared/_Layout.cshtml` | Estructura general, menu lateral, navegacion y scripts globales. |
| `src/Web/SushiMiau.Web/Pages/Shared/_Flash.cshtml` | Mensajes de exito/error para formularios. |
| `src/Web/SushiMiau.Web/wwwroot/css/site.css` | Paleta visual, layout lateral, tablas, cards, paneles, formularios y estados por color. |
| `src/Web/SushiMiau.Web/wwwroot/js/site.js` | Dialogos, confirmaciones, dropdowns filtrables, filas dinamicas de pedidos, calculo de totales y panel de mesas. |

Si el cambio es solo visual, normalmente se toca `site.css`. Si el cambio es una interaccion del navegador, se revisa `site.js`.

## 7. Microservicios, endpoints y repositorios

### Identity

| Archivo | Funcion |
| --- | --- |
| `src/Services/Identity/SushiMiau.Identity.Api/Program.cs` | Endpoints HTTP de auth y usuarios. |
| `src/Services/Identity/SushiMiau.Identity.Api/Data/IdentityRepository.cs` | Consultas Cassandra de usuarios. |

Endpoints principales:

- `POST /api/auth/login`
- `GET /api/users`
- `POST /api/users`
- `PUT /api/users/{userId}`

Tablas Cassandra:

- `app_users_by_username`
- `app_users_by_id`

### Inventory

| Archivo | Funcion |
| --- | --- |
| `src/Services/Inventory/SushiMiau.Inventory.Api/Program.cs` | Endpoints HTTP de inventario. |
| `src/Services/Inventory/SushiMiau.Inventory.Api/Data/InventoryRepository.cs` | Consultas Cassandra de ingredientes, categorias y movimientos. |

Endpoints principales:

- `GET /api/inventory/items`
- `POST /api/inventory/items`
- `PUT /api/inventory/items/{id}`
- `DELETE /api/inventory/items/{id}`
- `GET /api/inventory/categories`
- `POST /api/inventory/categories`
- `GET /api/inventory/snapshot`
- `POST /api/inventory/items/{id}/movements`

Tablas Cassandra:

- `inventory_items`
- `inventory_categories`
- `stock_movements_by_item`

### Operations

| Archivo | Funcion |
| --- | --- |
| `src/Services/Operations/SushiMiau.Operations.Api/Program.cs` | Endpoints HTTP operativos. |
| `src/Services/Operations/SushiMiau.Operations.Api/Data/OperationsRepository.cs` | Consultas Cassandra de menu, reservas, mesas, clientes y notificaciones. |

Endpoints principales:

- `GET /api/operations/menu`
- `POST /api/operations/menu`
- `PUT /api/operations/menu/{id}`
- `DELETE /api/operations/menu/{id}`
- `GET /api/operations/menu/categories`
- `POST /api/operations/menu/categories`
- `GET /api/operations/customers`
- `POST /api/operations/customers`
- `PUT /api/operations/customers/{id}`
- `DELETE /api/operations/customers/{id}`
- `GET /api/operations/reservations`
- `POST /api/operations/reservations`
- `PUT /api/operations/reservations/{id}/status`
- `GET /api/operations/tables`
- `PUT /api/operations/tables/{tableName}`
- `GET /api/operations/notifications`
- `POST /api/operations/notifications`

Tablas Cassandra:

- `restaurant_tables`
- `menu_items`
- `menu_categories`
- `customers`
- `reservations_by_day`
- `reservations_by_id`
- `kitchen_tickets_by_day`
- `kitchen_tickets_by_id`
- `staff_shifts_by_day`
- `notifications_by_role`

### Sales

| Archivo | Funcion |
| --- | --- |
| `src/Services/Sales/SushiMiau.Sales.Api/Program.cs` | Endpoints HTTP de pedidos, pagos y reportes. |
| `src/Services/Sales/SushiMiau.Sales.Api/Data/SalesRepository.cs` | Consultas Cassandra de pedidos, pagos y facturas. |

Endpoints principales:

- `GET /api/sales/orders`
- `POST /api/sales/orders`
- `PUT /api/sales/orders/{orderId}`
- `PATCH /api/sales/orders/{orderId}/status`
- `DELETE /api/sales/orders/{orderId}`
- `GET /api/sales/delivery-orders`
- `POST /api/sales/delivery-orders`
- `PATCH /api/sales/delivery-orders/{orderId}/status`
- `PATCH /api/sales/orders/{orderId}/pay`
- `POST /api/sales/payments`
- `POST /api/sales/invoices`
- `GET /api/sales/summary`
- `GET /api/sales/dish-metrics`

Tablas Cassandra:

- `orders_by_day`
- `orders_by_id`
- `payments_by_day`
- `invoices_by_day`

## 8. Donde se consume Cassandra

La web no consume Cassandra directamente. Los consumos Cassandra estan encapsulados en repositorios:

| Dominio | Repositorio | Tablas principales |
| --- | --- | --- |
| Usuarios | `IdentityRepository.cs` | `app_users_by_username`, `app_users_by_id` |
| Inventario | `InventoryRepository.cs` | `inventory_items`, `inventory_categories`, `stock_movements_by_item` |
| Operaciones | `OperationsRepository.cs` | `menu_items`, `menu_categories`, `customers`, `reservations_by_day`, `restaurant_tables`, `notifications_by_role` |
| Ventas | `SalesRepository.cs` | `orders_by_day`, `orders_by_id`, `payments_by_day`, `invoices_by_day` |

Patron repetido en cada repositorio:

1. `InitializeAsync()` crea tablas si no existen.
2. Los metodos publicos reciben contratos o parametros simples.
3. El repositorio arma consultas CQL.
4. El resultado se mapea de nuevo a DTOs compartidos.

Si necesitas agregar una nueva tabla Cassandra, normalmente se hacen cambios en:

1. Un contrato en `SushiMiau.Shared/Contracts`.
2. El repositorio del microservicio dueño de la informacion.
3. `Program.cs` del microservicio para exponer el endpoint.
4. `RestaurantApiClient.cs` para consumirlo desde la web.
5. La pagina Razor que lo va a mostrar o editar.

## 9. Flujos de negocio importantes

### Crear pedido de mesa

```text
Pedidos.cshtml
  -> Pedidos.cshtml.cs
  -> RestaurantApiClient.CreateOrderAsync
  -> Sales API POST /api/sales/orders
  -> SalesRepository
  -> Cassandra orders_by_day / orders_by_id
  -> RestaurantApiClient.DiscountInventoryForOrderAsync
  -> Inventory API movements
  -> Cassandra inventory tables
```

El precio de los platos se toma del menu cargado desde Operations. Aunque alguien altere el formulario, el PageModel vuelve a calcular los precios antes de mandar el pedido.

### Crear pedido delivery

```text
Delivery.cshtml
  -> Delivery.cshtml.cs
  -> RestaurantApiClient.CreateDeliveryOrderAsync
  -> Sales API POST /api/sales/delivery-orders
  -> SalesRepository
  -> Cassandra orders_by_day / orders_by_id
```

Usa multiples lineas dinamicas igual que pedidos de mesa.

### Descontar inventario por plato

```text
Pedido creado
  -> RestaurantApiClient.DiscountInventoryForOrderAsync
  -> consulta menu/ingredientes
  -> Inventory API POST /api/inventory/items/{id}/movements
  -> InventoryRepository
```

El descuento es aproximado y depende de los ingredientes configurados en el plato del menu.

### Panel de mesas

```text
Index.cshtml.cs
  -> carga mesas desde Operations
  -> carga reservas del dia desde Operations
  -> carga pedidos del dia desde Sales
  -> arma modelo visual de mesas
  -> Index.cshtml renderiza cards de mesa
  -> site.js abre/cierra detalle interactivo
```

El estado del pedido asociado a una mesa se usa para mostrar si esta preparando, en mesa, pagado, cancelado, etc.

### Pagos y facturacion

```text
Pagos.cshtml
  -> Pagos.cshtml.cs
  -> RestaurantApiClient
  -> Sales API payments/invoices/pay
  -> SalesRepository
  -> Cassandra payments_by_day / invoices_by_day / orders_by_id
```

## 10. Guia rapida para cambios futuros

### Agregar una nueva pagina

1. Crear `Pages/NuevaPagina.cshtml`.
2. Crear `Pages/NuevaPagina.cshtml.cs`.
3. Agregar link en `Pages/Shared/_Layout.cshtml` si debe aparecer en el menu lateral.
4. Agregar metodos en `RestaurantApiClient.cs` si necesita datos del backend.
5. Agregar endpoint en el microservicio correspondiente si no existe.

### Agregar un nuevo campo a un formulario

1. Agregar el campo al contrato compartido.
2. Agregar input/select en el `.cshtml`.
3. Agregar propiedad bindable o mapping en el `.cshtml.cs`.
4. Actualizar `RestaurantApiClient.cs`.
5. Actualizar endpoint y repositorio del microservicio.
6. Si se guarda en Cassandra, actualizar tabla o estrategia de lectura/escritura.

### Cambiar diseno

- Layout general y menu lateral: `_Layout.cshtml`.
- Paleta, espaciado, cards, tablas, estados: `wwwroot/css/site.css`.
- Dialogos, dropdowns filtrables, filas dinamicas: `wwwroot/js/site.js`.

### Depurar errores de conexion a servicios

Revisar en este orden:

1. `docker-compose.yml`, nombres y puertos de servicios.
2. `src/Web/SushiMiau.Web/Program.cs`, URLs de `HttpClient`.
3. `RestaurantApiClient.cs`, metodo que llama al endpoint.
4. `Program.cs` del microservicio, ruta del endpoint.
5. Logs del contenedor correspondiente.

### Depurar datos en Cassandra

1. Identificar dominio: Identity, Inventory, Operations o Sales.
2. Abrir el repositorio correspondiente.
3. Buscar el nombre de tabla en `InitializeAsync()`.
4. Revisar el metodo que ejecuta la consulta CQL.
5. Comparar con el contrato DTO usado por la pagina.

## 11. Comandos utiles

Build local:

```powershell
dotnet build .\SushiMiau.slnx
```

Levantar todo con Docker:

```powershell
docker compose up -d --build
```

Levantar solo la web reconstruida:

```powershell
docker compose up -d --build web
```

Ver logs:

```powershell
docker compose logs -f web
docker compose logs -f sales
docker compose logs -f operations
docker compose logs -f inventory
docker compose logs -f identity
```

