using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using OnlineStore.Data;

namespace OnlineStore.Accounts;

public sealed class SessionEvents(StoreDbContext db) : CookieAuthenticationEvents
{
    public const string StampClaim = "security_stamp";

    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        if (!int.TryParse(context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id))
        {
            context.RejectPrincipal();
            return;
        }
        try
        {
            var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(user => user.Id == id, context.HttpContext.RequestAborted);
            if (user is not null && !string.IsNullOrEmpty(user.SecurityStamp)
                && context.Principal!.FindFirstValue(StampClaim) == user.SecurityStamp
                && context.Principal.FindFirstValue(ClaimTypes.Role) == user.Role.ToString()) return;
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
        catch (Exception exception) when (DatabaseErrors.IsUnavailable(exception))
        {
            // A database outage must never authorize stale staff privileges.
            context.RejectPrincipal();
        }
    }

    public override Task RedirectToLogin(RedirectContext<CookieAuthenticationOptions> context)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    }

    public override Task RedirectToAccessDenied(RedirectContext<CookieAuthenticationOptions> context)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    }
}
