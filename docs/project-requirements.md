# Course project requirements

Source: **Course Project.pdf**, Sabancı University CS 308, seven pages.
SHA-256: `3eff444660da86493ebd87430b9c3ab2ca665887acb63195e61be63fa56ef5f0`.
The specification takes precedence over contradictory ticket wording. This
analysis does not imply that all the features below are implemented.

| Requirement / PDF page | Required behavior | Related work |
| --- | --- | --- |
| 1 / 1 | Categorized products and a shopping cart | Catalog 41/43/44/54; cart 49/99/102 |
| 2 / 1 | Team chooses the product domain | Existing phones and tablets |
| 3 / 1 | Show stock; decrement after purchase; processing, in-transit, delivered order history | Checkout 72; history/delivery 76/79 |
| 4 / 1 | Guest cart; login before checkout; retain cart after login; confirmed mock payment; visible invoice and emailed PDF | 47/49/72–75/99/100; password/role foundation 48 |
| 5 / 1 | Delivered purchases only for reviews; comments approved by product manager; ratings immediately visible | 80/81; separate Rating and Comment records |
| 6 / 1 | Attractive, usable, professional UI | 56/98; keep catalog responsive during redesign |
| 7 / 1–2 | Search names/descriptions; sort price/popularity; out-of-stock items searchable but not cartable | 50/51/65/67/70/101; server stock checks as well as UI |
| 8 / 2 | Customer shopping UI and custom manager UI | 68/69/77–79/83/86/87/92/98 |
| 9 / 2 | Product ID, name, model, serial, description, stock, price, warranty, distributor | 41 adds fields; active-product validation/check now requires description and all catalog details |
| 10 / 2 | Customer, SalesManager, ProductManager, SupportAgent are distinct roles | 48; public requests cannot grant staff roles |
| 11 / 2 | Sales managers set prices/discounts, notify wishlists, export invoices by date, chart revenue/profit; default cost 50% | 83–87; snapshot paid price and unit cost for historical reports |
| 12 / 2 | Product managers manage catalog/stock, delivery list/status, comment approval | 77–81; delivery ID/customer/product/quantity/total/address/completion |
| 13 / 2 | Realtime guest/customer support; queue/claim; text/files; linked authenticated customer context | 91/92; conversation ownership, agent assignment and protected attachments |
| 14 / 2–3 | Customer ID/name/tax ID/email/address/password; profile; wishlists, orders, cancellation, returns | Fields 71 (overlaps 104); profile 103; cancellation 88; returns 89/90 |
| 15 / 3 | Credit-card input for mock payment; banking checks/limits excluded | 73; do not retain full PAN or CVV |
| 16 / 3 | Selective return of delivered items within 30 days of **purchase**; manager approval after receipt; original-price refund, restock and notification | 89/90; partial quantities, paid-price snapshots and idempotency |
| 17 / 3 | Defensive validation, separate role privileges, protect sensitive information | 48/60/93/95; adaptive salted password hashes; encrypt/access-control retrievable personal data and invoices |
| 18 / 3 | Concurrent users/roles must preserve correctness | 72/90/91/94; atomic inventory changes, idempotent refunds, exclusive chat claims |

Progress demo requires **1, 3, 4, 5, 7, 9**; final demo requires all features
(page 4). Passing catalog tests does not prove demo readiness. Process metrics
also require meaningful commits, tests, bug reports, backlog work and continuous
contribution, not artificial changes to meet counts.

## Corrections to the original design

- The original five-table diagram was only the initial foundation. It was also
  missing SCRUM-41's product fields. The current 29-table design is described in
  the [current schema](database-schema.md) and the
  [redesign documentation](db-redesign/README.md).
- Tax ID and home address belong on the existing user/customer record (71),
  rather than in an unrelated duplicate account table. A PasswordHash column
  alone does not implement secure authentication or role boundaries (48).
- AlignOrderLifecycle now separates payment from Processing/InTransit/Delivered
  fulfillment and preserves known legacy paid/unpaid information. Unknown legacy
  cancellation payment, dates and costs require reconciliation; none are invented.
- Order lines already preserve product name and paid unit price. UnitCost now
  preserves known purchase-time cost for profit calculations. Order, invoice and delivery
  customer data are purchase-time snapshots (SCRUM-97), unaffected by later profile edits.
- Ratings and comments need separate visibility rules. A pending comment must
  not hide a rating that requires no approval.
- The return window starts at **purchase**, with delivery as a prerequisite.
  SCRUM-89 has been corrected to purchase-based eligibility, with delivery required.
- Homepage links now reach real API category IDs through SCRUM-54. The browser
  verification covers real MySQL, category grouping and failure/retry states.

## Delivery branches

| Branch | Scope | Base |
| --- | --- | --- |
| scrum-39-schema-alignment | This analysis, current ER corrections, full target design | main |
| scrum-71-customer-profile | Customer fields, validation, migration, tests | schema alignment |
| scrum-48-passwords-and-roles | Password hashing, authentication, role policies | 71 |
| scrum-53-local-mysql | Reproducible local MySQL, configuration/migration/seed checks | 48 |
| scrum-54-catalog-integration | Shared API integration, navigation, MySQL/browser checks | 53 |

Branches form a dependency stack; review/merge them in this order. Each task's
own diff is against its preceding branch. No main history rewrite is needed.
SCRUM-97 (Tuna) owns the full schema implementation; SCRUM-64 (Rümeysa) owns
broader schema documentation. This correction provides their design inputs,
without claiming their unimplemented application features are complete.

The current planning/ownership update is recorded in [Sprint 2 MVP](sprint-2-mvp.md);
legacy migration behavior and return timing are in [order lifecycle](order-lifecycle.md).
