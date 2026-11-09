# SCRUM-119 implementation and migration notes

## Applied strategy

The user's final instruction was to build the **last merged 29-table design**,
reset/reseed locally, and adapt the existing application. We implemented that
scope. The optional `schema-future.sql` was not applied.

This is an explicit **fresh database baseline**, `RedesignedStore`. The old chain
is available in Git at `3acc716`; it is not an upgrade path for existing data.
The native local helper `scripts/redesign_db.py reset --reset-local` backs up the database before recreating it.
Do not run the new baseline over an existing 23-table database.

`schema.sql` is embedded in the migration, rather than loaded from a mutable file
at runtime. EF records the baseline in `__EFMigrationsHistory`. Applying the same
migration twice is a no-op. Never bootstrap with standalone SQL and then attempt
to apply the same EF baseline on top.

## Old → new mapping

| Previous application data | New storage / application behavior |
| --- | --- |
| INT IDs | BIGINT UNSIGNED; C# `long` plus unsigned provider conversion |
| Users.Email | AES-GCM `EmailEnc`; login and duplicate detection use `EmailBlindIndex` |
| Users.TaxId | AES-GCM `TaxIdEnc`; customers still require a real value |
| Users.HomeAddress | Default active row in Addresses; profile API keeps `homeAddress` |
| Categories | ParentId, unique Slug and sibling-name uniqueness; seed has one root and seven leaves |
| Products.DistributorInformation | Distributors + DistributorId; detail API keeps distributorInformation |
| Products.ImageUrl | Primary ProductImages row; existing API keeps imageUrl |
| New catalog metadata | Brands, AttributeDefinitions and ProductAttributeValues |
| Orders snapshots | Renamed customer snapshot columns; e-mail/tax/address encrypted |
| Orders totals | Subtotal, DiscountTotal, ShippingTotal, GrandTotal; populated at checkout |
| OrderItems.UnitPrice | PaidUnitPrice; CLR alias kept for existing purchase/invoice code |
| OrderItems | List price, discount rate, SKU and line total snapshots; allocation/delivery/return/refund counters |
| Payments | User/cart ownership; nullable OrderId; failed bank attempts persist without an order |
| Payment/refund idempotency keys | Binary 32-byte hashes; existing CLR strings are hex representations |
| Invoices.ProtectedDocumentKey | PdfStorageKey plus PdfSha256; PDF is written before the invoice row |
| InvoiceEmailDeliveries | EmailOutbox, linked to an issued invoice, with persisted status and dedup key |
| Invoice.EmailedAt | Derived from the successful outbox delivery; no duplicate database column |
| Outbox lease/retry fields | LeaseOwner/LeaseExpiresAt and LastError; CLR aliases preserve the dispatcher structure |
| Deliveries.DeliveredAt | CompletedAt; UserId binds the delivery to the order owner |
| DeliveryItems | Composite order references prevent attaching another order's line |
| Comments.ApprovalStatus | Status; existing CLR enum name retained |
| Conversations.CartId | Not stored; authenticated customer context is obtained through CustomerUserId |
| Notifications | Title, Body, DiscountId and ReadAt; in-app notices do not store SentAt |

## Necessary difference from the proposal

`EmailOutbox.LastAttemptAt DATETIME(6) NULL` was added to preserve retry diagnostics.
The original notes explicitly identified this gap. The data dictionary, SQL and
migration now agree. No optional future-extension table was introduced.

The EF model cannot use nullable composite alternate keys for all parents.
`Payments(CartId, UserId) → Carts(Id, UserId)` and
`Refunds(PaymentId, OrderId) → Payments(Id, OrderId)` remain enforced by the SQL
baseline. The latter has a simpler EF navigation; do not weaken its SQL
constraint in later migrations. Other composite alternate keys use their actual
SQL `UX_*` constraint names.

## Application changes that matter

- Account/profile DTOs retain their public field names. Encryption happens in EF
  value converters. Search by plaintext e-mail was replaced with HMAC lookup.
- Personal-data keys are required for relational contexts and remain outside the
  repository/database. Tests create isolated temporary keys.
- Cart mutations and checkout retain their existing row locks. Unique OpenUserId
  adds database protection against multiple open carts.
- Checkout fills list/paid prices, totals, allocation counters and safe card
  metadata. PAN, CVV and expiry are discarded. A declined attempt survives the
  rolled-back checkout; retrying that same key returns the same decline.
- Delivery completion updates delivered counters with the order/delivery state.
- Invoice creation stores the PDF hash; email enqueue follows invoice issuance.
  Reconciliation repairs paid orders that were interrupted before invoice/outbox
  completion. Workers use leases, fencing tokens and row locks.
- Product projections read normalized distributor/image relationships. Category
  filtering includes descendants. Substring search remains compatible with the
  existing API and normalizes Turkish dotted/dotless i; FULLTEXT exists in the
  schema but is not used by the current substring endpoint.
- Popularity subtracts refunded quantities from confirmed, noncancelled sales.
- Browser ID validators accept JavaScript-safe integer IDs rather than the old
  nine-digit / signed-INT ceilings. Numeric route compatibility is retained.

## Seed and demo

The catalog seed contains 8 categories, 25 products, 6 brands, one distributor,
21 attribute definitions, 75 typed values and 25 primary images. Prices,
quantities, serial numbers and technical specifications are fictional examples.

`--seed` adds missing catalog rows and does not overwrite existing inventory.
`--seed-demo` additionally creates four role accounts, three order states,
invoices/outbox entries, a delivered review, a selective return/refund, a
notification, wishlist and support conversation/attachment. It uses the actual
account, checkout, delivery and invoice services; the future feature examples are
inserted as valid demonstration records. It does not implement their missing UI.

The local reset command backs up first, recreates the application database,
applies the baseline and reseeds it. Existing local key/credential files are
retained so demo passwords and ciphertext remain usable across resets.
