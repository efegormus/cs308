# Effective prices (SCRUM-147)

`Product.Price` is the list price. Every current shopping read uses
`ProductPricing.Query`: product lists/detail, legacy catalog, wishlist and its
dashboard preview, guest/customer cart. `price` / `unitPrice` remain the current
amount payable; `listPrice` / `listUnitPrice` and `discountRate` explain a sale.
React renders a struck-through original price only when the paid amount is lower.
Price filtering, ordering and pagination operate on the same effective price.

A campaign applies when it is not cancelled, `StartsAt <= now` and `EndsAt > now`,
in UTC. The highest single active rate wins; overlapping discounts never stack.
Checkout uses the same active-link query and monetary calculation. Equal best
rates use the existing lowest discount-ID snapshot tie-breaker. For non-negative
money, `floor(listPrice * (1-rate) * 100 + .5) / 100` gives the same exact decimal
away-from-zero cents in both MySQL and .NET (including half-cent boundaries).

Each request uses a single server timestamp. A campaign can expire between reads;
the checkout quote is the review step, and a changed quote returns 409 before
payment/stock finalization. Retry the same successful attempt key to recover the
original order, not a recalculated purchase. No current-price read changes any
historical paid-price, discount, cost, invoice or refund snapshot.

No migration is required. Existing `Price`, campaigns and purchase snapshots are
retained. The added list-price/rate fields are explanatory and do not authorize
client prices or totals. The legacy undiscounted DTO mapping expressions remain
available for fixtures; HTTP routes use the priced mapping.

`ProductPricingTests` covers UTC campaign boundaries, cancellations and decimal
rounding. `PricingMySqlTests` checks all shopping surfaces, effective-price
sorting/filtering/paging, overlapping/future/expired/cancelled campaigns, changed
quote rejection with unchanged stock, and immutable snapshots after campaign and
product changes plus a repeated checkout. Run with the existing guarded local
`ONLINESTORE_TEST_MYSQL` connection; skipped SQL tests are not verification.
