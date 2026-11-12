> SCRUM-119 uses a fresh 29-table baseline. Existing 23-table databases need the
> backed-up reset using `scripts/redesign_db.py reset --reset-local`; do not apply it as an in-place upgrade.

# Reproducible local development (SCRUM-53)

Prerequisites: Git, the .NET 8 SDK, Python 3.9+, and either **Docker with Compose
v2 and a running engine**, or an existing local MySQL 8.0.16+ server.
Install Node.js 22.22+ and npm for React. ASP.NET Core serves the built React app
and the API from the same origin. See [React hosting](react-hosting.md).

## Docker route: fresh clone

From the repository root:

```sh
python3 scripts/dev.py init
python3 scripts/init_data_keys.py
python3 scripts/dev.py database
dotnet restore cs308.sln
python3 scripts/dev.py migrate
python3 scripts/dev.py seed
python3 scripts/dev.py frontend-build
python3 scripts/dev.py run
```

Open http://localhost:5200/products. React routes such as `/checkout` can be refreshed directly. In another terminal:

```sh
python3 scripts/dev.py check
dotnet test cs308.sln
python3 -m unittest discover -s scripts -p 'test_*.py'
```

`check` queries database-backed categories/products, not only `/api/health`.
After seeding a fresh database the sample has 10 top-level departments, 10 subcategories and 55 products,
including sold-out examples. A second seed run adds 0/0 and keeps existing rows.

The helper creates independent random application/root passwords in ignored
`.local/` files with private permissions. It passes the application connection
string through the child environment; it never prints secrets or places them
in command arguments. `init` is idempotent and refuses partial/mismatched
configuration instead of overwriting credentials for an existing database.
New files use LF endings. For existing CRLF application-password files, the
helper preserves the trailing carriage return used by the MySQL image when it
reads the secret; do not normalize or replace a secret for a database that
already has data.

Compose pins `mysql:8.4.13`, uses file-backed secrets, waits for an authenticated
SELECT 1 health check, binds only 127.0.0.1 and persists data in `mysql-data`.
Its image configuration follows the
[official MySQL image](https://hub.docker.com/_/mysql) (`*_PASSWORD_FILE`).
See [MySQL downloads](https://dev.mysql.com/downloads/mysql/8.4.html) for native
server distributions. The application's existing Pomelo configuration is reused.

To use a different host port before initialization:

```sh
python3 scripts/dev.py init --port 33306
```

The helper passes MYSQL_PORT to Compose. When invoking Compose directly, use the
same value: `MYSQL_PORT=33306 docker compose ...`. Changing the host port later
requires editing `.local/dev.json` and recreating the service; stored credentials
must still match its persistent volume.

Stop the services with `python3 scripts/dev.py stop`; database contents remain.
Do not delete `.local/` while retaining a database volume that uses those secrets.
Changing secret files does not rotate the password inside an initialized MySQL
volume. No database/user credentials or staff passwords are committed.

## Existing MySQL route

Use the database/user provisioning and User Secrets instructions in the
[README](../README.md#mysql-setup-and-configuration). Then:

```sh
python3 scripts/init_data_keys.py
dotnet tool restore
dotnet restore cs308.sln
dotnet ef database update --project OnlineStore -- --environment Development
dotnet run --project OnlineStore --launch-profile http -- --seed
npm --prefix frontend ci
npm --prefix frontend run build
dotnet run --project OnlineStore --launch-profile http
python3 scripts/dev.py check
```

User Secrets load in Development only. Outside that environment supply
ConnectionStrings__DefaultConnection and MySql__ServerVersion through a secret
manager/environment. Do not use the helper against a shared or production server.
Migrations are explicit; ordinary startup does not create tables or seed data.

## Real MySQL integration tests

The normal suite uses InMemory and separately tests database failure paths. It
does not enforce SQL constraints. MySqlIntegrationTests also apply the migration
chain, run the seed loader twice, preserve modified inventory, and exercise actual
MySQL FK/check/unique/length/delete and historical-price behavior.

Create a dedicated disposable local database with a name ending in `_tests` and
give a test-only user migration/read/write permissions on that database. Set
`ONLINESTORE_TEST_MYSQL` to its connection string through your local secret
configuration, then run:

```sh
dotnet test cs308.sln --filter FullyQualifiedName~MySqlIntegrationTests
```

These tests **delete rows** from the named database. They refuse non-local hosts
and database names without the `_tests` suffix. They are explicitly skipped when
the variable is absent, so a normal unit run cannot be mistaken for MySQL proof.

## Troubleshooting

| Symptom | Check |
| --- | --- |
| docker not found / daemon unavailable | Install Docker with Compose v2 and start its engine; `docker info` must succeed |
| Port 3306 already used | Choose another host port at init, or stop the conflicting local service |
| MySQL unhealthy | `docker compose ps` and `docker compose logs mysql`; allow first-start initialization to finish |
| Access denied after editing secrets | Secret files and initialized volume disagree; restore the original secrets or intentionally rotate the database user password |
| Missing connection configuration | Use the helper, or load User Secrets in Development / the documented environment variable |
| Table does not exist / API 503 | Apply all migrations; confirm database/server version and MySQL health |
| Empty catalog | Run the explicit seed command in Development and query `/api/products` |
| --seed exits with database error | Confirm credentials, connectivity and migration state; errors intentionally omit connection details |
| Session not retained outside Development | Use HTTPS; production session cookies are Secure |
| Staff permission denied | Use the trusted role command (`python3 scripts/dev.py role email@example.com ProductManager`), then log in again |
| Browser API blocked by CORS | Open the page from ASP.NET Core, not file:// or an unrelated static server. Same-origin development needs no CORS |

For schema design, auth contracts and future work see the other documents linked
from the README. Never copy actual `.local` secrets into issues or commits.

## React development and publishing (SCRUM-108)

For hot reload, keep the backend on port 5200 in one terminal and run
`python3 scripts/dev.py frontend` in a second after installing frontend dependencies.
Open http://localhost:5173. Vite proxies `/api` and the temporary legacy login
assets to ASP.NET, preserving same-origin session/CSRF cookies.

For a demo with one server, run `python3 scripts/dev.py frontend-build` then
`python3 scripts/dev.py run` and use port 5200. Rebuild/restart after changing
frontend source. The frontend helper commands do not read or pass database secrets.

`dotnet publish OnlineStore -c Release -o .local/publish` runs the locked npm build and
packages it under `wwwroot/spa`. Start `dotnet OnlineStore.dll` from the `.local/publish` output
directory with your normal environment configuration. Node is needed at build
time, not on the runtime host. CI can reuse an already built frontend with
`/p:SkipFrontendBuild=true`; missing `dist/index.html` then fails publishing.

Legacy HTML files remain accessible during migration, including the working
login form used by `/login`. They are no longer the default home page. Without
a React build, API endpoints still work and storefront routes return a safe 503.
