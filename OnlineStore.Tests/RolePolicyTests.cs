using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OnlineStore.Accounts;

namespace OnlineStore.Tests;

public class RolePolicyTests
{
    public static IEnumerable<object[]> RoleMatrix =>
        from role in new[] { "Customer", "SalesManager", "ProductManager", "SupportAgent", "" }
        from route in new[] { "customer", "sales", "product-management", "support" }
        select new object[] { role, route };

    [Theory]
    [MemberData(nameof(RoleMatrix))]
    public async Task ProtectedGroups_AllowOnlyTheirOwnRole(string role, string route)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddAuthentication("test").AddScheme<AuthenticationSchemeOptions, TestIdentity>("test", _ => { });
        builder.Services.AddAuthorization(RolePolicies.Configure);
        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapCustomerApi().MapGet("/customer-probe", () => "ok");
        app.MapSalesManagerApi().MapGet("/sales-probe", () => "ok");
        app.MapProductManagerApi().MapGet("/product-management-probe", () => "ok");
        app.MapSupportAgentApi().MapGet("/support-probe", () => "ok");
        await app.StartAsync();
        using var client = app.GetTestClient();
        if (role != "") client.DefaultRequestHeaders.Add("X-Test-Role", role);
        var requiredRole = route switch
        {
            "customer" => "Customer", "sales" => "SalesManager", "product-management" => "ProductManager", _ => "SupportAgent"
        };
        var expected = role == "" ? HttpStatusCode.Unauthorized
            : role == requiredRole ? HttpStatusCode.OK : HttpStatusCode.Forbidden;
        Assert.Equal(expected, (await client.GetAsync($"/api/{route}/{route}-probe")).StatusCode);
    }

    private sealed class TestIdentity(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("X-Test-Role", out var role)) return Task.FromResult(AuthenticateResult.NoResult());
            var principal = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, role.ToString()) }, "test"));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, "test")));
        }
    }
}
