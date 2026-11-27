using OnlineStore.Accounts;
using OnlineStore.Dashboards;

namespace OnlineStore.Reporting;

public static class ProfitReportEndpoints
{
    public static readonly TimeSpan MaximumRange = TimeSpan.FromDays(366);

    public static void MapProfitReportEndpoints(this WebApplication app)
    {
        var sales = app.MapSalesManagerApi().AddEndpointFilter<DashboardErrorFilter>();
        sales.MapGet("/reports/profit", async (DateTimeOffset? from, DateTimeOffset? to, HttpRequest request,
            ProfitReportService reports, CancellationToken ct) =>
        {
            if (from is null || to is null || from.Value >= to.Value || to.Value - from.Value > MaximumRange
                || !HasExplicitOffset(request.Query["from"].ToString()) || !HasExplicitOffset(request.Query["to"].ToString()))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["dateRange"] = [$"Provide from and to with explicit UTC offsets, from < to, and a range no greater than {MaximumRange.TotalDays:0} days."]
                });
            }
            return Results.Ok(await reports.GetAsync(from.Value, to.Value, ct));
        })
            .WithName("GetProfitReport")
            .WithSummary("Get revenue, cost and profit per day")
            .Produces<ProfitReportResponse>()
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status503ServiceUnavailable)
            .WithMetadata(PrivateResponseMetadata.Instance);
    }

    private static bool HasExplicitOffset(string value)
    {
        value = value.Trim();
        if (value.EndsWith('Z') || value.EndsWith('z')) return true;

        var offsetStart = value.Length - 6;
        return offsetStart >= 0
            && value[offsetStart] is '+' or '-'
            && value[offsetStart + 1] is >= '0' and <= '9'
            && value[offsetStart + 2] is >= '0' and <= '9'
            && value[offsetStart + 3] == ':'
            && value[offsetStart + 4] is >= '0' and <= '9'
            && value[offsetStart + 5] is >= '0' and <= '9';
    }
}
