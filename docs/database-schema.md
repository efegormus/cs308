# Current database schema (SCRUM-119)

The application now uses the 29-table redesign merged in SCRUM-118, with a fresh
EF Core baseline and a local reset/reseed workflow. The previous 23-table schema
and migrations are available in Git at `3acc716`.

- [Complete current design and file index](db-redesign/README.md)
- [ER diagrams](db-redesign/er-diagrams.md)
- [Every column, key and constraint](db-redesign/data-dictionary.md)
- [Runnable reference SQL](db-redesign/schema.sql)
- [Migration / application mappings](db-redesign/migration-notes.md)
- [Verification and its limits](db-redesign/validation-results.md)

The executable sources are `OnlineStore/Models`, `Data/RedesignConfiguration.cs`
and `Data/Migrations/*_RedesignedStore.cs`. The initial migration embeds the SQL;
it does not load a documentation file at runtime. `EmailOutbox.LastAttemptAt` is
the only extra column relative to the original proposal. The future schema is
excluded.

The database contains 29 domain tables, `vw_DeliveryList` and EF's
`__EFMigrationsHistory` bookkeeping table. Keys are BIGINT UNSIGNED (C# long with
provider conversion), money is exact decimal, timestamps are UTC and personal
e-mail/tax/address data is encrypted in the application. Runtime keys stay in a
private file outside Git and MySQL. Existing public DTO field names and numeric
routes remain compatible.

Resetting the database is explicitly authorized for this local implementation;
this baseline is not a data-conversion migration for an existing installation.
Do not run it over an old database. Back up and follow the reset instructions.

A complete data model does not implement every final-demo screen or service.
Current account, catalog, cart, checkout, invoice and delivery flows have been
adapted; future review/return/chat/management/reporting features retain their
separate application work.
