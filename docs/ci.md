# Continuous integration — SCRUM-59, extended by SCRUM-106

GitHub Actions builds and tests the project automatically. The workflow is
`.github/workflows/ci.yml`; the MySQL helper it uses is
`.github/actions/start-mysql/action.yml`.

## When it runs

- a pull request is opened, updated with new commits, or reopened (any target branch);
- commits are pushed to `main`, including when a pull request is merged.

There are no path filters, so every job reports on every pull request (a required
check that is skipped would stay "pending" forever). A newer push to a pull request
cancels the older run of the same branch; runs on `main` are never cancelled.

## Jobs

All jobs run in parallel on a fresh `ubuntu-24.04` runner. The image is pinned on purpose:
when GitHub moved `ubuntu-latest` to Ubuntu 26.04, `playwright install --with-deps chromium`
(Playwright 1.60) stopped working there and `Browser end-to-end` failed before any test ran.
Move to a newer image only together with a Playwright version that supports it.

| Job (check name) | What it proves | Fails when | Blocking |
| --- | --- | --- | --- |
| `Backend build and test` | The solution compiles and the xUnit suite passes (EF Core in-memory database). | compile error or failing test | Yes |
| `MySQL integration tests` | Migrations, constraints, foreign keys, seed loader and the schema tests work on a real MySQL 8.4.13. | failing test, **or any MySQL test skipped**, or no test ran | Yes |
| `Browser end-to-end` | The published ASP.NET app serves the built React catalog and purchasing pages against seeded MySQL in Chromium, including protected PDF and lost-response recovery. | migration/seed/start-up error, API check or any browser assertion fails | Yes |
| `Frontend and script checks` | React unit tests (`*.test.js` under `frontend/src`, Node test runner) and build, `scripts/test_*.py`, and `actionlint` on the workflows. | build error, failing test, bad workflow file | Yes |
| `Security hygiene` | `gitleaks` scan of the git history and a report of vulnerable NuGet packages. | a secret or vulnerable package is found | No (report only, `continue-on-error`) |

On a pull request the checks are listed as `CI / <job name>`.

### How the MySQL jobs avoid secrets

`start-mysql` starts `mysql:8.4.13` (the version pinned in `compose.yaml`, with the same
character set and SQL mode) bound to `127.0.0.1:3306` (the `compose.yaml` default). The password is generated with
`openssl rand` for every run, masked in the logs and passed to later steps through
`MYSQL_APP_PASSWORD`. No repository secret is needed and nothing is committed. The tests
delete rows, which is why the database is named `onlinestore_tests`: the test fixture
refuses every other name.

### Why "no skipped test" is checked

`MySqlIntegrationTests` skip themselves when `ONLINESTORE_TEST_MYSQL` is not set, and
`dotnet test` still exits with 0. `scripts/ci_require_executed.py` reads the TRX report and
fails when no test passed or any test was skipped, so a green MySQL job really means the
tests ran on MySQL.

## Required checks

Branch protection is not configured yet. A repository admin can add these after each has run once
in the repository (a check can only be selected after its
first run):

| Check name | Required |
| --- | --- |
| `Backend build and test` | Yes |
| `MySQL integration tests` | Yes |
| `Browser end-to-end` | Yes |
| `Frontend and script checks` | Yes |
| `Security hygiene` | No, until its findings are triaged; then remove `continue-on-error` and require it |

## Running the same commands locally

```sh
# Backend (no MySQL needed)
dotnet restore cs308.sln
dotnet build cs308.sln --no-restore
dotnet test cs308.sln --no-build

# MySQL tests: see "Real MySQL integration tests" in docs/local-development.md
dotnet test cs308.sln --filter FullyQualifiedName~MySql

# Browser end-to-end, with the app running and seeded (docs/local-development.md)
python3 -m pip install -r scripts/requirements-browser.txt
python3 scripts/verify_catalog_browser.py --expect-seeded
# Disposable local DB only: needs the server connection environment for the trusted fixture role command
python3 scripts/verify_purchasing_browser.py --operator-assembly <published-app>/OnlineStore.dll

# Frontend and scripts
(cd frontend && npm ci && npm test && npm run build)
python3 -I -m unittest discover -s scripts -p 'test_*.py'
```

Put frontend unit tests next to the code as `frontend/src/<folder>/<name>.test.js`;
`npm test` (Node test runner) picks them up automatically.

## Secrets

No GitHub secrets are required. The workflow uses only the automatic `GITHUB_TOKEN`, limited to
read access by `permissions: contents: read`. The connection string inside `CatalogTests.cs`
is a dummy value for a closed port, not a credential. Never commit a connection string or
password.

## Suggested branch protection (for a repository admin)

This is a proposal only; it has not been configured. A repository admin can add it under
Settings → Rules → Rulesets (or Settings → Branches) for `main`:

- Require a pull request before merging, with at least 1 approval.
- Require status checks to pass: the required checks listed above.
- Require branches to be up to date before merging, so the checks run against the latest `main`.
- Block force pushes and branch deletion.
- Do not allow bypassing these rules, including for admins.
