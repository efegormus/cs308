namespace OnlineStore.Reporting;

// SCRUM-152: daily revenue, cost and profit for the sales manager.
public sealed record ProfitDay(DateOnly Date, decimal Revenue, decimal Cost, decimal Profit);

public sealed record ProfitReportResponse(DateTimeOffset From, DateTimeOffset To, string CurrencyCode,
    decimal Revenue, decimal Cost, decimal Profit, IReadOnlyList<ProfitDay> Days);
