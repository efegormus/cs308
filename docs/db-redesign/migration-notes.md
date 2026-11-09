# Migration notes: current schema -> proposed schema

For SCRUM-119 (EF Core migrations). "Current" = `main` at `8533156` (23 tables, read from `OnlineStore/Models`).
"Proposed" = `schema.sql` (29 tables). Nothing in the repository has been changed by this design.

## 1. Recommendation: reset and reseed, do not convert data

The only data in the development and CI databases is seed and test data (SCRUM-120 will reseed it). Converting it is risky and
buys nothing: several new `NOT NULL` columns and `CHECK` rules cannot be satisfied by old rows without inventing values
(see section 4), and the existing docs already say "nothing is invented for legacy rows".

So: a new migration chain that builds the proposed schema on an empty MySQL, plus a documented "drop and recreate the
development database, then run `--seed`" path. If a real upgrade of existing rows is ever needed, the column mapping below is
the starting point.

## 2. Tables

| Current table | Proposed table | Change |
|---|---|---|
| Users | Users | changed (encryption, new columns) |
| Categories | Categories | changed (tree, slug) |
| Products | Products | changed (distributor, image, brand, search) |
| Carts, CartItems, WishlistItems | same names | small changes |
| Orders, OrderItems | same names | changed (totals, snapshots, counters) |
| Discounts, DiscountProducts | same names | small changes |
| Payments | Payments | changed (cart link, card brand/last 4) |
| Invoices | Invoices | changed (totals, encrypted snapshots) |
| InvoiceEmailDeliveries | **EmailOutbox** | renamed and reshaped |
| Deliveries, DeliveryItems | same names | small changes |
| Ratings, Comments | same names | small changes |
| ReturnRequests, Refunds | same names | small changes |
| Notifications | Notifications | small changes |
| Conversations, ChatMessages, ChatAttachments | same names | small changes |
| (none) | **Addresses, Distributors, Brands, AttributeDefinitions, ProductAttributeValues, ProductImages** | 6 new tables |

23 current tables + 6 new = 29. No table is dropped.

## 3. Column mapping

Key type: every `Id` and foreign key changes from `INT` to `BIGINT UNSIGNED`.

| Table | Current -> proposed |
|---|---|
| **Users** | `Email` -> `EmailEnc` + `EmailBlindIndex` (HMAC-SHA256 of the normalised e-mail; login looks up the blind index). `TaxId` -> `TaxIdEnc` (null allowed for staff, required for customers). `HomeAddress` -> a row in `Addresses` (`Label` = 'Home', `IsDefault` = 1). `Name`, `PasswordHash`, `Role`, `SecurityStamp`, `CreatedAt` unchanged. New: `PublicId`, `DataKeyVersion`, `IsActive`, `FailedLoginCount`, `LockoutUntil`, `UpdatedAt`, `RowVersion` |
| **Categories** | `Name` is unique per parent instead of globally. New: `ParentId`, `Slug` (required, unique), `SortOrder`, `IsActive`, `CreatedAt` |
| **Products** | `DistributorInformation` -> `Distributors` (one row per distinct text; `Name` and `ContactInfo`) + `Products.DistributorId`. `ImageUrl` -> a `ProductImages` row (`IsPrimary` = 1, `StorageKey` = the URL). `Description` becomes `NOT NULL`. `StockQuantity` becomes `INT UNSIGNED`. New: `BrandId` (nullable), `SearchText` (computed), `CreatedAt`, `UpdatedAt`, `RowVersion`. Other columns unchanged |
| **Carts** | New: `RowVersion`, computed `OpenUserId` (unique: one open cart per user). Old data with several open carts for one user must be merged first |
| **CartItems, WishlistItems** | unchanged (`Quantity` is unsigned) |
| **Orders** | `CustomerNameSnapshot` -> `CustomerName`; `EmailSnapshot` -> `CustomerEmailEnc`; `TaxIdSnapshot` -> `CustomerTaxIdEnc`; `AddressSnapshot` -> `ShippingAddressEnc` (encrypted, now `NOT NULL`). `PaymentStatus` value `Unknown` is removed. New: `OrderNumber`, `CurrencyCode`, `Subtotal`, `DiscountTotal`, `ShippingTotal`, `GrandTotal`, `CancelledAt`, `CancelReason`, `DataKeyVersion` |
| **OrderItems** | `UnitPrice` -> `PaidUnitPrice`. New: `UserId` (copy of the order owner, used by composite FKs), `Sku` (snapshot), `ListUnitPrice`, `DiscountRate` (from the discount), `LineTotal`, and four counters: `DeliveryAllocatedQuantity`, `DeliveredQuantity`, `ReturnRequestedQuantity`, `RefundedQuantity`. `UnitCost` becomes `NOT NULL` |
| **Discounts** | New: `Name`, `CreatedByUserId` (`NOT NULL`), `CancelledAt`, `CreatedAt`. `DiscountProducts` unchanged |
| **Payments** | `OrderId` becomes nullable (set when confirmed). `IdempotencyKey` -> `IdempotencyKeyHash` (`BINARY(32)`). New: `UserId`, `CartId`, `CardBrand`, `CardLast4`, `CurrencyCode`, `FailureCode` |
| **Invoices** | `CustomerNameSnapshot` -> `CustomerName`; `TaxIdSnapshot` -> `CustomerTaxIdEnc`; `AddressSnapshot` -> `BillingAddressEnc`; `ProtectedDocumentKey` -> `PdfStorageKey`. `EmailedAt` is dropped (read it from `EmailOutbox`). New: `Subtotal`, `DiscountTotal`, `ShippingTotal`, `GrandTotal`, `CurrencyCode`, `PdfSha256`, `DataKeyVersion` |
| **InvoiceEmailDeliveries -> EmailOutbox** | `OrderId` -> `InvoiceId` (via `Invoices.OrderId`). `RecipientSnapshot` -> `RecipientEmailEnc` + `RecipientUserId`. `LeaseUntil` -> `LeaseExpiresAt`; `LeaseToken` -> `LeaseOwner`; `LastErrorCode` -> `LastError`; `StoppedAt` -> `Status` = 'Failed'. `AttemptCount`, `NextAttemptAt`, `SentAt`, `CreatedAt` unchanged. New: `DedupKey` (for example `invoice:{InvoiceId}`), `Kind` ('InvoiceEmail' or 'RefundNotice'), `Status`. **`LastAttemptAt` has no column yet** (see section 4) |
| **Deliveries** | `AddressSnapshot` -> `ShippingAddressEnc`; `DeliveredAt` -> `CompletedAt`. New: `UserId` (order owner), `DataKeyVersion`, `RowVersion`, computed `IsCompleted`. `Status` also allows 'Cancelled' |
| **DeliveryItems** | New: `OrderId` (copy from the delivery). Other columns unchanged |
| **Ratings** | New: `UpdatedAt` |
| **Comments** | `ApprovalStatus` -> `Status`. `Body` is limited to 2000 characters. New: `ModerationNote`, `UpdatedAt` |
| **ReturnRequests** | Status values unchanged (`Requested, Received, Approved, Rejected`). New: `ProductId` (from the line), `Reason` (`NOT NULL`), `ReviewedAt`, `ReviewNote` |
| **Refunds** | `IdempotencyKey` -> `IdempotencyKeyHash`; `RefundedAt` -> `CompletedAt`. New: `OrderId` (from the payment), `Reason` ('Return' or 'Cancellation'), `Status`, `CurrencyCode`, `CreatedAt` |
| **Notifications** | `DeduplicationKey` -> `DedupKey`. `SentAt` is dropped (in-app notices have no send step). Kind values unchanged (`WishlistDiscount, RefundApproved, ReturnRejected`). New: `DiscountId`, `Title`, `Body` (`NOT NULL`), `ReadAt` |
| **Conversations** | `CartId` is dropped: cart and order context come from `CustomerUserId` on the server. New: `PublicId`, `ClaimedAt`, `ClosedAt` |
| **ChatMessages** | New: `SenderKind` (Guest when `SenderUserId` is null, otherwise Customer or Agent), `ReadAt` |
| **ChatAttachments** | `ProtectedStorageKey` -> `StorageKey`. New: `Sha256`, `CreatedAt` |

## 4. Where old rows cannot honestly fill the new columns

These are the reasons for the "reset and reseed" recommendation. None of them should be filled with invented values:

- `Orders` snapshots and totals: old orders may have `NULL` snapshots and no `PurchasedAt`.
- `OrderItems.UnitCost`: old rows may be `NULL`; `ListUnitPrice` of a discounted line cannot be recovered exactly.
- `Payments.CardBrand` / `CardLast4`: old payments stored no card data (by design).
- `Discounts.CreatedByUserId`: the current table has no creator.
- `ReturnRequests.Reason`: the current table has no reason.
- `PaymentStatus = 'Unknown'` (cancelled legacy orders): not allowed any more.
- `EmailOutbox.LastAttemptAt`: the current table has `LastAttemptAt`, the proposed one does not. This is a gap in the
  proposal: add one nullable `DATETIME(6)` column if the retry logic still needs it.

## 5. Changes the application code will notice

1. **Login by e-mail** uses `EmailBlindIndex`; e-mail, tax ID and addresses are encrypted in the application (AES-GCM, key never in the database). This is SCRUM-93.
2. **Keys are `BIGINT UNSIGNED`** (C# `long`/`ulong`); public identifiers are separate columns (`PublicId`, `OrderNumber`, `InvoiceNumber`).
3. **Checkout** writes the order, the lines and the counters under the order-line row lock; a failed payment is stored as a `Payments` row with no order (linked to the cart), so it survives the rolled-back checkout.
4. **Stock** is an unsigned column: keep the conditional `UPDATE ... WHERE StockQuantity >= @qty` and check affected rows.
5. **Search** must replace `ı` and `İ` by `i` in the search text (same as `Products.SearchText`) and use `LIKE` for words shorter than 3 characters.
6. **Popularity** is computed from `OrderItems` (`Quantity` minus `RefundedQuantity` for paid, not cancelled orders). There is no precomputed statistics table in this version.
7. **Support chat** no longer stores `CartId`.
8. **Return window** counts from `Orders.PurchasedAt`, delivery is only a prerequisite (PDF 16). SCRUM-118's text says "after delivery"; that is not what the PDF says.

## 6. Suggested migration order

1. Create the new tables (`Addresses`, `Distributors`, `Brands`, `AttributeDefinitions`, `ProductAttributeValues`, `ProductImages`).
2. Change the existing tables in dependency order (Users, Categories, Products, then carts, orders and the rest).
3. Rebuild the seed data (SCRUM-120) with categories, brands, specs and images.
4. Run the existing checkout, cart and login tests, then the MySQL integration tests in CI.

`schema-future.sql` (ProductGroups, VAT, security logs, history tables, carrier tracking, precomputed reports) is optional and
should be added by separate migrations only if the team decides to.
