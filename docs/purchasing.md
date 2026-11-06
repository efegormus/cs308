# Checkout and delivery integration (SCRUM-72 / SCRUM-79)

The checkout, confirmation and ProductManager delivery pages are `/checkout`,
`/order-confirmation?orderId=123` and `/deliveries`. They use the existing
same-origin cookie session, role policies, CSRF header and storefront styling.
Money is TRY. Apply the normal migration chain before running the application.

## HTTP contract

All responses are private (`Cache-Control: no-store`). Customer identity comes
from the authenticated session. Unknown input properties are rejected. Mutations
require `X-CSRF-TOKEN` obtained from `/api/auth/csrf` for the current identity.

| Method and path | Role | Result |
| --- | --- | --- |
| `GET /api/customer/checkout` | Customer | Open cart, original profile/address, items, server-calculated prices, total, `quoteToken` |
| `POST /api/customer/checkout` | Customer | 201 purchase receipt; 200 when replaying a completed attempt |
| `GET /api/customer/checkout/attempts/{UUID}` | Customer | Recover the owner's completed attempt after a lost response; 404 otherwise |
| `GET /api/customer/orders/{id}` | Customer | Owner-only order snapshot and current payment/fulfillment; 404 for another owner |
| `GET /api/product-management/deliveries?afterId=0` | ProductManager | Up to 50 deliveries, snapshot lines/totals/address and `nextAfterId` |
| `POST /api/product-management/deliveries/{id}/status` | ProductManager | Guarded transition and refreshed version/view |

Checkout input:

```json
{
  "cartId": 123,
  "idempotencyKey": "00000000-0000-0000-0000-000000000000",
  "quoteToken": "<64-character token from the latest checkout GET>",
  "payment": {
    "number": "4242424242424242",
    "expiryMonth": 12,
    "expiryYear": 2030,
    "securityCode": "123"
  }
}
```

`quoteToken` detects changed cart quantities, paid prices/discounts, product names
or customer details. It is not a trusted total or an authorization credential.
The server always reads current cart ownership, product prices and inventory.
Overlapping active discounts use the highest single rate, without stacking;
paid prices round to two decimals away from zero. Explicit product cost wins,
otherwise the line snapshots 50% of its actual paid price. A stale quote is 409
and requires another review, not an automatic payment retry.

Missing/invalid inputs return 400; a mock decline returns 402; missing ownership
returns 404; invalid stock/state/profile/quote returns 409. Database unavailability
returns a generic 503 without SQL or credentials. Anonymous/incorrect roles use
the existing 401/403 responses.

## Transaction and retry rules

1. Begin a MySQL ReadCommitted transaction and lock the customer row.
2. Look up the owner-scoped, SHA-256-hashed UUID in Payments. A completed attempt
   returns its existing order, even if the profile/prices later change. Reusing
   that key with a different cart is a conflict.
3. Lock the owned Open cart, its lines and product rows in ascending product ID
   order. Reject empty, unavailable or over-stock carts without deleting selections.
4. Recalculate the quote and call `ICheckoutPaymentGateway` with the authoritative
   amount. The supplied gateway is a deterministic **mock**, with no bank call.
5. Conditionally decrement stock (`StockQuantity >= quantity`), snapshot order,
   customer/address, paid prices, costs and payment, create one whole-order
   delivery, mark the cart CheckedOut and remove its purchased lines.
6. Save and commit once. Any failure rolls back every write. A unique nullable
   `Orders.SourceCartId` prevents a second purchase of the same cart with a new key.

`LinkCheckoutToSourceCart` adds only the nullable source-cart FK and unique index;
existing orders remain valid with null. Purchased source carts are retained and
cannot be deleted while referenced by order history. Rollback removes this link;
do not downgrade a database with new purchases without a backup/reconciliation.

Concurrent requests for the same identity serialize before payment. Competing
customers lock stock rows, so the final unit has one winner. The mock reference
is deterministic for the attempt. It contains no card data. Request DTOs, card
number and security code must never be logged/persisted/echoed. The UI clears
number/code after every attempt, retaining only cart ID, quote hash and UUID in
sessionStorage. Network/timeout/503 retries keep the same UUID; a page reload
first checks the attempt recovery endpoint. Storage-disabled browsers retain the
key for the lifetime of the page; the checked-out cart still cannot be bought twice.

Test card `4242424242424242` succeeds; `4000000000000002` declines. Use a future
expiry and a 3/4-digit numeric security code. The gateway validates bounded numeric
input and checksum; it does not contact a bank. SCRUM-73 can replace the scoped
`ICheckoutPaymentGateway` registration. A real charging provider would require a
separate durable reservation/payment recovery design, not a network charge inside
this local transaction.

## Fulfillment rules

The delivery API accepts `{ "status": "InTransit", "version": "<current version>" }`
then `Delivered` with the refreshed version. It locks the order before its delivery
and updates both in the same transaction through `Order.TransitionTo`. A stale
version, skipped/reversed transition, unpaid order, cancelled/delivered terminal
state or inconsistent legacy shipment is rejected. There is no endpoint that merely
sets cancellation without reversing payment/inventory; cancellation remains its
separate feature.

The MVP creates a single full-order shipment. Legacy split shipments, missing
snapshots or mismatched shipment lines require reconciliation and show no action
buttons. Delivery IDs, customer/product IDs, quantities, original paid totals,
address and completion state come from purchase/delivery snapshots. Updating the
profile or current product cannot rewrite these values. Customer receipt reads
show the same order status immediately after the manager changes it.

## Integration responsibilities

- **SCRUM-49/69/70/100:** populate the existing customer-owned Open cart. Guest
  selections must be merged on login before navigating to checkout. Checkout
  does not invent/claim a guest cart. Cart mutation/merge writers must lock the
  parent cart before changing lines and require `State == Open`. When also locking
  the customer, lock customer first, then cart, matching checkout. Retain selections
  and explicitly report stock conflicts. Multiple Open carts produce a clear
  merge-required conflict rather than silently discarding one.
- **SCRUM-73:** reuse/replace the gateway seam and mock form; never supply a
  browser-controlled paid flag or introduce a second order finalization path.
- **SCRUM-74:** invoice PDFs are implemented, see [invoices](invoices.md). Checkout
  commits the purchase first and then issues the invoice; a failed generation leaves
  `invoiceStatus: Pending` and the next order read or download retries it. The
  response never returns the protected storage key. Generation retries never re-run
  checkout or decrement stock again.
- **SCRUM-75:** after commit, enqueue one invoice email using the purchase-time email snapshot.
  Queue failures never fail the purchase response; the worker reconciles missing rows.
  Durable leases and capped transient retries operate only on email delivery, never checkout/payment.
  Permanent/configuration failures stop for manual intervention. Existing emailed invoices are skipped;
  a poison historical record cannot block dispatch of valid pending deliveries.
  See [invoice email configuration and lifecycle](invoices.md#invoice-email-delivery-scrum-75).
- **SCRUM-76:** reuse the owner-only order-detail response and shared confirmation
  components for history; avoid registering the same detail route twice. List
  history is still SCRUM-76.

## Verification

`PurchasingMySqlTests` runs only on a disposable local `*_tests` MySQL database,
using the existing fixture and CI MySQL job. It covers authoritative discounted
prices/costs, historical snapshots, decline/stale-quote rollback, injected save
failure after stock updates, ownership/key rebinding, simultaneous duplicate
requests and competing last-unit checkouts. Real cookie/CSRF HTTP tests verify
role isolation, unknown-price/owner/paid-field rejection and owner-only recovery.
Delivery tests verify ordered transitions, stale versions, unpaid rejection and
receipt visibility. `MockPaymentTests` checks bounded input and safe mock results.

The UI was also exercised in Chrome against real MySQL with isolated temporary
accounts/cart/product: login return URL, mobile checkout/confirmation, literal
HTML-like snapshot text, decline without stock loss, successful purchase, manager
shipping/delivery reflected in the owner receipt, and recovery after deliberately
losing the successful POST response. Temporary fixture rows were removed afterwards.

The React pages and ASP.NET hosting are documented in [react-hosting.md](react-hosting.md).
