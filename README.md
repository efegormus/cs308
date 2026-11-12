# CS 308 Online Store

Electronics store with phones, tablets, laptops, monitors, audio, wearables and accessories, built for the Sabancı University CS 308 course project.

## Tech stack

- Backend: C# with ASP.NET Core (.NET 8)
- Front end: React and Vite; the production build is served by ASP.NET Core
- Database: MySQL (Entity Framework Core)
- Version control: Git and GitHub; planning in Jira (Scrum)

## Project structure

```
OnlineStore/          ASP.NET Core project
  Program.cs          application entry point
  Models/             domain entities for the 29-table SCRUM-119 schema
  Data/               StoreDbContext, entity mappings, and migrations
  wwwroot/            empty in the repo; publishing puts the React build in wwwroot/spa
frontend/             React routes, components, tests and Vite build
.config/              repository-local EF Core CLI tool
docs/                 database schema documentation
```

See [course requirements](docs/project-requirements.md), the
[implemented database schema](docs/database-schema.md), and the
[current ER diagrams](docs/db-redesign/er-diagrams.md) before extending the domain.
The [order/payment rules](docs/order-lifecycle.md) and
[Sprint 2 purchasing plan](docs/sprint-2-mvp.md) define the current integration contract.

## Getting started

For a fresh clone, follow the [reproducible local setup](docs/local-development.md)
using `python3 scripts/dev.py init`, `database`, `migrate`, `seed`, `frontend-build`, and `run`.
Run `python3 scripts/init_data_keys.py` before migrating a new database.
The workflow creates private random credentials and starts the pinned MySQL Compose service.
The manual setup below remains available for an existing local MySQL installation.

1. Install the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) and Node.js 22.22 or later.
2. Set up a MySQL development database and local credentials as described below.
3. From the repository root, restore dependencies and tools:

   ```sh
   dotnet tool restore
   dotnet restore OnlineStore/OnlineStore.csproj
   dotnet build OnlineStore/OnlineStore.csproj --no-restore
   ```

4. Initialize private encryption keys for a **new** database, then apply the initial migration:

   ```sh
   python3 scripts/init_data_keys.py
   ```

   Existing 23-table installations require the documented backed-up reset, not an in-place upgrade.

   ```sh
   dotnet ef database update --project OnlineStore --context StoreDbContext -- --environment Development
   ```

5. Build React and run the backend:

   ```sh
   npm --prefix frontend ci
   npm --prefix frontend run build
   dotnet run --project OnlineStore --launch-profile http
   ```

6. Open `http://localhost:5200`. The React home page is served by ASP.NET Core, and
   `/api/health` returns `{"status":"ok"}`. This endpoint reports application
   liveness; it does not query the database.

## MySQL setup and configuration

Use MySQL 8.0.16 or later, InnoDB tables, `utf8mb4`, and strict SQL mode. The
SCRUM-39 schema uses `utf8mb4_0900_ai_ci` and enforced check constraints; MariaDB
is not supported by this schema.

Have a local MySQL administrator create a dedicated development database named
`onlinestore` with character set `utf8mb4` and collation `utf8mb4_0900_ai_ci`.
Create a separate database user with privileges on that database only. Applying
the initial migration requires permission to create tables and indexes,
references, and migration-history records; subsequent application access needs
SELECT, INSERT, UPDATE, and DELETE. Rolling a migration back (`Down`) also needs
the DROP privilege. Do not point development migration commands
at a shared or production database.

For a local server reached over TCP at `127.0.0.1`, an administrator can run
the following once. Replace both placeholders locally; these are not real
credentials. If the database or account already exists, reuse it instead:

```sql
CREATE DATABASE onlinestore CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;
CREATE USER '<db-user>'@'127.0.0.1' IDENTIFIED BY '<db-password>';
GRANT SELECT, INSERT, UPDATE, DELETE, CREATE, ALTER, INDEX, REFERENCES
    ON onlinestore.* TO '<db-user>'@'127.0.0.1';
```

Store your connection string outside the repository using ASP.NET Core User
Secrets. The project already has a `UserSecretsId`; no `init` command is needed.
Replace the placeholders below locally; do not commit the actual value:

```sh
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=127.0.0.1;Port=3306;Database=onlinestore;User ID=<db-user>;Password=<db-password>" --project OnlineStore
```

User Secrets loads in the Development environment and stores values in your
user profile, outside Git. It is a local development facility, not an encrypted
production secret store. You can also use your IDE's Manage User Secrets UI to
avoid placing a real password in shell history.

For deployment or other environments, supply `ConnectionStrings__DefaultConnection`
through the environment or your platform's secret configuration. The tracked
`appsettings.json` contains an empty connection string.

`MySql:ServerVersion` is the SQL feature target, initially `8.0.16`, the schema's
minimum version. Set it to your team's actual server version using configuration
or `MySql__ServerVersion`. Keep the same target when generating and applying
migrations. An explicit version avoids connecting to MySQL during migration
generation. The application registers the existing `StoreDbContext` as a scoped
service using Pomelo 8.0.3 and EF Core 8.0.32. Missing connection configuration,
malformed strings, missing database names, and unsupported server-version settings stop
startup with a configuration error.

The connection is configured with `DateTimeKind=Utc` to materialize SCRUM-39
timestamps as UTC. Write UTC `DateTime` values. Sensitive-data logging is not
enabled. Registering the context does not open a connection or apply migrations;
database operations connect when needed.

## EF Core migrations

The initial migration and `StoreDbContextModelSnapshot` are included under
`OnlineStore/Data/Migrations`. Do not create a second initial migration.
With configuration supplied, these commands inspect the model without a live
MySQL connection:

```sh
dotnet ef dbcontext info --project OnlineStore --context StoreDbContext -- --environment Development
dotnet ef migrations has-pending-model-changes --project OnlineStore --context StoreDbContext -- --environment Development
dotnet ef migrations script --project OnlineStore --context StoreDbContext --output /tmp/onlinestore-schema.sql -- --environment Development
```

For a future intentional schema change, generate a new migration:

```sh
dotnet ef migrations add <DescriptiveName> --project OnlineStore --context StoreDbContext --output-dir Data/Migrations -- --environment Development
```

Review the generated SQL before running `database update`. Startup does not call
`EnsureCreated` or automatically run migrations. See
[the schema documentation](docs/database-schema.md) for tables, constraints,
relationships, and database verification steps. Database reads/writes and
constraint enforcement need a running MySQL instance; generating a script or
calling `/api/health` does not validate them.

## Product catalog (SCRUM-44)

Open `/products` or pick a category in the header's category bar.
The page loads `GET /api/categories` and `GET /api/products` from SCRUM-43 and
groups active MySQL products by category ID. Both lists are ordered by name.
`GET /api/catalog` remains available for compatible clients. Each category has an anchor link, and each
product shows its name, description, TRY price, stock availability, and image
with a fallback when no image can be loaded. Out-of-stock products remain visible;
inactive products are excluded. Empty categories and an empty catalog have explicit
messages. Database failures return a generic HTTP 503 and the page offers a retry.

The API uses the existing `Categories` and `Products` tables. To fill them with the
sample catalog from `SeedData/seed-products.json`, apply the migrations and run:

```sh
dotnet run --project OnlineStore --launch-profile http -- --seed
```

The command only runs in the Development environment, writes the sample categories
and products, and exits without starting the website. It is safe to run again: a
category with the same name is reused and a product with the same SKU (the seed key) is skipped.
The demo category hierarchy is reconciled, while existing product inventory and prices are preserved. The sample data is
for development and demos only; it is never imported automatically at startup.

For the full 55-product catalog plus four role accounts, orders and review fixtures,
see [demo data setup](docs/demo-data.md).

Run backend integration tests and the existing seed validation tests with:

```sh
dotnet test OnlineStore.Tests/OnlineStore.Tests.csproj
```

Catalog tests host the application with an isolated in-memory database and check
grouping, ordering, inactive and out-of-stock products, empty results, public
response fields, and static assets. A closed local MySQL port checks the HTTP 503
response; these tests do not validate a live MySQL installation.

## Team workflow

See [authentication and role boundaries](docs/authentication.md) for the shared
registration/login APIs, CSRF token flow and trusted staff-role assignment.
See [catalog integration verification](docs/catalog-integration.md) for the
live MySQL/browser check and controlled loading/error/retry scenarios.

- Work is tracked in Jira (project SCRUM) in two-week sprints.
- Commit messages are short and describe the change, for example `Add product list API`.

Checkout and ProductManager delivery integration: [docs/purchasing.md](docs/purchasing.md).

React hosting, publishing and the purchasing routes: [docs/react-hosting.md](docs/react-hosting.md).
