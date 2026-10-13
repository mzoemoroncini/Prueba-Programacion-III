using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection;

namespace Offers.Api.Features.OfferPublication;

public static class AdminAccess
{
    public static void AddAdminAccess(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton(new AdminCredentials(builder.Configuration));
        builder.Services.AddDataProtection().PersistKeysToFileSystem(
            new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, ".local", "keys")));
        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
        {
            options.Cookie.Name = "Offers.Admin";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
            options.ExpireTimeSpan = TimeSpan.FromHours(2);
            options.SlidingExpiration = false;
            options.Events.OnRedirectToLogin = context => { context.Response.StatusCode = 401; return Task.CompletedTask; };
            options.Events.OnRedirectToAccessDenied = context => { context.Response.StatusCode = 403; return Task.CompletedTask; };
        });
        builder.Services.AddAuthorization(options => options.AddPolicy("Administrator", policy => policy.RequireRole("Administrator")));
        builder.Services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-CSRF-TOKEN";
            options.Cookie.Name = "Offers.Antiforgery";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        });
    }

    public static void MapAdminAccess(this WebApplication app)
    {
        app.MapGet("/api/admin/csrf", (HttpContext http, IAntiforgery antiforgery) =>
        {
            http.Response.Headers.CacheControl = "no-store";
            return Results.Ok(new { token = antiforgery.GetAndStoreTokens(http).RequestToken });
        });
        app.MapPost("/api/admin/login", async (AdminLoginDto input, HttpContext http, AdminCredentials credentials) =>
        {
            if (!credentials.IsConfigured) return Results.Problem(statusCode: 503, title: "El acceso administrativo no está configurado.");
            if (string.IsNullOrEmpty(input.Username) || string.IsNullOrEmpty(input.Password)
                || input.Username.Length > 256 || input.Password.Length > 1024 || !credentials.Verify(input.Username, input.Password))
                return Results.Unauthorized();
            var identity = new ClaimsIdentity(new[] {
                new Claim(ClaimTypes.Name, input.Username), new Claim(ClaimTypes.Role, "Administrator")
            }, CookieAuthenticationDefaults.AuthenticationScheme);
            await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
            return Results.NoContent();
        }).AddEndpointFilter<AdminCsrfFilter>();
        app.MapGet("/api/admin/session", (HttpContext http) =>
        {
            http.Response.Headers.CacheControl = "no-store";
            return Results.Ok(new { username = http.User.Identity!.Name });
        }).RequireAuthorization("Administrator");
        app.MapPost("/api/admin/logout", async (HttpContext http) =>
        {
            await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.NoContent();
        }).RequireAuthorization("Administrator").AddEndpointFilter<AdminCsrfFilter>();
    }
}

public sealed record AdminLoginDto(string? Username, string? Password);

public sealed class AdminCredentials
{
    private readonly string? username;
    private readonly string? passwordHash;
    private readonly PasswordHasher<string> hasher = new();
    public bool IsConfigured => username is not null && passwordHash is not null;
    public AdminCredentials(IConfiguration configuration)
    {
        username = configuration["Admin:Username"];
        var password = configuration["Admin:Password"];
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password)) { username = null; return; }
        passwordHash = hasher.HashPassword(username, password);
    }
    public bool Verify(string candidate, string password) => IsConfigured &&
        hasher.VerifyHashedPassword(username!, passwordHash!, password) != PasswordVerificationResult.Failed &&
        string.Equals(candidate, username, StringComparison.Ordinal);
}

public sealed class AdminCsrfFilter(IAntiforgery antiforgery) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try { await antiforgery.ValidateRequestAsync(context.HttpContext); }
        catch (AntiforgeryValidationException) { return Results.Problem(statusCode: 400, title: "La solicitud no pudo validarse. Reintentá la acción."); }
        return await next(context);
    }
}
