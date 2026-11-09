# Database redesign proposal (SCRUM-118)

A from-scratch design of the store database for a general electronics store (phones, tablets, laptops, monitors, audio,
wearables, accessories), covering every requirement of Course Project.pdf. This folder is a **proposal**: the current
schema in `docs/database-schema.md` and the EF Core migrations are unchanged until SCRUM-119 implements it.

| File | What it is |
|---|---|
| [er-diagrams.md](er-diagrams.md) | Overview diagram and one diagram per module (Mermaid) |
| [data-dictionary.md](data-dictionary.md) | Every table and column: type, null, default, keys, checks, indexes, and why the table exists |
| [schema.sql](schema.sql) | Runnable MySQL 8.4 DDL for the 29 tables and one view |
| [schema-future.sql](schema-future.sql) | Optional extensions that are **not** part of the proposal (product groups, VAT, security logs, history tables, carrier tracking, precomputed reports) |
| [validation-results.md](validation-results.md) | 144 checks run against a real MySQL (invalid data is rejected, parallel requests, Turkish search) |
| [migration-notes.md](migration-notes.md) | Current -> proposed table and column mapping, breaking changes, and the reset-and-reseed recommendation for SCRUM-119 |
| [design-decisions.md](design-decisions.md) | Decisions and assumptions behind the design |

## In short

- 29 tables: 25 required by a PDF requirement (`[CORE]`) and 4 catalog tables for product variety (`[EXT]`: Brands, AttributeDefinitions, ProductAttributeValues, ProductImages). 23 of the 29 already exist in the current schema.
- Each product is one row with its own price, cost, stock, SKU and serial number. Specs differ per category and are typed (no free-form JSON).
- Card numbers, CVV and expiry are never stored (brand and last four digits only). E-mail, tax ID and addresses are encrypted in the application; the database stores ciphertext, a key version and an e-mail blind index.
- Many rules that were only service-layer rules are enforced by the database: one open cart per user, one refund per return request, stock cannot go below zero, a rating or comment must reference a line bought by the same user for the same product, delivered <= allocated <= ordered quantity.
- Rules MySQL cannot express stay in the services (30-day return window counted from purchase, "the line was delivered", category cycles, role of the moderator, overlapping discounts, out-of-stock items in the cart).

Everything was run on MySQL 26.7.0 (the project targets 8.4); see the end of `validation-results.md`.
