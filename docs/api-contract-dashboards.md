# Dashboard and management API contract (SCRUM-121)

These are read-only, cookie-authenticated APIs. JSON property names are camelCase.
Successful responses and handled validation/database failures carry
`Cache-Control: no-store`; authorization failures contain no account data. The
APIs reuse the existing server-validated session and role policies; they do not
accept a user ID to select the customer whose data is returned.
Runnable request examples are in [`OnlineStore/Dashboards.http`](../OnlineStore/Dashboards.http).

## Endpoints and access

| Method and path | Required role | Purpose |
| --- | --- | --- |
| `GET /api/me` | Any authenticated user | Current account summary |
| `GET /api/customer/dashboard` | `Customer` | Own recent orders, wishlist preview, and basic profile |
| `GET /api/sales/dashboard` | `SalesManager` | Revenue, recent invoices, and active discounts |
| `GET /api/product-management/dashboard` | `ProductManager` | Low stock, pending deliveries, and pending comments |
| `GET /api/management/users` | `SalesManager` or `ProductManager` | Paged safe user summaries |

`GET /api/auth/me` remains available for existing clients and returns the same
account summary as `GET /api/me`. The latter is an alias to the same handler,
not a second identity implementation.

## `GET /api/me`

Returns `200 OK` from the account row identified by the authenticated session:

```json
{
  "id": 42,
  "name": "Ada Customer",
  "email": "ada@example.com",
  "role": "Customer",
  "roles": ["Customer"]
}
```

Only the user's current role is included in `roles`. Password hashes, security
stamps, tax IDs, addresses, and other private fields are never returned.

## `GET /api/customer/dashboard`

Returns only data owned by the authenticated customer. Recent confirmed orders
are newest first and limited to five. Wishlist count covers the customer's
wishlist; the product preview is limited to five active products.

```json
{
  "profile": { "id": 42, "name": "Ada Customer", "email": "ada@example.com" },
  "recentConfirmedOrders": [
    {
      "orderId": 801,
      "status": "InTransit",
      "paymentStatus": "Confirmed",
      "purchasedAt": "2026-11-09T12:00:00Z",
      "deliveredAt": null,
      "itemCount": 2,
      "total": 1250.00,
      "invoiceStatus": "Ready",
      "invoiceUrl": "/api/customer/orders/801/invoice"
    }
  ],
  "wishlistCount": 8,
  "wishlistItems": [
    { "id": 15, "name": "Example product", "price": 499.00, "stockQuantity": 3, "imageUrl": null }
  ]
}
```

The profile excludes tax and address details. Orders reuse the existing
customer-owned confirmed-order history and its status/payment/invoice semantics.

## `GET /api/sales/dashboard`

Required query parameters are `from` and `to`, ISO-8601 timestamps with an
explicit UTC offset (for example `2026-11-01T00:00:00Z`). The period is the
half-open interval `[from, to)` and must be positive and no longer than 366 days.

Gross revenue is the sum of confirmed payment amounts whose `ConfirmedAt` falls
in the interval. Refunds are completed refund amounts whose `RefundedAt` falls
in the interval. `netRevenue` is gross revenue minus completed refunds for that
interval; refund events can therefore make a period's net revenue negative.
Pending and failed payments/refunds do not affect these totals. Amounts use TRY.

The response includes up to ten latest invoices (without customer/tax snapshots)
and up to ten discounts that have not been cancelled and whose active interval
contains the current UTC time.

```json
{
  "from": "2026-11-01T00:00:00Z",
  "to": "2026-11-10T00:00:00Z",
  "currencyCode": "TRY",
  "grossRevenue": 1000.00,
  "refunds": 100.00,
  "netRevenue": 900.00,
  "recentInvoices": [
    { "orderId": 801, "invoiceNumber": "INV-2026-000801", "issuedAt": "2026-11-09T12:00:00Z", "total": 1000.00, "currencyCode": "TRY" }
  ],
  "activeDiscounts": [
    { "id": 4, "name": "November", "rate": 0.10, "startsAt": "2026-11-01T00:00:00Z", "endsAt": "2026-12-01T00:00:00Z", "productCount": 3 }
  ]
}
```

## `GET /api/product-management/dashboard`

The response is bounded: at most 20 low-stock products, 20 pending deliveries,
and 10 comments waiting for approval. Low stock means an active product with
`stockQuantity <= 5`. Pending deliveries use the schema's non-terminal statuses
`Processing` and `InTransit`; terminal `Delivered` and `Cancelled` rows are not
included. Only comments with schema status `Pending` are returned; comment text
is capped at 1,000 characters.

```json
{
  "lowStockProducts": [
    { "id": 15, "sku": "EXAMPLE-15", "name": "Example product", "stockQuantity": 2 }
  ],
  "pendingDeliveries": [
    { "id": 21, "orderId": 801, "status": "InTransit", "createdAt": "2026-11-09T12:00:00Z" }
  ],
  "commentsWaitingForApproval": [
    { "id": 31, "productId": 15, "productName": "Example product", "body": "Customer review", "createdAt": "2026-11-09T13:00:00Z" }
  ]
}
```

## `GET /api/management/users`

Only `SalesManager` and `ProductManager` can list users; `SupportAgent` and
customers cannot. Optional query parameters:

| Parameter | Default | Limits |
| --- | --- | --- |
| `page` | `1` | `1`-`10000` |
| `pageSize` | `20` | `1`-`50` |
| `search` | none | 2-100 characters after trimming; name prefix only |

Search is parameterized and matches the plaintext name only. Email is encrypted
with a blind index and is intentionally not searched using plaintext `LIKE`.
The response does not include email, tax ID, address, password hash, or security
stamp.

```json
{
  "page": 1,
  "pageSize": 20,
  "totalCount": 1,
  "users": [
    { "id": 42, "name": "Ada Customer", "role": "Customer", "isActive": true, "createdAt": "2026-11-09T12:00:00Z" }
  ]
}
```

## Errors and authorization

| Status | Meaning |
| --- | --- |
| `200 OK` | Successful read, including empty dashboard lists |
| `400 Bad Request` | Missing/invalid sales range or invalid pagination/search values |
| `401 Unauthorized` | No valid authenticated session, or the session's account no longer exists |
| `403 Forbidden` | Authenticated session does not have the endpoint's required role |
| `503 Service Unavailable` | Database unavailable; response does not include provider details |

The session is checked against the database, including active account, role, and
security stamp. Customer dashboard ownership comes only from the session's
user ID. No endpoint accepts a customer ID, and manager listings are limited to
the two manager roles above.

## Support-agent dashboard placeholder

`SupportAgent` has no dashboard endpoint in this contract. The role is
intentionally denied access to customer, sales, product-management, and manager
dashboards and to the manager user list. Support-agent workflows are outside
SCRUM-131; this placeholder does not introduce an endpoint or grant dashboard
data access.
