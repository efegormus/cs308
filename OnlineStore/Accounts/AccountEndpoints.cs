using System.Security.Claims;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using OnlineStore.Data;
using OnlineStore.Models;

namespace OnlineStore.Accounts;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RegisterRequest(string? Name, string? Email, string? TaxId, string? HomeAddress, string? Password);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LoginRequest(string? Email, string? Password);
/// <summary>The editable part of a customer profile. Email, role and IDs are not accepted.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UpdateCustomerProfileRequest(string? Name, string? TaxId, string? HomeAddress);
/// <summary>Safe account information returned for the authenticated or newly created account.</summary>
public sealed record AccountDto(long Id, string Name, string Email, string Role)
{
    public static AccountDto FromUser(User user) => new(user.Id, user.Name, user.Email, user.Role.ToString());
}

/// <summary>Safe identity data for the current authenticated account.</summary>
public sealed record CurrentAccountDto(long Id, string Name, string Email, string Role, IReadOnlyList<string> Roles)
{
    public static CurrentAccountDto FromUser(User user)
    {
        var role = user.Role.ToString();
        return new CurrentAccountDto(user.Id, user.Name, user.Email, role, [role]);
    }
}

public static class AccountEndpoints
{
    public static void MapAccountEndpoints(this WebApplication app)
    {
        var auth = app.MapGroup("/api/auth");
        auth.AddEndpointFilter(PrivateRequest);

        auth.MapGet("/csrf", (HttpContext context, IAntiforgery antiforgery) =>
            Results.Ok(new { token = antiforgery.GetAndStoreTokens(context).RequestToken }))
            .AllowAnonymous();

        auth.MapPost("/register", async (RegisterRequest input, AccountService accounts, CancellationToken ct) =>
        {
            var profile = new CustomerProfileInput(input.Name, input.Email, input.TaxId, input.HomeAddress);
            var errors = CustomerProfileValidation.Validate(profile);
            if (!PasswordService.IsValidNewPassword(input.Password))
                errors["password"] = ["Use a password between 12 and 128 characters."];
            if (errors.Count != 0) return Results.ValidationProblem(errors);
            try
            {
                var user = await accounts.CreateCustomerAsync(profile, input.Password!, ct);
                return user is null ? Results.Conflict(new { message = "An account with this email already exists." })
                    : Results.Json(AccountDto.FromUser(user), statusCode: StatusCodes.Status201Created);
            }
            catch (Exception exception) when (DatabaseErrors.IsUnavailable(exception)) { return Unavailable(); }
        }).AllowAnonymous().RequireRateLimiting("account-attempts").AddEndpointFilter<CsrfFilter>();

        auth.MapPost("/login", async (LoginRequest input, HttpContext context, AccountService accounts, CancellationToken ct) =>
        {
            try
            {
                var user = await accounts.AuthenticateAsync(input.Email, input.Password, ct);
                if (user is null) return Results.Problem(statusCode: StatusCodes.Status401Unauthorized,
                    title: "Invalid email or password.");
                var identity = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(ClaimTypes.Name, user.Name),
                    new Claim(ClaimTypes.Role, user.Role.ToString()),
                    new Claim(SessionEvents.StampClaim, user.SecurityStamp)
                }, CookieAuthenticationDefaults.AuthenticationScheme);
                await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(identity), new AuthenticationProperties { IsPersistent = false });
                return Results.Ok(AccountDto.FromUser(user));
            }
            catch (Exception exception) when (DatabaseErrors.IsUnavailable(exception)) { return Unavailable(); }
        }).AllowAnonymous().RequireRateLimiting("account-attempts").AddEndpointFilter<CsrfFilter>();

        auth.MapPost("/logout", async (HttpContext context, StoreDbContext db, CancellationToken ct) =>
        {
            var id = long.Parse(context.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var user = await db.Users.SingleOrDefaultAsync(user => user.Id == id, ct);
            if (user is not null)
            {
                user.SecurityStamp = Guid.NewGuid().ToString("N");
                await db.SaveChangesAsync(ct);
            }
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.NoContent();
        }).RequireAuthorization().AddEndpointFilter<CsrfFilter>();

        auth.MapGet("/me", CurrentAccount).RequireAuthorization()
            .WithMetadata(PrivateResponseMetadata.Instance);

        app.MapGet("/api/me", CurrentAccount)
            .WithName("GetCurrentAccount")
            .WithSummary("Get the authenticated account summary")
            .RequireAuthorization()
            .WithMetadata(PrivateResponseMetadata.Instance)
            .AddEndpointFilter(PrivateRequest);

        // Read-only bootstrap for a management UI; business operations use the role-specific groups.
        app.MapGet("/api/management/session", (HttpContext context) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            return Results.Ok(new { role = context.User.FindFirstValue(ClaimTypes.Role) });
        }).RequireAuthorization(RolePolicies.Staff);

        var customer = app.MapCustomerApi().AddEndpointFilter(PrivateRequest);
        customer.MapGet("/profile", async (HttpContext context, StoreDbContext db, CancellationToken ct) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            var id = long.Parse(context.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var user = await db.Users.AsNoTracking().Include(user => user.Addresses)
                .SingleOrDefaultAsync(user => user.Id == id, ct);
            return user is null ? Results.Unauthorized() : Results.Ok(CustomerProfileDto.FromUser(user));
        });

        // SCRUM-103: the owner corrects name, tax ID and home address. The account always comes from the session;
        // the body cannot name another account (unknown properties are rejected) and the email is not editable.
        customer.MapPut("/profile", async (UpdateCustomerProfileRequest input, HttpContext context, StoreDbContext db, CancellationToken ct) =>
        {
            var id = long.Parse(context.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var user = await db.Users.Include(user => user.Addresses).SingleOrDefaultAsync(user => user.Id == id, ct);
            if (user is null) return Results.Unauthorized();
            // The stored email goes through the same validation, so every field limit stays in CustomerProfileValidation.
            var profile = new CustomerProfileInput(input.Name, user.Email, input.TaxId, input.HomeAddress);
            var errors = CustomerProfileValidation.Validate(profile);
            if (errors.Count != 0) return Results.ValidationProblem(errors);

            var before = (user.Name, user.TaxId, user.HomeAddress);
            CustomerProfileValidation.Apply(user, profile);
            var now = DateTime.UtcNow;
            if ((user.Name, user.TaxId) != (before.Name, before.TaxId)) user.UpdatedAt = now;
            if (user.HomeAddress != before.HomeAddress)
                user.Addresses.Single(address => address.IsDefault && address.IsActive).UpdatedAt = now;
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateConcurrencyException)
            {
                return Results.Problem(statusCode: StatusCodes.Status409Conflict,
                    title: "Your profile was changed at the same time. Reload it and try again.");
            }
            return Results.Ok(CustomerProfileDto.FromUser(user));
        }).AddEndpointFilter<CsrfFilter>();
    }

    private static async Task<IResult> CurrentAccount(ClaimsPrincipal principal, StoreDbContext db, CancellationToken ct)
    {
        if (!long.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id))
            return Results.Unauthorized();
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(user => user.Id == id, ct);
        return user is null ? Results.Unauthorized() : Results.Ok(CurrentAccountDto.FromUser(user));
    }

    private static async ValueTask<object?> PrivateRequest(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        context.HttpContext.Response.Headers.CacheControl = "no-store";
        try { return await next(context); }
        catch (Exception exception) when (DatabaseErrors.IsUnavailable(exception)) { return Unavailable(); }
    }

    private static IResult Unavailable() => Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable,
        title: "The account service is temporarily unavailable.", detail: "Please try again shortly.");
}

public sealed class PrivateResponseMetadata
{
    public static PrivateResponseMetadata Instance { get; } = new();

    private PrivateResponseMetadata() { }
}

public static class PrivateResponseMiddleware
{
    public static IApplicationBuilder UsePrivateResponseNoStore(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            if (context.GetEndpoint()?.Metadata.GetMetadata<PrivateResponseMetadata>() is not null)
                context.Response.Headers.CacheControl = "no-store";
            await next(context);
        });
}
