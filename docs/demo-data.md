# SCRUM-120 demo data

The development seed contains 55 products across 10 top-level departments and
10 subcategories. It includes sold-out stock, brands, images, typed specifications,
prices, costs, serial numbers, model names, warranty and distributor details.
The 30 added Nova products are explicitly fictional demo models; none of their
prices/specifications are retail claims.

Product pictures and descriptions (SCRUM-127): every product has a square picture in
`frontend/public/assets/catalog/products/`, named after its seed key, and a written description.
45 products have an original 512x512 SVG illustration (no real brand marks) and the 10 real-brand phones and
tablets have a `.jpg` photo (see `docs/IMAGE-CREDITS.md` for where they come from). The product
card shows the picture at 1:1, and the pictures work offline. `scripts/test_seed_catalog.py` checks that each
image exists, is square and is not shared, that photos are at most 150 KB and are listed in the credits file,
and that each description is written for its product.
The seed skips SKUs that already exist, so a database seeded earlier keeps its old pictures
and descriptions. To load the new ones, start from an empty database (this deletes the demo
data in it): `docker compose down -v`, then run the database, migrate and seed commands again.

## Initialize

Follow [local development](local-development.md) to configure a fresh MySQL database,
generate private encryption keys and apply EF migrations. Then run:

```sh
dotnet run --project OnlineStore --launch-profile http -- --seed-demo
```

This is the full fixture command; `--seed` installs only the catalog. Both refuse
non-Development environments. With the existing private native MySQL setup, use
`.local/db-redesign/venv/bin/python scripts/redesign_db.py seed` instead.

The full seed supplies:

- Five accounts: two Customers plus ProductManager, SalesManager and SupportAgent.
- Eight orders: one Processing, one InTransit and six Delivered, each with a
  confirmed simulated payment, delivery, invoice and email-outbox entry.
- One active 10% phone discount, a wishlist item, three ratings, two approved
  comments and two pending comments on delivered products.
- A sample partial refund and support conversation, preserving the schema's
  ownership constraints. These records do not implement the later management UIs.

Account names are `customer@onlinestore.invalid`, `reviewer@onlinestore.invalid`,
`productmanager@onlinestore.invalid`, `salesmanager@onlinestore.invalid` and
`supportagent@onlinestore.invalid`. Random passwords are generated once in the
ignored `.local/db-redesign/demo-accounts.json` file. Read that file locally for
demo login; do not paste its contents into Jira or commit it. Existing passwords
are never reset by reseeding. Keep the credential file and encryption keys with
the database backup. Upgrading an existing four-account credential file adds only
the missing reviewer password; existing passwords are retained. A malformed
credential file is rejected rather than silently replacing its passwords.

## Requirement 5 review demo (SCRUM-136)

On a fresh fixture, both customers have three delivered products:

| Customer | Product SKU | Initial rating | Initial comment |
| --- | --- | --- | --- |
| customer | PHONE-001 | 5 | Approved |
| customer | TABLET-001 | None | Pending |
| customer | AUDIO-002 | None | None: live rating/comment example |
| reviewer | PHONE-001 | 3 | Approved |
| reviewer | TABLET-001 | 4 | Pending |
| reviewer | LAPTOPS-002 | None | None: second live example |

PHONE-001 therefore starts with a 4.0/5 average from two ratings. TABLET-001 has
4.0/5 from one rating even while its comments await approval. No pending text
should appear to other customers. After SCRUM-80/137 are integrated, log in as
customer and review AUDIO-002 from order DEMO-RATE-LIVE-001. Repeat with reviewer
and LAPTOPS-002 (DEMO-REVIEWER-LIVE-001). Show immediate rating visibility and the
author's pending state, then use the ProductManager moderation feature
(SCRUM-81/141) to approve a comment before showing it publicly.

These fixtures provide real confirmed purchases and completed deliveries through
the existing checkout/fulfillment services. They do not bypass eligibility checks
or implement the separate review/moderation screens. Product IDs are generated
by the target database; use the SKU/order marker to find the current ID.

Reseeding never resets a review submitted during a demo or a moderator's decision.
To rehearse the same empty-review example again, use a disposable fresh database
or restore a private backup. Do not delete reviews from a shared demo database
just to make the form empty.

## Repeat runs and upgrading the old seed

Products are matched by SKU; existing prices and inventory are retained. Named
demo categories are reused and their hierarchy is reconciled. On an existing
25-product database the now-unused legacy Electronics category is retained to
avoid deleting existing data, so there can be 21 category rows instead of the
fresh database's 20. Existing products remain in their original departments;
new products populate the subcategories. Orders are matched by stable DEMO order
numbers and other fixtures by their ownership keys, so a second completed run
does not duplicate purchases, comments, refunds or stock changes.

The phone discount ends 30 days after initial creation; reseeding deliberately
does not rewrite existing campaigns. For a later demo use a newly initialized
disposable database or manage that campaign explicitly.

## Verification

`DemoSeedMySqlTests` runs the full seed twice on the guarded local `_tests`
database, with temporary private credentials and invoice storage. It checks
department counts, 55 products, all four roles, two customers with several
delivered products and unreviewed lines, exact initial rating averages and
moderation states. The second run preserves passwords, edited stock, a customer's
new review and a moderator's changed decision. `DemoAccountCredentialsTests`
covers creation, upgrades, repeat runs, corrupt files and private Unix permissions.
`MySqlIntegrationTests` verifies catalog idempotence and schema constraints;
`SeedDataLoaderTests` also covers promotion of legacy departments to top level.

```sh
# Set ONLINESTORE_TEST_MYSQL using your local secret configuration first.
dotnet test cs308.sln
```

The CI MySQL job already sets the disposable connection and runs these tests.
Without the variable, MySQL tests are skipped; a unit-only pass is not database
verification. Review APIs/screens remain SCRUM-80/81/137/141; fixtures alone do not
implement those features.
