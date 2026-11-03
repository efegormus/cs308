using Microsoft.AspNetCore.Authorization;
using OnlineStore.Models;

namespace OnlineStore.Accounts;

public static class RolePolicies
{
    public const string Customer = "Customer";
    public const string SalesManager = "SalesManager";
    public const string ProductManager = "ProductManager";
    public const string SupportAgent = "SupportAgent";
    public const string Staff = "Staff";

    public static void Configure(AuthorizationOptions options)
    {
        options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
        foreach (var role in Enum.GetNames<UserRole>())
            options.AddPolicy(role, policy => policy.RequireAuthenticatedUser().RequireRole(role));
        options.AddPolicy(Staff, policy => policy.RequireAuthenticatedUser()
            .RequireRole(SalesManager, ProductManager, SupportAgent));
    }

    // Feature owners attach their endpoints to these groups so each operation inherits its role guard.
    public static RouteGroupBuilder MapCustomerApi(this IEndpointRouteBuilder app) =>
        app.MapGroup("/api/customer").RequireAuthorization(Customer);
    public static RouteGroupBuilder MapSalesManagerApi(this IEndpointRouteBuilder app) =>
        app.MapGroup("/api/sales").RequireAuthorization(SalesManager);
    public static RouteGroupBuilder MapProductManagerApi(this IEndpointRouteBuilder app) =>
        app.MapGroup("/api/product-management").RequireAuthorization(ProductManager);
    public static RouteGroupBuilder MapSupportAgentApi(this IEndpointRouteBuilder app) =>
        app.MapGroup("/api/support").RequireAuthorization(SupportAgent);
}
