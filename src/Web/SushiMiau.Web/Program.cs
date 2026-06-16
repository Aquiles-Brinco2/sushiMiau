using Microsoft.AspNetCore.Authentication.Cookies;
using SushiMiau.Shared.Contracts;
using SushiMiau.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/");
    options.Conventions.AllowAnonymousToPage("/Login");
    options.Conventions.AuthorizePage("/Usuarios", "AdminOnly");
});
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Login";
        options.AccessDeniedPath = "/Login";
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole(AppRoles.Admin));
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient("Identity", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ServiceUrls:Identity"] ?? "http://localhost:5204");
});
builder.Services.AddHttpClient("Inventory", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ServiceUrls:Inventory"] ?? "http://localhost:5201");
});
builder.Services.AddHttpClient("Operations", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ServiceUrls:Operations"] ?? "http://localhost:5202");
});
builder.Services.AddHttpClient("Sales", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ServiceUrls:Sales"] ?? "http://localhost:5203");
});
builder.Services.AddScoped<RestaurantApiClient>();

var app = builder.Build();

app.UseExceptionHandler("/Error");
app.UseStatusCodePagesWithReExecute("/Error", "?statusCode={0}");

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();
app.Run();
