using Microsoft.EntityFrameworkCore;
using OnlineStore.Data;
using OnlineStore.Models;

namespace OnlineStore.Reporting;

// SCRUM-152. Rules (same period and revenue rules as GET /api/sales/dashboard, so the totals agree):
// - Days are UTC days. The period is half-open [from, to). Every day in it appears, with zeros when nothing happened.
// - Revenue of a day = confirmed payments captured that day minus completed refunds paid that day.
// - Cost of a day = purchase-time cost of the items of the orders paid that day, minus the cost of the goods
//   given back by refunds paid that day (a returned quantity, or the whole order for a cancellation).
// - An item's cost is its UnitCost snapshot. Older orders have none, so the default applies: 50% of the paid price.
// - Profit = revenue - cost. A day with only refunds can be negative.
public sealed class ProfitReportService(StoreDbContext db)
{
    public const decimal DefaultCostRatio = 0.5m;
    public const string CancellationReason = "Cancellation";

    public static decimal UnitCostOf(decimal? unitCost, decimal unitPrice) =>
        unitCost ?? Math.Round(unitPrice * DefaultCostRatio, 2);

    public async Task<ProfitReportResponse> GetAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
    {
        var start = from.UtcDateTime;
        var end = to.UtcDateTime;
        var revenue = new Dictionary<DateOnly, decimal>();
        var cost = new Dictionary<DateOnly, decimal>();
        void Add(Dictionary<DateOnly, decimal> map, DateTime moment, decimal amount)
        {
            var day = DateOnly.FromDateTime(moment);
            map[day] = map.GetValueOrDefault(day) + amount;
        }

        var payments = await db.Payments.AsNoTracking()
            .Where(p => p.Status == PaymentStatus.Confirmed && p.OrderId != null && p.ConfirmedAt >= start && p.ConfirmedAt < end)
            .Select(p => new { p.ConfirmedAt, p.Amount }).ToListAsync(ct);
        foreach (var p in payments) Add(revenue, p.ConfirmedAt!.Value, p.Amount);

        var soldItems = await (from p in db.Payments.AsNoTracking()
                join i in db.OrderItems.AsNoTracking() on p.OrderId equals (long?)i.OrderId
                where p.Status == PaymentStatus.Confirmed && p.OrderId != null && p.ConfirmedAt >= start && p.ConfirmedAt < end
                select new { p.ConfirmedAt, i.UnitPrice, i.UnitCost, i.Quantity }).ToListAsync(ct);
        foreach (var i in soldItems) Add(cost, i.ConfirmedAt!.Value, UnitCostOf(i.UnitCost, i.UnitPrice) * i.Quantity);

        var refunds = await db.Refunds.AsNoTracking()
            .Where(r => r.Status == "Completed" && r.RefundedAt >= start && r.RefundedAt < end)
            .Select(r => new { r.RefundedAt, r.Amount }).ToListAsync(ct);
        foreach (var r in refunds) Add(revenue, r.RefundedAt!.Value, -r.Amount);

        var returnedItems = await (from r in db.Refunds.AsNoTracking()
                join q in db.ReturnRequests.AsNoTracking() on r.ReturnRequestId equals q.Id
                join i in db.OrderItems.AsNoTracking() on q.OrderItemId equals i.Id
                where r.Status == "Completed" && r.RefundedAt >= start && r.RefundedAt < end
                select new { r.RefundedAt, i.UnitPrice, i.UnitCost, q.Quantity }).ToListAsync(ct);
        foreach (var i in returnedItems) Add(cost, i.RefundedAt!.Value, -UnitCostOf(i.UnitCost, i.UnitPrice) * i.Quantity);

        var cancelledItems = await (from r in db.Refunds.AsNoTracking()
                join i in db.OrderItems.AsNoTracking() on r.OrderId equals i.OrderId
                where r.Status == "Completed" && r.Reason == CancellationReason && r.ReturnRequestId == null
                    && r.RefundedAt >= start && r.RefundedAt < end
                select new { r.RefundedAt, i.UnitPrice, i.UnitCost, i.Quantity }).ToListAsync(ct);
        foreach (var i in cancelledItems) Add(cost, i.RefundedAt!.Value, -UnitCostOf(i.UnitCost, i.UnitPrice) * i.Quantity);

        var days = new List<ProfitDay>();
        for (var day = DateOnly.FromDateTime(start); day <= DateOnly.FromDateTime(end.AddTicks(-1)); day = day.AddDays(1))
        {
            var r = revenue.GetValueOrDefault(day);
            var c = cost.GetValueOrDefault(day);
            days.Add(new ProfitDay(day, r, c, r - c));
        }
        var totalRevenue = days.Sum(d => d.Revenue);
        var totalCost = days.Sum(d => d.Cost);
        return new ProfitReportResponse(from, to, "TRY", totalRevenue, totalCost, totalRevenue - totalCost, days);
    }
}
