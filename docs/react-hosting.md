# React hosting and purchasing migration (SCRUM-108)

The default home page and `/products`, `/product`, `/cart`, `/checkout`,
`/order-confirmation?orderId=...` and `/deliveries` are React routes. ASP.NET
serves the Vite build from `frontend/dist` during local development, and from
`wwwroot/spa` in the published application. Direct navigation and refresh serve
React's entry file, with `no-cache` so deployments discover the latest bundle.
The backend API, authentication, PDF and email contracts remain unchanged.

## Run and publish

```sh
# One server for the demo, using the documented .local development setup
python3 scripts/dev.py frontend-build
python3 scripts/dev.py run
# Open http://localhost:5200

# Optional second terminal for React hot reload
python3 scripts/dev.py frontend
# Open http://localhost:5173 (API and legacy login assets are proxied to 5200)

# Self-contained web content in a normal framework-dependent .NET publication
dotnet publish OnlineStore -c Release -o .local/publish
# Configure the normal database/email environment, then run from .local/publish:
dotnet OnlineStore.dll
```

Publishing runs `npm ci` and `npm run build`. The runtime host needs .NET 8 but
no Node process. `SkipFrontendBuild=true` is an explicit CI optimization requiring
an existing `frontend/dist/index.html`. Backend builds/tests do not require Node.
A missing frontend build produces a safe storefront 503 while API endpoints stay
available. `Frontend:DistPath` can explicitly select a different built-assets
directory; it must contain only public frontend files.

Unknown `/api` paths and missing assets return 404, never an HTML success or the
SPA shell. Existing API endpoints retain their role/auth/CSRF policies. Private
order/customer data is always fetched through those endpoints. Invoice PDFs stay
outside the static web root. Legacy `.html`, JavaScript and CSS files are retained;
`/index.html` is still the old home page but `/` is React. `/login` bridges to the
working legacy login form until that form's React migration is complete. Its
return URL is validated by the existing same-site helper.

## Purchasing behavior

- Checkout waits for the Customer session and guest-cart restoration. Transfer
  uses the existing idempotent merge endpoint with a fresh CSRF token. Stock or
  cart-capacity conflicts preserve selections and are reported in the cart. A
  failed merge blocks checkout until an explicit successful retry.
- Quotes, totals, snapshots and payment results remain server-owned. Card inputs
  are uncontrolled ephemeral fields cleared after each submit, never stored or
  logged. A synchronous submit guard prevents double clicks/StrictMode effects
  from duplicating a checkout POST.
- Retry metadata stores only cart ID, quote hash, UUID and submission state under
  a customer-specific sessionStorage key. Uncertain results retain the UUID and
  recover the existing purchase on reload. Mutation requests never retry on their
  own. Definite 400/402/409 failures permit a fresh reviewed attempt.
- Order confirmation reads the owner's immutable snapshots, current payment and
  fulfillment states, and shows an authorized PDF download or invoice-pending
  message. Changing order IDs cannot grant access to another customer's data.
- Deliveries require ProductManager. Pagination, loading/empty/error states and
  versioned transitions use the existing delivery API. Lost/stale mutation results
  require a read refresh; completed/cancelled or unpaid shipments have no actions.
- Logout/identity changes unmount private page content and cancel pending reads.
  React escapes product names and address snapshots as text.

## Verification

`ReactHostingTests` proves navigation, refresh, asset MIME types, missing build,
API isolation and role guards without a database or Node build. The retained
legacy page checks still verify `/index.html` and legacy catalog assets.

Frontend tests cover response validation, owner-separated retry storage, blocked
storage, preserved uncertain keys, whitelisted checkout payloads, delivery versions
and safe error handling. The browser CI builds React, publishes the complete
application and runs both catalog and purchasing checks against real MySQL.
`verify_purchasing_browser.py` uses temporary local accounts and an existing trusted
role-assignment command; run it only against a disposable development database.
It performs actual mock purchases and consumes sample stock. All fixture email
uses the local transport in CI. Do not supply production database credentials.
