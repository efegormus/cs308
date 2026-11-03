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
public sealed record AccountDto(int Id, string Name, string Email, string Role)
{
    public static AccountDto FromUser(User user) => new(user.Id, user.Name, user.Email, user.Role.ToString());
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
            var id = int.Parse(context.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var user = await db.Users.SingleOrDefaultAsync(user => user.Id == id, ct);
            if (user is not null)
            {
                user.SecurityStamp = Guid.NewGuid().ToString("N");
                await db.SaveChangesAsync(ct);
            }
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.NoContent();
        }).RequireAuthorization().AddEndpointFilter<CsrfFilter>();

        auth.MapGet("/me", async (ClaimsPrincipal principal, StoreDbContext db, CancellationToken ct) =>
        {
            var id = int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(user => user.Id == id, ct);
            return user is null ? Results.Unauthorized() : Results.Ok(AccountDto.FromUser(user));
        }).RequireAuthorization();

        // Read-only bootstrap for a management UI; business operations use the role-specific groups.
        app.MapGet("/api/management/session", (HttpContext context) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            return Results.Ok(new { role = context.User.FindFirstValue(ClaimTypes.Role) });
        }).RequireAuthorization(RolePolicies.Staff);

        app.MapCustomerApi().AddEndpointFilter(PrivateRequest).MapGet("/profile", async (HttpContext context, StoreDbContext db, CancellationToken ct) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            var id = int.Parse(context.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(user => user.Id == id, ct);
            return user is null ? Results.Unauthorized() : Results.Ok(CustomerProfileDto.FromUser(user));
        });
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
