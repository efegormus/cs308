# CS 308 Online Store

Online store for phones and tablets, built for the Sabancı University CS 308 course project.

## Tech stack

- Backend: C# with ASP.NET Core (.NET 8)
- Front end: plain HTML, CSS and JavaScript served from `wwwroot`
- Database: MySQL (Entity Framework Core)
- Version control: Git and GitHub; planning in Jira (Scrum)

## Project structure

```
OnlineStore/          ASP.NET Core project
  Program.cs          application entry point
  Models/             SCRUM-39 entities
  Data/               StoreDbContext, entity mappings, and migrations
  wwwroot/            static front end files
.config/              repository-local EF Core CLI tool
docs/                 database schema documentation
```

## Getting started

1. Install the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).
2. Set up a MySQL development database and local credentials as described below.
3. From the repository root, restore dependencies and tools:

   ```sh
   dotnet tool restore
   dotnet restore OnlineStore/OnlineStore.csproj
   dotnet build OnlineStore/OnlineStore.csproj --no-restore
   ```

4. Apply the provided initial migration to your development database:

   ```sh
   dotnet ef database update --project OnlineStore --context StoreDbContext -- --environment Development
   ```

5. Run the backend:

   ```sh
   dotnet run --project OnlineStore --launch-profile http
   ```

6. Open `http://localhost:5200`. The home page is served from `wwwroot`, and
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
SELECT, INSERT, UPDATE, and DELETE. Do not point development migration commands
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

Open `/products.html` or follow **Browse products by category** from the home page.
The page loads `GET /api/catalog`, which returns categories and their active
products from MySQL, ordered by name. Each category has an anchor link, and each
product shows its name, description, TRY price, stock availability, and image
with a fallback when no image can be loaded. Out-of-stock products remain visible;
inactive products are excluded. Empty categories and an empty catalog have explicit
messages. Database failures return a generic HTTP 503 and the page offers a retry.

The API uses the existing `Categories` and `Products` tables. Apply migrations
and populate these tables in your development database to see products. The
`SeedData/seed-products.json` file is sample data only; it is not automatically
imported or served as live inventory.

Run backend integration tests and the existing seed validation tests with:

```sh
dotnet test OnlineStore.Tests/OnlineStore.Tests.csproj
```

Catalog tests host the application with an isolated in-memory database and check
grouping, ordering, inactive and out-of-stock products, empty results, public
response fields, and static assets. A closed local MySQL port checks the HTTP 503
response; these tests do not validate a live MySQL installation.

## Team workflow

- Work is tracked in Jira (project SCRUM) in two-week sprints.
- Commit messages are short and describe the change, for example `Add product list API`.
