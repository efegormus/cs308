# Continuous integration — SCRUM-59

The backend is built and tested automatically by GitHub Actions. The workflow
is defined in `.github/workflows/ci.yml`. It only runs the existing tests in
`OnlineStore.Tests`; it does not add or change tests.

## What the workflow does

The workflow runs when:

- a pull request is opened, updated with new commits, or reopened (any target branch);
- commits are pushed to `main`, including when a pull request is merged.

It has one job, **Backend build and test**, on a fresh `ubuntu-latest` runner:

| Step | Command | Fails when |
| --- | --- | --- |
| Check out repository | `actions/checkout` | the code cannot be fetched |
| Set up .NET 8 SDK | `actions/setup-dotnet`, version `8.0.x` | the SDK cannot be installed |
| Restore dependencies | `dotnet restore cs308.sln` | a NuGet package cannot be downloaded |
| Build | `dotnet build cs308.sln --no-restore` | there is a compile error |
| Test | `dotnet test cs308.sln --no-build` | any xUnit test fails |

The steps run in order and the job stops at the first failure. The pull
request page shows the job as passed or failed, and the Actions tab shows the
log of each step. The job is cancelled if it runs longer than 15 minutes.

## Required check

| Check name | Workflow | Must pass before merging into `main` |
| --- | --- | --- |
| `Backend build and test` | `CI` | Yes |

On a pull request the check is listed as `CI / Backend build and test`.

## Running the same commands locally

Install the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0),
then run from the repository root:

```sh
dotnet restore cs308.sln
dotnet build cs308.sln --no-restore
dotnet test cs308.sln --no-build
```

The tests do not need MySQL, User Secrets, or environment variables. Catalog
tests replace MySQL with an EF Core in-memory database, and one test uses a
closed local port (`127.0.0.1:1`) to check the HTTP 503 response. Seed tests
read `SeedData/seed-products.json` relative to the default build output folder,
so do not change the output path with `--output`.

Running the application itself still needs a MySQL database and a connection
string; see the README.

## Secrets

No GitHub secrets are required. The workflow uses only the automatic
`GITHUB_TOKEN`, limited to read access by `permissions: contents: read`. The
connection string inside `CatalogTests.cs` is a dummy value for a closed port,
not a credential.

If a future test needs a real MySQL database, add a `mysql:8` service container
with a health check to the job, store its password as a repository secret
(Settings → Secrets and variables → Actions), and pass the connection string to
the test step as `ConnectionStrings__DefaultConnection` using
`${{ secrets.<NAME> }}`. Never commit a connection string or password. GitHub
does not pass secrets to pull requests opened from forks.

## Suggested branch protection (for a repository admin)

This is a proposal only; it has not been configured. A repository admin can
add it under Settings → Rules → Rulesets (or Settings → Branches) for `main`:

- Require a pull request before merging, with at least 1 approval.
- Require status checks to pass: `Backend build and test`.
- Require branches to be up to date before merging, so the check runs against
  the latest `main`.
- Block force pushes and branch deletion.
- Do not allow bypassing these rules, including for admins.

A check can only be selected after it has run in the repository at least once,
so add the rule after the first CI run, for example on the SCRUM-59 pull
request.
