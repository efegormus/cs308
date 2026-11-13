# SCRUM-120 demo data

The development seed contains 55 products across 10 top-level departments and
10 subcategories. It includes sold-out stock, brands, images, typed specifications,
prices, costs, serial numbers, model names, warranty and distributor details.
The 30 added Nova products are explicitly fictional demo models; none of their
prices/specifications are retail claims.

Product pictures and descriptions (SCRUM-127): every product has a square picture in
`frontend/public/assets/catalog/products/`, named after its seed key, and a written description.
46 products have an original 512x512 SVG illustration (no real brand marks) and the 9 real-brand phones and
tablets with a matching photo have a `.jpg` (see `docs/IMAGE-CREDITS.md` for where they come from). The product
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

- Customer, ProductManager, SalesManager and SupportAgent accounts.
- Four orders: one Processing, one InTransit and two Delivered, each with a
  confirmed simulated payment, delivery, invoice and email-outbox entry.
- One active 10% phone discount, a wishlist item, a rating, one approved comment
  and one pending comment on a different delivered product.
- A sample partial refund and support conversation, preserving the schema's
  ownership constraints. These records do not implement the later management UIs.

Account names are `customer@onlinestore.invalid`,
`productmanager@onlinestore.invalid`, `salesmanager@onlinestore.invalid` and
`supportagent@onlinestore.invalid`. Random passwords are generated once in the
ignored `.local/db-redesign/demo-accounts.json` file. Read that file locally for
demo login; do not paste its contents into Jira or commit it. Existing passwords
are never reset by reseeding. Keep the credential file and encryption keys with
the database backup.

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
department counts, 55 products, all four roles, all three order statuses, approved
and pending comments, and preservation of edited stock on the second run.
`MySqlIntegrationTests` verifies catalog idempotence and schema constraints;
`SeedDataLoaderTests` also covers promotion of legacy departments to top level.

```sh
# Set ONLINESTORE_TEST_MYSQL using your local secret configuration first.
dotnet test cs308.sln
```

The CI MySQL job already sets the disposable connection and runs these tests.
Without the variable, MySQL tests are skipped; a unit-only pass is not database
verification. Dashboard APIs/UI remain in SCRUM-121/125 and their subtasks.
