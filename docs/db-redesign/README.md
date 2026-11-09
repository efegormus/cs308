# Database redesign — implemented (SCRUM-119)

The design merged in **3acc716 / SCRUM-118** is now the application's database:
**29 domain tables and one view**, with EF Core mappings, a fresh migration
baseline, encrypted personal data and expanded demonstration data. The only
additional column is `EmailOutbox.LastAttemptAt`, identified as missing by the
original migration notes. The optional future schema is **not installed**.

| File | Purpose |
| --- | --- |
| [er-diagrams.md](er-diagrams.md) | Overview and module relationships |
| [data-dictionary.md](data-dictionary.md) | All tables, columns, keys, checks and types |
| [schema.sql](schema.sql) | MySQL 8.4 DDL; also embedded in the initial EF migration |
| [schema-future.sql](schema-future.sql) | Excluded historical ideas; never executed by setup |
| [validation-results.md](validation-results.md) | Current verification and clearly separated original design results |
| [migration-notes.md](migration-notes.md) | Reset contract, mappings and application compatibility |
| [design-decisions.md](design-decisions.md) | Design rationale and implementation decisions |

There are 25 tables tagged CORE in the original proposal and four retained
catalog extensions: Brands, AttributeDefinitions, ProductAttributeValues and
ProductImages. These tags identify design scope, not a PDF-mandated table count:
for example, the PDF requires an address and distributor information but does
not prescribe separate tables. The user's final instruction was to implement
the last merged design, including its retained catalog extensions.

Local verification uses **MySQL 8.4.11**, UTC, strict mode and InnoDB. The
application uses numeric routes for compatibility, with C# `long` IDs mapped to
MySQL unsigned BIGINT; the existing frontend accepts safe JavaScript integers.
The schema also has public UUID/order/invoice identifiers. Keeping those columns
does not mean every existing route has been changed to UUIDs.

A table's existence does not mean every final-demo feature already has an API or
UI. The existing account, catalog, cart, checkout, invoice and delivery flows are
adapted. Review submission/moderation, full returns/refunds management, live chat,
wishlist/discount management and reporting still need their planned application
features; their data model and demo records are present.
