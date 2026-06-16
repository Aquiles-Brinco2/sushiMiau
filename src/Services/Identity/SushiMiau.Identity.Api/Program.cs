using SushiMiau.Identity.Api.Data;
using SushiMiau.Shared.Cassandra;
using SushiMiau.Shared.Contracts;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSushiMiauCassandra(builder.Configuration);
builder.Services.AddSingleton<IdentityRepository>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

var repository = app.Services.GetRequiredService<IdentityRepository>();
await repository.InitializeAsync();

app.MapGet("/health", () => Results.Ok(new { service = "identity", status = "ok" }));

app.MapPost("/api/auth/login", async (LoginRequest request, IdentityRepository repo) =>
{
    var response = await repo.LoginAsync(request);
    return response.Success ? Results.Ok(response) : Results.Unauthorized();
});

app.MapGet("/api/users", async (HttpContext context, IdentityRepository repo) =>
{
    if (!IsAdmin(context))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    return Results.Ok(await repo.GetUsersAsync());
});

app.MapGet("/api/users/{userId:guid}", async (Guid userId, HttpContext context, IdentityRepository repo) =>
{
    if (!IsAdmin(context))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var user = await repo.GetUserAsync(userId);
    return user is null ? Results.NotFound() : Results.Ok(user);
});

app.MapPost("/api/users", async (CreateUserRequest request, HttpContext context, IdentityRepository repo) =>
{
    if (!IsAdmin(context))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var user = await repo.CreateUserAsync(request);
    return Results.Created($"/api/users/{user.UserId}", user);
});

app.MapPut("/api/users/{userId:guid}", async (Guid userId, UpdateUserRequest request, HttpContext context, IdentityRepository repo) =>
{
    if (!IsAdmin(context))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var user = await repo.UpdateUserAsync(userId, request);
    return user is null ? Results.NotFound() : Results.Ok(user);
});

app.Run();

static bool IsAdmin(HttpContext context) =>
    context.Request.Headers.TryGetValue("X-SushiMiau-Role", out var role)
    && role.ToString().Equals(AppRoles.Admin, StringComparison.OrdinalIgnoreCase);
