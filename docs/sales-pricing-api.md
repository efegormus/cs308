# Sales manager pricing and discount API (SCRUM-155)

Requirement 11: the sales manager sets product prices and discounts. When a discount
is created for a list of products, the new price takes effect on its own.

All routes live under `/api/sales`, so only the `SalesManager` role reaches them
(`RolePolicies.MapSalesManagerApi`). The service checks the role again, and writes
need the CSRF header like every other write in the project. Errors are
`ProblemDetails` (400 invalid input, 401 log in again, 403 wrong role, 404 unknown id).

## How the price changes

`Product.Price` is the **list price**. A discount never edits it. A campaign is a
`Discounts` row plus `DiscountProducts` links, and every shopping read and checkout
already calculate the price payable through `ProductPricing` ([pricing.md](pricing.md)):

- a campaign applies while it is not cancelled, `StartsAt <= now` and `EndsAt > now` (UTC);
- overlapping campaigns never stack: the highest single rate wins;
- past orders keep their paid-price and discount snapshots, so editing a price or
  cancelling a campaign never changes history.

So "set the new price" means creating the campaign. Nothing needs a migration.

## Routes

| Route | Purpose |
|---|---|
| `GET /api/sales/products?afterId=` | Products with list price, price payable now and active rate. 50 per page, `nextAfterId` is the cursor. |
| `PUT /api/sales/products/{id}/price` | Body `{ "price": 149.90 }`. Changes the list price (above 0, at most 9999999.99, 2 decimals). Returns the product row. |
| `GET /api/sales/discounts?beforeId=` | Campaigns, newest first, 50 per page, `nextBeforeId` is the cursor. |
| `POST /api/sales/discounts` | Creates a campaign, `201` with the campaign. |
| `POST /api/sales/discounts/{id}/cancel` | Cancels a campaign. Idempotent. |

### Create a discount

```json
{
  "name": "Black Friday",
  "rate": 0.2,
  "startsAt": "2026-11-27T00:00:00Z",
  "endsAt": "2026-11-30T23:59:59Z",
  "productIds": [3, 7, 12]
}
```

- `name`: 1 to 100 characters, trimmed.
- `rate`: above 0 and below 1, at most 4 decimals. `0.2` means 20% off.
- `startsAt`: optional, defaults to now. A future value schedules the campaign.
- `endsAt`: required, after `startsAt` and in the future.
- `productIds`: 1 to 200 distinct ids of existing, **active** products. If any id is
  unknown or inactive the whole request is rejected and nothing is stored.

The response has `status`: `Scheduled`, `Active`, `Expired` or `Cancelled`, computed
from the clock and `CancelledAt`.

### Cancel a discount

Sets `CancelledAt`, so the list price applies again at once. A campaign that already
ended stays `Expired` and is returned unchanged; cancelling twice returns the first
cancellation time.

## Not in this task

- The React screen is SCRUM-156.
- Notifying wishlist users about a discount is SCRUM-85. It will hook into
  `PricingService.CreateDiscountAsync` using the existing `WishlistDiscount` notification kind.
- Revenue, cost and profit reporting is SCRUM-152.

## Tests

`PricingServiceTests` (in-memory database) covers request validation, the role guard,
the product list and paging, list price changes, creating, scheduling, cancelling and
listing campaigns, overlapping rates and the status calculation.

## Wishlist discount notifications (SCRUM-85)

When `POST /api/sales/discounts` creates a campaign, `WishlistDiscountNotifier` looks up the active
customers who have a discounted product on their wishlist.

- Each (customer, product) pair gets one `Notification` of kind `WishlistDiscount` with the discount id.
- Each customer gets one email that lists their discounted products (at most 10 names, then "and N more").
- `DeduplicationKey` is `wishlist-discount:{discountId}:{productId}:{userId}`. Running the notifier again
  for the same discount stores and sends nothing new.
- Email goes through `IDiscountEmailSender`, chosen by the same `Email:Transport` setting (`Local` or `Smtp`)
  and `Email:From` / `Email:Smtp:*` values as the invoice emails. Addresses and message text are never logged.
- A mail failure is logged without the address and does not fail the discount; the in-app notification stays.
- The email is sent when the campaign is created, also for a campaign that starts later.
