using System.Security.Claims;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Offers.Api.Configuration;
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
        builder.Services.AddScoped<AdminCredentials>();
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
            if (!await credentials.IsConfiguredAsync(http.RequestAborted)) return Results.Problem(statusCode: 503, title: "El acceso administrativo no está configurado.");
            if (string.IsNullOrEmpty(input.Username) || string.IsNullOrEmpty(input.Password)
                || input.Username.Length > 256 || input.Password.Length > 1024 || !await credentials.VerifyAsync(input.Username, input.Password, http.RequestAborted))
                return Results.Unauthorized();
            var identity = new ClaimsIdentity(new[] {
                new Claim(ClaimTypes.Name, input.Username), new Claim(ClaimTypes.Role, "Administrator")
            }, CookieAuthenticationDefaults.AuthenticationScheme);
            await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
            return Results.NoContent();
        }).AddEndpointFilter<AdminCsrfFilter>().RequireRateLimiting("login");
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

public sealed class AdminCredentials(IServiceProvider services)
{
    public async Task<bool> IsConfiguredAsync(CancellationToken token)
    {
        var db = services.GetService<ConfigurationDbContext>();
        if (db is null) return false;
        try { return await db.Administrators.AnyAsync(token); }
        catch (DbException) { return false; }
    }
    public async Task<bool> VerifyAsync(string username, string password, CancellationToken token)
    {
        var db = services.GetService<ConfigurationDbContext>();
        if (db is null) return false;
        var admin = await db.Administrators.SingleOrDefaultAsync(x => x.Username == username, token);
        var hasher = new PasswordHasher<Administrator>();
        if (admin is null) return false;
        var result = hasher.VerifyHashedPassword(admin, admin.PasswordHash, password);
        if (result == PasswordVerificationResult.Failed) return false;
        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            admin.PasswordHash = hasher.HashPassword(admin, password); await db.SaveChangesAsync(token);
        }
        return string.Equals(admin.Username, username, StringComparison.Ordinal);
    }
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
