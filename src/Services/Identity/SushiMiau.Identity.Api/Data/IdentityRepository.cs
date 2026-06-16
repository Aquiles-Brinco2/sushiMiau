using System.Security.Cryptography;
using Cassandra;
using SushiMiau.Shared.Contracts;
using CassandraSession = Cassandra.ISession;

namespace SushiMiau.Identity.Api.Data;

public sealed class IdentityRepository
{
    private const string RestaurantId = "sushi-miau-centro";
    private readonly CassandraSession _session;

    public IdentityRepository(CassandraSession session)
    {
        _session = session;
    }

    public async Task InitializeAsync()
    {
        await _session.ExecuteAsync(new SimpleStatement("""
            CREATE TABLE IF NOT EXISTS app_users_by_username (
                restaurant_id text,
                username text,
                user_id uuid,
                full_name text,
                role text,
                is_active boolean,
                password_hash text,
                password_salt text,
                created_at timestamp,
                updated_at timestamp,
                PRIMARY KEY (restaurant_id, username)
            )
            """));

        await _session.ExecuteAsync(new SimpleStatement("""
            CREATE TABLE IF NOT EXISTS app_users_by_id (
                restaurant_id text,
                user_id uuid,
                username text,
                full_name text,
                role text,
                is_active boolean,
                password_hash text,
                password_salt text,
                created_at timestamp,
                updated_at timestamp,
                PRIMARY KEY (restaurant_id, user_id)
            )
            """));

        if ((await GetUsersAsync()).Count == 0)
        {
            await CreateUserAsync(new CreateUserRequest("Administrador General", "admin", "Admin123!", AppRoles.Admin, true));
            await CreateUserAsync(new CreateUserRequest("Caja Principal", "caja", "Caja123!", AppRoles.Cashier, true));
        }
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request)
    {
        var stored = await GetStoredUserByUsernameAsync(NormalizeUsername(request.Username));
        if (stored is null || !stored.User.IsActive)
        {
            return new LoginResponse(false, "Usuario o clave no validos.", null);
        }

        var isValid = VerifyPassword(request.Password, stored.PasswordSalt, stored.PasswordHash);
        return isValid
            ? new LoginResponse(true, "Sesion iniciada.", stored.User)
            : new LoginResponse(false, "Usuario o clave no validos.", null);
    }

    public async Task<IReadOnlyList<AppUser>> GetUsersAsync()
    {
        var rows = await _session.ExecuteAsync(new SimpleStatement("""
            SELECT user_id, username, full_name, role, is_active, created_at, updated_at
            FROM app_users_by_id
            WHERE restaurant_id = ?
            """, RestaurantId));

        return rows.Select(MapUser).OrderBy(user => user.FullName).ToList();
    }

    public async Task<AppUser?> GetUserAsync(Guid userId)
    {
        var rows = await _session.ExecuteAsync(new SimpleStatement("""
            SELECT user_id, username, full_name, role, is_active, created_at, updated_at
            FROM app_users_by_id
            WHERE restaurant_id = ? AND user_id = ?
            """, RestaurantId, userId));

        return rows.Select(MapUser).FirstOrDefault();
    }

    public async Task<AppUser> CreateUserAsync(CreateUserRequest request)
    {
        var now = DateTimeOffset.UtcNow;
        var username = NormalizeUsername(request.Username);
        var salt = GenerateSalt();
        var hash = HashPassword(request.Password, salt);

        var user = new AppUser(
            Guid.NewGuid(),
            request.FullName.Trim(),
            username,
            NormalizeRole(request.Role),
            request.IsActive,
            now,
            now);

        await SaveUserAsync(user, hash, salt);
        return user;
    }

    public async Task<AppUser?> UpdateUserAsync(Guid userId, UpdateUserRequest request)
    {
        var existing = await GetStoredUserByIdAsync(userId);
        if (existing is null)
        {
            return null;
        }

        var updated = existing.User with
        {
            FullName = request.FullName.Trim(),
            Role = NormalizeRole(request.Role),
            IsActive = request.IsActive,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var salt = existing.PasswordSalt;
        var hash = existing.PasswordHash;
        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            salt = GenerateSalt();
            hash = HashPassword(request.Password, salt);
        }

        await SaveUserAsync(updated, hash, salt);
        return updated;
    }

    private async Task<StoredUser?> GetStoredUserByUsernameAsync(string username)
    {
        var rows = await _session.ExecuteAsync(new SimpleStatement("""
            SELECT user_id, username, full_name, role, is_active, password_hash, password_salt, created_at, updated_at
            FROM app_users_by_username
            WHERE restaurant_id = ? AND username = ?
            """, RestaurantId, username));

        return rows.Select(MapStoredUser).FirstOrDefault();
    }

    private async Task<StoredUser?> GetStoredUserByIdAsync(Guid userId)
    {
        var rows = await _session.ExecuteAsync(new SimpleStatement("""
            SELECT user_id, username, full_name, role, is_active, password_hash, password_salt, created_at, updated_at
            FROM app_users_by_id
            WHERE restaurant_id = ? AND user_id = ?
            """, RestaurantId, userId));

        return rows.Select(MapStoredUser).FirstOrDefault();
    }

    private async Task SaveUserAsync(AppUser user, string hash, string salt)
    {
        await _session.ExecuteAsync(new SimpleStatement("""
            INSERT INTO app_users_by_username
            (restaurant_id, username, user_id, full_name, role, is_active, password_hash, password_salt, created_at, updated_at)
            VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
            """, RestaurantId, user.Username, user.UserId, user.FullName, user.Role, user.IsActive, hash, salt, user.CreatedAt, user.UpdatedAt));

        await _session.ExecuteAsync(new SimpleStatement("""
            INSERT INTO app_users_by_id
            (restaurant_id, user_id, username, full_name, role, is_active, password_hash, password_salt, created_at, updated_at)
            VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
            """, RestaurantId, user.UserId, user.Username, user.FullName, user.Role, user.IsActive, hash, salt, user.CreatedAt, user.UpdatedAt));
    }

    private static StoredUser MapStoredUser(Row row) =>
        new(MapUser(row), row.GetValue<string>("password_hash"), row.GetValue<string>("password_salt"));

    private static AppUser MapUser(Row row) =>
        new(
            row.GetValue<Guid>("user_id"),
            row.GetValue<string>("full_name"),
            row.GetValue<string>("username"),
            row.GetValue<string>("role"),
            row.GetValue<bool>("is_active"),
            row.GetValue<DateTimeOffset>("created_at"),
            row.GetValue<DateTimeOffset>("updated_at"));

    private static string NormalizeUsername(string username) => username.Trim().ToLowerInvariant();

    private static string NormalizeRole(string role)
    {
        var allowed = new[] { AppRoles.Admin, AppRoles.Owner, AppRoles.Manager, AppRoles.Kitchen, AppRoles.Cashier, AppRoles.Inventory };

        return allowed.FirstOrDefault(item => item.Equals(role.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? AppRoles.Manager;
    }

    private static string GenerateSalt() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));

    private static string HashPassword(string password, string salt)
    {
        using var deriveBytes = new Rfc2898DeriveBytes(password, Convert.FromBase64String(salt), 100_000, HashAlgorithmName.SHA256);
        return Convert.ToBase64String(deriveBytes.GetBytes(32));
    }

    private static bool VerifyPassword(string password, string salt, string expectedHash)
    {
        var actualHash = HashPassword(password, salt);
        return CryptographicOperations.FixedTimeEquals(Convert.FromBase64String(actualHash), Convert.FromBase64String(expectedHash));
    }

    private sealed record StoredUser(AppUser User, string PasswordHash, string PasswordSalt);
}
