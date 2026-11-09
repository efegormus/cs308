# Design decisions and assumptions

Status: implemented in SCRUM-119. Earlier sections below record the design history;
the implementation decisions at the end supersede obsolete proposal-only assumptions.

## Sources read

- Course Project.pdf (SHA-256 `3eff4446...f5f0`, identical to the hash recorded in
  `docs/project-requirements.md`). Requirements 1-18 are the single source of truth.
- `docs/`: database-schema, database-target, project-requirements, order-lifecycle,
  purchasing, invoices.
- `OnlineStore/Data/Configurations/CartConfiguration.cs`, `ProductConfiguration.cs`.
- Jira SCRUM-72..100 status check (done: 72, 73, 74, 75, 100; to do: 76-78, 80, 81, 83-93).

## What exists today (reference, not a starting point)

- 22 tables created by the SCRUM-97 chain, plus `Orders.SourceCartId`.
- Already enforced in the database: RESTRICT on history, one owner per cart (CHECK),
  unique (CartId, ProductId), idempotent payments, one invoice per order,
  separate Ratings/Comments, one refund per return request, notification dedup key,
  status CHECKs, purchase snapshots, `RowVersion` on Orders.
- Left to the service layer today, but expressible in MySQL: one open cart per owner,
  "rating/comment belongs to a delivered line of the same user and product" (composite FK).

## Decisions taken with the owner

| Topic | Decision |
| --- | --- |
| Positioning | Design from scratch, plus a migration plan that separates additive changes from rewrites |
| Scope | All PDF requirements (CORE) plus brand, category tree, variants and typed specs, multiple addresses, audit log, delivery tracking. Out: multi-currency/exchange rates, coupons, multi-warehouse, reservations, closure table |
| Product domain | Broad electronics (phones, tablets, laptops, headphones, smartwatches, accessories, TV), per the TA feedback |
| Category tree | Adjacency list (`ParentId`) |
| Status columns | Text + CHECK (collation `utf8mb4_bin`), as in the current repo |
| Sensitive data | AES-GCM in the application, `VARBINARY` + `KeyVersion` columns, HMAC-SHA256 blind index for e-mail |
| Split of work | Tuna: design, DDL, tests on local MySQL, mapping and phase plan. Efe: EF entities, migrations, seed code |

## Assumptions (tell me if any is wrong)

1. **Rating scale 1-5.** The PDF allows 1-5 or 1-10; the repo already uses 1-5.
2. **Currency.** Single currency (TRY). Money is `DECIMAL(18,2)`. Orders, payments and
   invoices carry a `CurrencyCode CHAR(3)` with `CHECK = 'TRY'`, so the context is explicit
   without an exchange-rate table.
3. **Requirement 9 fields are CORE.** Model, serial number, warranty and distributor stay.
   Distributor becomes its own table; serial number stays a product/variant text field
   (a per-physical-unit table is out of scope).
4. **Roles.** One fixed role per account, `CHECK` on the four roles (PDF 10, matches the repo).
   No Roles/UserRoles tables; the PDF does not ask for configurable permissions.
5. **Keys.** Internal IDs `BIGINT UNSIGNED AUTO_INCREMENT`. Externally visible identifiers
   (order number, invoice number, guest token) are separate, unguessable columns.
   This breaks the current signed `INT` keys: listed as a breaking change.
6. **Cards.** No PAN, CVV or expiry is stored: brand plus last four digits only.
   This satisfies PDF 17 more strongly than hashing.
7. **Deliveries.** PDF 12 defines the delivery list. `Deliveries/DeliveryItems` stay CORE and gain
   carrier and tracking fields; tracking events are an extension. No second parallel
   "shipments" table.
8. **Return window** counts from purchase time; delivery is a prerequisite only (PDF 16).
9. **Real-time chat** transport (SignalR) is application-level. The schema stores
   conversations, messages, attachment metadata and claim state.
10. **Search.** FULLTEXT on name/description. The `ngram` parser, short model names (for
    example "S24") and the dotted/dotless i behavior will be tested, not assumed.
11. **Verification environment.** Docker is unavailable on this machine. Checks run on the local
    MySQL service in a throwaway schema (never `onlinestore`). The server version will be
    recorded; anything that cannot be confirmed against 8.4 is labeled "not verified".
12. **Output location.** `C:\Users\Tuna\db-design\`, outside the repository.

## Things MySQL cannot enforce (go to the service-layer catalog)

- Category depth limit and "products only in leaf categories".
- Cumulative returned quantity <= purchased quantity across rows.
- "Delivery delivered => payment confirmed" across tables (partly mirrored with a summary column).
- Overlapping discounts: the highest single rate wins (computed at checkout).
- Return request within 30 days of purchase (time of the request, not a static value).

## v2 changes (2026-11-08)

- Product model: every product is one independent row that holds price, cost, stock, SKU and serial
  number (Trendyol style). `ProductVariants` and `VariantOptionValues` were removed. Reason: the PDF
  does not ask for colour/storage variants, and the current cart, order, discount and wishlist code
  already uses ProductId, so the single-row model breaks far less.
- Scope after the owner's choice (extras option A): 25 [CORE] tables + 4 kept catalog tables
  (Brands, AttributeDefinitions, ProductAttributeValues, ProductImages) = 29 tables.
  Everything else (13 tables + ProductGroups, VAT, carrier tracking) is in `schema-future.sql`.
- Open for the team: grouping of "other colours / sizes" (ProductGroups, designed in
  schema-future.sql E1) and the extras scope were not answered by the team yet; the defaults above apply.
- Jira SCRUM-118 says returns are allowed "30 days after delivery"; PDF 16 says 30 days after
  PURCHASE, with delivery as a prerequisite. This design follows the PDF.
- Verified on MySQL 26.7.0 (the project targets 8.4): see `validation-results.md`.

## SCRUM-119 implementation (2026-11-09)

- The final user instruction selected the last merged design, with its 29 tables,
  rather than a reduction to the former 23-table schema. Future SQL remains excluded.
- The actual application is adapted to the new storage, preserving existing API
  field names through explicit mappings and relationship projections.
- A fresh EF baseline and backed-up reset/reseed workflow replace the earlier
  migration chain for development. No invented historical data conversion is used.
- MySQL 8.4.11 was installed locally and the schema, migrations, services and
  concurrency checks were exercised on that engine. The earlier 26.7.0 report is
  historical evidence, not the verification environment for this implementation.
- LastAttemptAt was added to EmailOutbox, as anticipated by the proposal's gap list.
- Version-1 encrypted values use nonce (12 bytes), ciphertext and authentication
  tag (16 bytes); a separate 32-byte HMAC key indexes trimmed, uppercase e-mails.
  Runtime keys are never stored in SQL or Git. Automatic key rotation is not implemented.
- Existing numeric API routes are retained. UUID/order/invoice identifiers remain
  in the database; role/ownership checks, not identifier secrecy, authorize reads.
- Existing substring search is kept for short model names and compatibility;
  Turkish i normalization is applied. A FULLTEXT index is present but not queried
  by the current endpoint.
- Demo examples in future-feature tables do not claim those APIs/UI are complete.
