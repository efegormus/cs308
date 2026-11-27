# Revenue, cost and profit report (SCRUM-152)

`GET /api/sales/reports/profit?from=...&to=...` (SalesManager role, `Cache-Control: no-store`).

`from` and `to` need an explicit UTC offset (`Z` or `+03:00`), `from < to`, and at most 366 days.
Anything else is a 400 validation problem. Anonymous is 401, another role is 403.
The period is half-open `[from, to)`, like `GET /api/sales/dashboard`.

```json
{
  "from": "2026-11-09T00:00:00+00:00", "to": "2026-11-12T00:00:00+00:00", "currencyCode": "TRY",
  "revenue": 200.00, "cost": 120.00, "profit": 80.00,
  "days": [ { "date": "2026-11-09", "revenue": 300.00, "cost": 180.00, "profit": 120.00 } ]
}
```

## Rules

- A day is a UTC day. Every day of the period is listed, with zeros when nothing happened.
- **Revenue** of a day = confirmed payments captured that day minus completed refunds paid that day.
  The total equals `netRevenue` of the sales dashboard for the same period.
- **Cost** of a day = purchase-time cost of the items of the orders paid that day, minus the cost of goods
  given back by refunds paid that day: the returned quantity for a return, the whole order for a cancellation.
- An item's cost is its `UnitCost` snapshot (the product's own cost when it had one). Older orders have no
  snapshot, so the default applies: **50% of the paid price**. Later changes to `Product.Cost` do not change history.
- **Profit** = revenue - cost. A day with only refunds is negative.
- Pending and failed refunds and unconfirmed payments are ignored.

The chart screen of SCRUM-87 uses this endpoint.
