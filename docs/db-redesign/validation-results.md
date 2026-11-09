# Validation results

## SCRUM-119 implementation verification — 2026-11-09

The application was migrated and tested against local **MySQL 8.4.11**, with strict SQL mode and UTC timestamps. The implemented baseline contains **29 domain tables**, `vw_DeliveryList`, and EF migration history. The only added column relative to the merged design is `EmailOutbox.LastAttemptAt`.

| Verification | Result |
|---|---|
| .NET test suite | 373 passed, 0 failed, 0 skipped; includes 39 real-MySQL tests |
| Frontend unit tests | 128 passed, 0 failed |
| Frontend production build | Passed |
| Python tooling tests | 11 passed |
| Catalog browser verification in Chrome | All 7 sections passed, including mobile and error/retry behavior |
| Purchasing browser verification in Chrome | All 4 scenario groups passed: checkout/PDF, delivery management, lost-response recovery, role isolation |
| EF model versus migration snapshot | No pending model changes |
| Fresh local database recreation | Backup, migration and demo seed succeeded |
| Repeated demo seed | Added 0 categories and 0 products; demo records did not duplicate |

The final clean seed contains 8 categories, 25 products, 6 brands, 21 attribute definitions, 75 typed attribute values, 4 role accounts, and 3 orders with payments, deliveries and invoices. Browser test fixtures were removed by a backed-up reset and reseed after browser verification. The backup includes SQL, encryption keys, demo credentials and invoice files under the private `.local/db-redesign/backups/` directory.

Real-MySQL coverage includes physical schema and encrypted persistence, composite ownership constraints, typed attributes, concurrent cart/checkout behavior, rollback, declined payments, invoices, and email-outbox processing. Payment and email transports are local simulations. The presence of return, review, discount and support demo rows does not mean their complete management APIs or screens are implemented.

## Historical design-only verification (from the merged design)

The 144 checks below are the original design report, retained for traceability. They were **not rerun as this same 144-check script** during SCRUM-119; the implementation verification above is a separate test run.

Run on MySQL **26.7.0** (the target is 8.4: see the note at the end), `sql_mode` = STRICT_TRANS_TABLES + defaults, database `schema_design_test`, user `onlinestore_dev`. Each test sends one statement (or a short sequence) against seeded data and compares the MySQL error number with the expectation.

**Result: 144 of 144 checks passed** (138 negative/positive SQL checks + 6 concurrency checks).

## SQL checks

| # | Area | Check | Expected | Actual | Result |
|---|---|---|---|---|---|
| 1 | Users | Role not one of the four | check violated (3819) | check violated (3819) | PASS |
| 2 | Users | Customer without tax ID | check violated (3819) | check violated (3819) | PASS |
| 3 | Users | Duplicate e-mail (same blind index) | duplicate key (1062) | duplicate key (1062) | PASS |
| 4 | Users | Key version 0 | check violated (3819) | check violated (3819) | PASS |
| 5 | Users | Staff without tax ID is allowed | succeeds | succeeds | PASS |
| 6 | Addresses | Second default address for the same user | duplicate key (1062) | duplicate key (1062) | PASS |
| 7 | Addresses | Address for a missing user | foreign key: parent missing (1452) | foreign key: parent missing (1452) | PASS |
| 8 | Addresses | Second NON-default address is allowed | succeeds | succeeds | PASS |
| 9 | Categories | Duplicate root category name | duplicate key (1062) | duplicate key (1062) | PASS |
| 10 | Categories | Duplicate name under the same parent | duplicate key (1062) | duplicate key (1062) | PASS |
| 11 | Categories | Same name under a different parent is allowed | succeeds | succeeds | PASS |
| 12 | Categories | Parent that does not exist | foreign key: parent missing (1452) | foreign key: parent missing (1452) | PASS |
| 13 | Categories | Delete a category that still has products | foreign key: row is referenced (1451) | foreign key: row is referenced (1451) | PASS |
| 14 | Products | Negative price | check violated (3819) | check violated (3819) | PASS |
| 15 | Products | Negative cost | check violated (3819) | check violated (3819) | PASS |
| 16 | Products | Duplicate SKU | duplicate key (1062) | duplicate key (1062) | PASS |
| 17 | Products | Stock decrement below zero | out of range (1690) | out of range (1690) | PASS |
| 18 | Products | Active product with blank description | check violated (3819) | check violated (3819) | PASS |
| 19 | Products | Inactive draft with blank description is allowed | succeeds | succeeds | PASS |
| 20 | Products | Product in a missing category | foreign key: parent missing (1452) | foreign key: parent missing (1452) | PASS |
| 21 | Products | Delete a product that appears in orders | foreign key: row is referenced (1451) | foreign key: row is referenced (1451) | PASS |
| 22 | Attributes | Number-typed value that also fills ValueText | check violated (3819) | check violated (3819) | PASS |
| 23 | Attributes | Value type differs from its definition | foreign key: parent missing (1452) | foreign key: parent missing (1452) | PASS |
| 24 | Attributes | Same spec twice on one product | duplicate key (1062) | duplicate key (1062) | PASS |
| 25 | Attributes | Correctly typed value is allowed | succeeds | succeeds | PASS |
| 26 | Images | Second primary image for a product | duplicate key (1062) | duplicate key (1062) | PASS |
| 27 | Images | Extra non-primary image is allowed | succeeds | succeeds | PASS |
| 28 | Discounts | Rate 0 | check violated (3819) | check violated (3819) | PASS |
| 29 | Discounts | Rate above 1 | check violated (3819) | check violated (3819) | PASS |
| 30 | Discounts | End not after start | check violated (3819) | check violated (3819) | PASS |
| 31 | Discounts | Same product twice in a campaign | duplicate key (1062) | duplicate key (1062) | PASS |
| 32 | Discounts | Creator that does not exist | foreign key: parent missing (1452) | foreign key: parent missing (1452) | PASS |
| 33 | Carts | Cart with both a user and a guest token | check violated (3819) | check violated (3819) | PASS |
| 34 | Carts | Cart with no owner | check violated (3819) | check violated (3819) | PASS |
| 35 | Carts | Second open cart for the same user | duplicate key (1062) | duplicate key (1062) | PASS |
| 36 | Carts | Guest token reused | duplicate key (1062) | duplicate key (1062) | PASS |
| 37 | Carts | Unknown cart state | check violated (3819) | check violated (3819) | PASS |
| 38 | Carts | First open cart for a user is allowed | succeeds | succeeds | PASS |
| 39 | Carts | Second open cart for that user | duplicate key (1062) | duplicate key (1062) | PASS |
| 40 | Carts | After closing the cart a new open cart is allowed | succeeds | succeeds | PASS |
| 41 | CartItems | Quantity 0 | check violated (3819) | check violated (3819) | PASS |
| 42 | CartItems | Same product twice in a cart | duplicate key (1062) | duplicate key (1062) | PASS |
| 43 | CartItems | Product that does not exist | foreign key: parent missing (1452) | foreign key: parent missing (1452) | PASS |
| 44 | Wishlist | Same product twice | duplicate key (1062) | duplicate key (1062) | PASS |
| 45 | Wishlist | Missing product | foreign key: parent missing (1452) | foreign key: parent missing (1452) | PASS |
| 46 | Orders | Unknown status | check violated (3819) | check violated (3819) | PASS |
| 47 | Orders | InTransit with unconfirmed payment | check violated (3819) | check violated (3819) | PASS |
| 48 | Orders | Delivered without DeliveredAt | check violated (3819) | check violated (3819) | PASS |
| 49 | Orders | Confirmed payment without PurchasedAt | check violated (3819) | check violated (3819) | PASS |
| 50 | Orders | GrandTotal does not match the parts | check violated (3819) | check violated (3819) | PASS |
| 51 | Orders | Duplicate order number | duplicate key (1062) | duplicate key (1062) | PASS |
| 52 | Orders | Same cart bought twice | duplicate key (1062) | duplicate key (1062) | PASS |
| 53 | Orders | Currency other than TRY | check violated (3819) | check violated (3819) | PASS |
| 54 | Orders | Cancelled without CancelledAt | check violated (3819) | check violated (3819) | PASS |
| 55 | Orders | Discount larger than subtotal | check violated (3819) | check violated (3819) | PASS |
| 56 | Orders | Valid order is allowed | succeeds | succeeds | PASS |
| 57 | OrderItems | Quantity 0 | check violated (3819) | check violated (3819) | PASS |
| 58 | OrderItems | Paid price above list price | check violated (3819) | check violated (3819) | PASS |
| 59 | OrderItems | Discounted price does not match the rate | check violated (3819) | check violated (3819) | PASS |
| 60 | OrderItems | Line total does not match price x quantity | check violated (3819) | check violated (3819) | PASS |
| 61 | OrderItems | Delivered more than allocated | check violated (3819) | check violated (3819) | PASS |
| 62 | OrderItems | Same product twice in an order | duplicate key (1062) | duplicate key (1062) | PASS |
| 63 | OrderItems | Line owner differs from the order owner | foreign key: parent missing (1452) | foreign key: parent missing (1452) | PASS |
| 64 | OrderItems | Refunded more than return-requested | check violated (3819) | check violated (3819) | PASS |
| 65 | OrderItems | Return-requested more than delivered | check violated (3819) | check violated (3819) | PASS |
| 66 | OrderItems | Missing product | foreign key: parent missing (1452) | foreign key: parent missing (1452) | PASS |
| 67 | OrderItems | Correct discounted line is allowed | succeeds | succeeds | PASS |
| 68 | Payments | Same idempotency key twice for one user | duplicate key (1062) | duplicate key (1062) | PASS |
| 69 | Payments | Card last 4 not digits | check violated (3819) | check violated (3819) | PASS |
| 70 | Payments | Unknown card brand | check violated (3819) | check violated (3819) | PASS |
| 71 | Payments | Confirmed payment without an order | check violated (3819) | check violated (3819) | PASS |
| 72 | Payments | Payment on another user's cart | foreign key: parent missing (1452) | foreign key: parent missing (1452) | PASS |
| 73 | Payments | Payment confirming another user's order | foreign key: parent missing (1452) | foreign key: parent missing (1452) | PASS |
| 74 | Payments | Negative amount | check violated (3819) | check violated (3819) | PASS |
| 75 | Payments | Failed attempt without an order is allowed | succeeds | succeeds | PASS |
| 76 | Deliveries | Delivered without CompletedAt | check violated (3819) | check violated (3819) | PASS |
| 77 | Deliveries | Delivery whose customer is not the order owner | foreign key: parent missing (1452) | foreign key: parent missing (1452) | PASS |
| 78 | Deliveries | Unknown status | check violated (3819) | check violated (3819) | PASS |
| 79 | DeliveryItems | Line belonging to a different order | foreign key: parent missing (1452) | foreign key: parent missing (1452) | PASS |
| 80 | DeliveryItems | Quantity 0 | check violated (3819) | check violated (3819) | PASS |
| 81 | DeliveryItems | Same line twice in a delivery | duplicate key (1062) | duplicate key (1062) | PASS |
| 82 | Ratings | Stars 6 | check violated (3819) | check violated (3819) | PASS |
| 83 | Ratings | Stars 0 | check violated (3819) | check violated (3819) | PASS |
| 84 | Ratings | Rating by someone who did not buy the line | foreign key: parent missing (1452) | foreign key: parent missing (1452) | PASS |
| 85 | Ratings | Rating for a different product than the line | foreign key: parent missing (1452) | foreign key: parent missing (1452) | PASS |
| 86 | Ratings | Valid rating is allowed | succeeds | succeeds | PASS |
| 87 | Ratings | Second rating by the same user and product | duplicate key (1062) | duplicate key (1062) | PASS |
| 88 | Comments | Pending comment that already has a moderator | check violated (3819) | check violated (3819) | PASS |
| 89 | Comments | Approved comment without moderator | check violated (3819) | check violated (3819) | PASS |
| 90 | Comments | Empty comment text | check violated (3819) | check violated (3819) | PASS |
| 91 | Comments | Comment by someone who did not buy the line | foreign key: parent missing (1452) | foreign key: parent missing (1452) | PASS |
| 92 | Comments | New comment starts as Pending (allowed) | succeeds | succeeds | PASS |
| 93 | Comments | Second comment by the same user and product | duplicate key (1062) | duplicate key (1062) | PASS |
| 94 | Returns | Return by a user who did not buy the line | foreign key: parent missing (1452) | foreign key: parent missing (1452) | PASS |
| 95 | Returns | Quantity 0 | check violated (3819) | check violated (3819) | PASS |
| 96 | Returns | Received without ReceivedAt | check violated (3819) | check violated (3819) | PASS |
| 97 | Returns | Requested but already has a reviewer | check violated (3819) | check violated (3819) | PASS |
| 98 | Returns | Return for a product different from the line | foreign key: parent missing (1452) | foreign key: parent missing (1452) | PASS |
| 99 | Refunds | Second refund for the same return request | duplicate key (1062) | duplicate key (1062) | PASS |
| 100 | Refunds | Return refund without a return request | check violated (3819) | check violated (3819) | PASS |
| 101 | Refunds | Refund using a payment of another order | foreign key: parent missing (1452) | foreign key: parent missing (1452) | PASS |
| 102 | Refunds | Same idempotency key twice | duplicate key (1062) | duplicate key (1062) | PASS |
| 103 | Refunds | Amount 0 | check violated (3819) | check violated (3819) | PASS |
| 104 | Refunds | Completed without CompletedAt | check violated (3819) | check violated (3819) | PASS |
| 105 | Refunds | Second cancellation refund for one order | duplicate key (1062) | duplicate key (1062) | PASS |
| 106 | Invoices | Second invoice for the same order | duplicate key (1062) | duplicate key (1062) | PASS |
| 107 | Invoices | Duplicate invoice number | duplicate key (1062) | duplicate key (1062) | PASS |
| 108 | Invoices | Invoice totals do not add up | check violated (3819) | check violated (3819) | PASS |
| 109 | Invoices | Invoice for a missing order | foreign key: parent missing (1452) | foreign key: parent missing (1452) | PASS |
| 110 | Invoices | Invoice for a new paid order is allowed | succeeds | succeeds | PASS |
| 111 | EmailOutbox | Invoice mail without an invoice | check violated (3819) | check violated (3819) | PASS |
| 112 | EmailOutbox | Invoice mail that also points at a refund | check violated (3819) | check violated (3819) | PASS |
| 113 | EmailOutbox | Sent without SentAt | check violated (3819) | check violated (3819) | PASS |
| 114 | EmailOutbox | Invoice mail is allowed | succeeds | succeeds | PASS |
| 115 | EmailOutbox | Same mail enqueued twice | duplicate key (1062) | duplicate key (1062) | PASS |
| 116 | Chat | Conversation with both owners | check violated (3819) | check violated (3819) | PASS |
| 117 | Chat | Conversation with no owner | check violated (3819) | check violated (3819) | PASS |
| 118 | Chat | Active conversation without an agent | check violated (3819) | check violated (3819) | PASS |
| 119 | Chat | Waiting conversation that already has an agent | check violated (3819) | check violated (3819) | PASS |
| 120 | Chat | Guest conversation is allowed | succeeds | succeeds | PASS |
| 121 | Chat | Agent message without a sender account | check violated (3819) | check violated (3819) | PASS |
| 122 | Chat | Unknown sender kind | check violated (3819) | check violated (3819) | PASS |
| 123 | Chat | Guest message is allowed | succeeds | succeeds | PASS |
| 124 | Chat | Attachment of size 0 | check violated (3819) | check violated (3819) | PASS |
| 125 | Chat | Attachment larger than 100 MB | check violated (3819) | check violated (3819) | PASS |
| 126 | Chat | Executable attachment type | check violated (3819) | check violated (3819) | PASS |
| 127 | Chat | PDF attachment is allowed | succeeds | succeeds | PASS |
| 128 | Notifications | Same event notified twice | duplicate key (1062) | duplicate key (1062) | PASS |
| 129 | Notifications | Wishlist-discount notice without a discount | check violated (3819) | check violated (3819) | PASS |
| 130 | Notifications | Unknown kind | check violated (3819) | check violated (3819) | PASS |
| 131 | Notifications | Wishlist-discount notice is allowed | succeeds | succeeds | PASS |
| 132 | History | Delete a user who has orders | foreign key: row is referenced (1451) | foreign key: row is referenced (1451) | PASS |
| 133 | History | Delete an order that has lines | foreign key: row is referenced (1451) | foreign key: row is referenced (1451) | PASS |
| 134 | History | Delete a paid order's payment | foreign key: row is referenced (1451) | foreign key: row is referenced (1451) | PASS |
| 135 | History | Delete an order's invoice's order via cart | foreign key: row is referenced (1451) | foreign key: row is referenced (1451) | PASS |
| 136 | History | Delete a discount used by a campaign | foreign key: row is referenced (1451) | foreign key: row is referenced (1451) | PASS |
| 137 | Cascade | Deleting a cart deletes its lines (owned draft) | succeeds | succeeds | PASS |
| 138 | Cascade | Deleting a conversation deletes messages and attachments | succeeds | succeeds | PASS |

## Concurrency checks (8 parallel connections)

| Scenario | Expected | Actual | Result |
|---|---|---|---|
| Last unit of stock: 8 buyers, 1 unit | exactly 1 winner, stock 0 | 1 winner(s), stock 0 | PASS |
| Support chat claim: 8 agents claim one chat | exactly 1 winner | 1 winner(s) | PASS |
| Double refund: 8 refunds for one return request, different keys | exactly 1 refund | 1 refund(s) | PASS |
| Double open cart: 8 requests create a cart for one user | exactly 1 cart | 1 cart(s) | PASS |
| Double invoice: 8 invoices for one order, different numbers | exactly 1 invoice | 1 invoice(s) | PASS |
| Same cart checked out by 8 requests with different order numbers | exactly 1 order | 1 order(s) | PASS |

## What this does not prove

- **Server version:** the test server is 26.7.0, not 8.4. Everything used here (CHECK, generated columns, composite FKs,
  expression defaults, FULLTEXT) exists in 8.4, but it was not run on 8.4 itself.
- **Service-layer rules** are not tested because MySQL cannot express them: category cycles, 30-day return window, "line was
  delivered" for ratings/comments, role of the moderator/reviewer, overlapping discounts (highest rate wins), out-of-stock
  items blocked from the cart, and the maximum category depth.
- **Concurrency** was tested with unique keys and one conditional UPDATE, not with the full checkout transaction.
- **FULLTEXT search** (Turkish characters, short model names) is not tested yet.
