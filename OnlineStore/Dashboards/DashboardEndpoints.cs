using System.Security.Claims;
using OnlineStore.Accounts;
using OnlineStore.Data;

namespace OnlineStore.Dashboards;

public static class DashboardEndpoints
{
    private static readonly TimeSpan MaximumSalesRange = TimeSpan.FromDays(366);
    private const int DefaultUserPageSize = 20;
    private const int MaximumUserPageSize = 50;
    private const int MaximumUserPage = 10_000;

    public static void MapDashboardEndpoints(this WebApplication app)
    {
        var customer = app.MapCustomerApi().AddEndpointFilter<DashboardErrorFilter>();
        customer.MapGet("/dashboard", async (ClaimsPrincipal principal, IDashboardService dashboards,
            CancellationToken cancellationToken) =>
        {
            if (!long.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                return Results.Unauthorized();
            var dashboard = await dashboards.GetCustomerAsync(userId, cancellationToken);
            return dashboard is null ? Results.Unauthorized() : Results.Ok(dashboard);
        })
            .WithName("GetCustomerDashboard")
            .WithSummary("Get the signed-in customer's dashboard")
            .Produces<CustomerDashboardResponse>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status503ServiceUnavailable)
            .WithMetadata(PrivateResponseMetadata.Instance);

        var sales = app.MapSalesManagerApi().AddEndpointFilter<DashboardErrorFilter>();
        sales.MapGet("/dashboard", async (DateTimeOffset? from, DateTimeOffset? to,
            IDashboardService dashboards, CancellationToken cancellationToken) =>
        {
            if (from is null || to is null || from.Value >= to.Value
                || to.Value - from.Value > MaximumSalesRange)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["dateRange"] = [$"Provide from and to with from < to and a range no greater than {MaximumSalesRange.TotalDays:0} days."]
                });
            }

            return Results.Ok(await dashboards.GetSalesAsync(from.Value, to.Value, cancellationToken));
        })
            .WithName("GetSalesDashboard")
            .WithSummary("Get sales totals and recent sales activity")
            .Produces<SalesDashboardResponse>()
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status503ServiceUnavailable)
            .WithMetadata(PrivateResponseMetadata.Instance);

        var productManagement = app.MapProductManagerApi().AddEndpointFilter<DashboardErrorFilter>();
        productManagement.MapGet("/dashboard", async (IDashboardService dashboards,
            CancellationToken cancellationToken) =>
                Results.Ok(await dashboards.GetProductManagementAsync(cancellationToken)))
            .WithName("GetProductManagementDashboard")
            .WithSummary("Get low stock, pending deliveries, and comments awaiting approval")
            .Produces<ProductManagementDashboardResponse>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status503ServiceUnavailable)
            .WithMetadata(PrivateResponseMetadata.Instance);

        var management = app.MapManagerApi().AddEndpointFilter<DashboardErrorFilter>();
        management.MapGet("/users", async (int? page, int? pageSize, string? search,
            IDashboardService dashboards, CancellationToken cancellationToken) =>
        {
            var requestedPage = page ?? 1;
            var requestedPageSize = pageSize ?? DefaultUserPageSize;
            var normalizedSearch = search?.Trim();
            if (requestedPage is < 1 or > MaximumUserPage
                || requestedPageSize is < 1 or > MaximumUserPageSize
                || (normalizedSearch is not null
                    && (normalizedSearch.Length is < 2 or > 100)))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["query"] = [$"Use page 1-{MaximumUserPage}, pageSize 1-{MaximumUserPageSize}, and an optional name-prefix search of 2-100 characters."]
                });
            }

            return Results.Ok(await dashboards.ListUsersAsync(requestedPage, requestedPageSize,
                normalizedSearch, cancellationToken));
        })
            .WithName("ListManagementUsers")
            .WithSummary("List safe user summaries for sales and product managers")
            .Produces<ManagementUsersResponse>()
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status503ServiceUnavailable)
            .WithMetadata(PrivateResponseMetadata.Instance);
    }
}

public sealed class DashboardErrorFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        context.HttpContext.Response.Headers.CacheControl = "no-store";
        try { return await next(context); }
        catch (Exception exception) when (DatabaseErrors.IsUnavailable(exception))
        {
            return Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "The dashboard service is temporarily unavailable.");
        }
    }
}
