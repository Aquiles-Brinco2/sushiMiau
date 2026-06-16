namespace SushiMiau.Shared.Contracts;

public static class AppRoles
{
    public const string Admin = "Administrador";
    public const string Owner = "Dueno";
    public const string Manager = "Gerente";
    public const string Kitchen = "Cocina";
    public const string Cashier = "Caja";
    public const string Inventory = "Inventario";
}

public sealed record AppUser(
    Guid UserId,
    string FullName,
    string Username,
    string Role,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record LoginRequest(string Username, string Password);

public sealed record LoginResponse(
    bool Success,
    string Message,
    AppUser? User);

public sealed record CreateUserRequest(
    string FullName,
    string Username,
    string Password,
    string Role,
    bool IsActive);

public sealed record UpdateUserRequest(
    string FullName,
    string Role,
    bool IsActive,
    string? Password);
