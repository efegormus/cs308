# Progress demo: requirements 1, 3, 4, 5, 7 and 9

The source is the supplied Course Project.pdf (requirements on pages 1–2,
progress scope on page 4). This checklist covers that scope. Course participation,
reviewed/merged revision and grading evidence must be recorded separately; a
passing automated suite alone does not establish a grade.

| Requirement | Demonstration | Repeatable evidence |
| --- | --- | --- |
| 1 | Browse departments, select a product, add to cart. Category links filter the complete result set and survive reload. | Catalog browser check, MySQL catalog tests |
| 3 | Show stock; purchase reduces it once; manager moves the same order Processing → InTransit → Delivered; customer history updates. | Purchasing browser check, checkout/delivery MySQL tests |
| 4 | Guest cart survives login; declined payment leaves no paid order; confirmed simulated payment shows the actual invoice; download and emailed PDF match. | Purchasing browser check including Mailpit SMTP attachment comparison; invoice/outbox MySQL tests |
| 5 | From a delivered order, submit 1–5 stars and a comment; rating appears immediately, pending text is private; ProductManager approves/rejects and only approved text is public. | Real purchasing/review browser flow, moderation concurrency and ownership MySQL tests |
| 7 | Search name/description; order by effective price or units sold across categories/subcategories; sold-out products remain visible with Add to cart disabled. | Catalog browser check, query/pricing MySQL tests |
| 9 | Product page shows ID, name, model, serial, description, stock, effective price, warranty and distributor. | Product detail API tests and real browser field checks |

## Demo setup and rehearsal

1. Back up the existing local database, encryption keys and private credentials
   before migrating/upgrading fixtures. Apply migrations and run `--seed-demo`;
   it retains existing account passwords, inventory, purchases and review decisions.
2. Use the private credential file described in [demo data](demo-data.md).
   Verify all four roles and the second customer can log in. Check catalog images
   from the published app. Never include the credential file in a screenshot,
   Jira comment, commit or application log.
3. Start as guest: browse an off-first-page category, search, sort and select a
   stocked product. Show the serial and stock, add to cart, then log in at checkout.
4. Enter the documented successful **simulated** card. Show receipt, invoice on
   screen, PDF download and the same attachment in the receiving inbox. Real
   external inbox delivery needs the private SMTP setup in [invoices](invoices.md).
5. Switch to ProductManager and advance the order to InTransit and Delivered.
   Return to the customer's order history and follow Rate / Comment. Submit a
   rating/comment; show immediate stars and the author's Waiting for approval.
6. In a separate guest/other customer session show that pending text is absent.
   Approve as ProductManager; reload publicly and show it. Use the second customer
   or a separate delivered product to demonstrate rejection and isolation.
7. Rehearse at desktop and 375px, including direct-link refresh. Existing seeded
   live examples are AUDIO-002 for customer and LAPTOPS-002 for reviewer. Reseeding
   preserves submitted reviews; use a disposable DB for repeated blank examples.

## Automated browser run

Use a disposable local MySQL database, migrated and seeded, with the published
React app and a local Mailpit SMTP service. Supply that same database connection
and encryption-key environment to the verifier so its trusted operator command
can assign staff roles. It never exposes a role-changing HTTP endpoint.

```sh
python3 scripts/verify_catalog_browser.py --expect-seeded
python3 scripts/verify_purchasing_browser.py --operator-assembly /path/to/published/OnlineStore.dll --smtp-api-url http://127.0.0.1:8025
python3 scripts/verify_admin_browser.py --operator-assembly /path/to/published/OnlineStore.dll
python3 scripts/verify_profile_browser.py
```

These checks register unique fixture accounts and buy real seeded inventory.
They are intended for disposable local data, never a shared demo database. The
CI Browser end-to-end job runs this sequence with an isolated MySQL/Mailpit pair.
The purchasing check includes decline/success/double-submit/lost-response recovery,
actual approve/reject decisions, safe literal comment rendering, protected invoice
screens/PDFs and layouts at 1280/480/375px. The separate admin check uses controlled
response cases for its queue UI; it does not substitute for actual moderation.

## Verification record — 20 November 2026

On implementation revision `e39daa0`, 482 backend tests passed against real local
MySQL with zero skipped tests; 270 frontend and 31 Python tests also passed. The
production React build, workflow lint and complete-history secret scan passed.
All four browser verifiers passed, including the real delivered-review approval/
rejection flow, effective sale-price comparison and exact SMTP PDF attachment
comparison. A separate authenticated Gmail SMTP run accepted a new controlled
invoice email; confirmation in the recipient inbox is recorded in Jira.

The existing local demo was backed up before fixture upgrade. All five fixture
accounts could sign in, their previous passwords were retained and all product
images loaded from the published application. A separate customer/order was
created for the external SMTP rehearsal. Private SMTP/account files were not
committed. Disposable browser database/services were removed after verification.

Review/merge and the final rehearsal on the resulting main revision remain the
normal integration gate. Meeting participation and any grading evidence must be
recorded from actual events by the team.

## Integration with main — 20 November 2026

Resolved PR #68 against main `8f71d8f` while retaining SCRUM-114 login validation,
SCRUM-138 purchase colors and SCRUM-140 search hints/clear filters. The SCRUM-81
moderation queue is shared with the React screen: duplicate routes/types were
removed, both decision contracts use the same row lock, and versioned decisions
retain stale-screen protection. Cross-contract concurrency and queue adaptation
are covered by SQL/frontend tests.

Verification after integration: 510 backend tests with real MySQL and zero skips,
277 frontend tests, 31 Python tests, production publish and workflow lint passed.
Published browser checks verified category paging, clearing legacy hash filters,
global sorting and the actual purchase/delivery/review/moderation/SMTP PDF flow.

## SCRUM-141 integration — 20 November 2026

After PR #73 advanced main to `abd6d51`, PR #68 was reconciled again. The shared
SCRUM-141 review screen and its approve/reject animations, author/rating display,
queue paging, conflict recovery and dashboard updates are retained. The real
purchase-to-review browser check now targets that screen and the shared decision
API. Its real MySQL/SMTP flow and the detailed admin browser checks passed;
289 frontend tests, 31 Python tests and the production publish also passed.

## SCRUM-142 verification — 26 November 2026

The required detail fields shipped in merged PR #68. RequiredProductFieldsTests
now explicitly checks all nine public fields, large IDs/serials, undiscounted and
discounted DTO projections, stock visibility at zero, and exclusion of internal
inventory fields. Six mapping tests pass. Seed regression tests verify every
product's nonempty required text, unique serial, nonnegative price and stock.
The existing frontend validation/spec-row suite remains the browser contract.
