> SCRUM-119 storage update: e-mail/tax/address data is encrypted, e-mail lookups use
> a HMAC blind index, and the default address lives in Addresses. Role/session
> protections still apply. See [current storage contract](db-redesign/migration-notes.md).

# Passwords, sessions and roles (SCRUM-48)

The authentication backend supplies the shared foundation for SCRUM-47
(registration/login) and SCRUM-68 (their pages). Those UI tasks remain separate;
use these services/endpoints rather than implementing a second password store.
The existing Users table also holds the SCRUM-71 customer fields.

## Password and session handling

- PasswordService uses the built-in ASP.NET Core Identity PasswordHasher with
  independent random salts and 210,000 PBKDF2 iterations. New passwords must be
  12–128 characters; do not trim them or store/log plaintext. Older valid hashes
  are upgraded after successful verification. Malformed hashes fail login.
- Registration validates name, email, tax ID, home address and password; it
  always creates Customer. Unknown JSON properties (including role/hash) are
  rejected. Duplicate email returns 409; database uniqueness also covers races.
- Login errors use the same generic 401 for unknown accounts/wrong passwords.
  Missing accounts still perform a password-hash verification. Register/login
  share a 10-attempt/minute/IP limiter and return 429 when exhausted.
- Sessions use an encrypted/authenticated HttpOnly, SameSite=Lax cookie, a
  one-hour absolute lifetime, and no persistent “remember me” setting. Secure
  cookies are mandatory outside Development. Use HTTPS in real deployments.
- Role/stamp are loaded from the database when validating a session. Password
  changes, role changes and logout rotate SecurityStamp, revoking existing
  cookies (including replay). Database errors fail closed. Auth/profile responses
  have Cache-Control: no-store and exclude hashes/stamps.
- ASP.NET Core Data Protection keys must be persisted outside source control
  and shared/protected appropriately if multiple application instances are used.
  In-memory rate limiting is per instance; use a shared limit when scaling out.

The session validation follows the framework's
[cookie authentication guidance](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/cookie?view=aspnetcore-8.0).

## HTTP contract

All paths use the same origin as the site, JSON and camelCase properties.

| Endpoint | Access | Response |
| --- | --- | --- |
| GET /api/auth/csrf | Public | 200 `{ "token": "..." }`, plus CSRF cookie |
| POST /api/auth/register | Public + CSRF | Name/email/taxId/homeAddress/password → 201 account; 400 invalid input; 409 duplicate |
| POST /api/auth/login | Public + CSRF | Email/password → 200 account + session cookie; 401 bad credentials |
| GET /api/auth/me, GET /api/me | Authenticated | 200 id/name/email/role/roles; 401 without valid session |
| POST /api/auth/logout | Authenticated + CSRF | 204; revokes sessions and clears cookie |
| GET /api/customer/profile | Customer only | Own id/name/email/taxId/homeAddress; 401/403 otherwise |
| PUT /api/customer/profile | Customer only + CSRF | Name/taxId/homeAddress → 200 own profile (same shape as GET); 400 invalid input or unknown property (`email`, `id`, `role` …); 409 changed at the same time; 401/403 otherwise |
| GET /api/management/session | Any of the three staff roles | 200 current role for management UI bootstrap; customer gets 403 |

`PUT /api/customer/profile` (SCRUM-103) takes exactly `{ "name", "taxId", "homeAddress" }`. The account is
the session's; an ID in the query is ignored and one in the body is an unknown property (400). The email is not
editable: the handler validates the stored email together with the new values through
`CustomerProfileValidation`, so the field limits live in one place, and trims each value as registration does.
A 400 has the same `ValidationProblem` shape as register (`errors.name`, `errors.taxId`, `errors.homeAddress`).
The tax ID and the default address stay in their AES-GCM columns (`Users.TaxIdEnc`, `Addresses.AddressEnc`);
a legacy account without a default address gets one. Checkout answers 409 "Complete your customer profile
before checkout." until name, tax ID and address are valid; the React checkout then opens
`/profile?returnUrl=/checkout`.

Database failures inside account operations return generic 503. During session
validation a database outage invalidates authentication for that request (401 on
protected resources), never granting stale privileges. Cancellation still flows
through CancellationToken. Catalog/health endpoints explicitly allow guests.

`GET /api/me` is the canonical current-account path for new clients;
`GET /api/auth/me` remains a compatibility alias to the same database-backed
handler. Both return the current role in `role` and a one-element `roles` array.
See the [dashboard API contract](api-contract-dashboards.md) for dashboard and
manager-list endpoints.

Every POST and PUT requires the CSRF cookie and X-CSRF-TOKEN header. Fetch a fresh token
after login/logout because the token is bound to the current identity. Example:

```js
const { token } = await fetch('/api/auth/csrf').then(r => r.json());
const response = await fetch('/api/auth/login', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': token },
  body: JSON.stringify({ email, password })
});
```

The synchronizer-token check uses the framework's
[IAntiforgery API](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.antiforgery.iantiforgery.validaterequestasync?view=aspnetcore-8.0).
There are no browser-stored bearer tokens and no wildcard credentialed CORS.

## Frontend contract

`AuthProvider` in `frontend/src/auth/AuthProvider.jsx` is the shared client for this
API. It wraps the whole React app, so a component reads it with `useAuth()`. It checks
`/api/auth/me` once per page load; a guest gets 401, which is normal and shows no error.

```js
const {
  account,    // current { id, name, email, role }, or null (also null before the first /me check)
  authReady,  // true after the first /me check, whatever its result
  csrfToken,  // () => Promise<string> with the latest token; rejects if it cannot be fetched
  onLogin     // (fn) => remove; fn(account) may return a Promise; runs after login, before the redirect
} = useAuth();
```

A component re-renders when `account` changes after login, logout or an expired session.
Send the token as `X-CSRF-TOKEN` on every POST. `csrfToken()` already returns a new
token after login and logout, so do not keep your own copy.

### onLogin hooks (for SCRUM-100)

- Register a hook in an effect, `useEffect(() => onLogin(hook), [onLogin])`, so the
  returned function removes it again when the component unmounts.
- Hooks run after the session and the new CSRF token are ready, and after a Customer's
  guest cart is merged, so a hook can POST straight away with `await csrfToken()`.
- The login waits for all hooks, for at most 5 seconds in total, and then redirects.
  A hook that fails or is too slow never blocks the login, and the redirect may cut
  off its request. A hook's server work must therefore be safe to repeat.

### returnUrl

`/login?returnUrl=<URL-encoded path>` sends the visitor there after login, and
also straight away if they are already signed in. The header's Login link carries the
current path, query and hash. `safeReturnUrl` in `frontend/src/lib/auth-utils.js` accepts only a
same-site path starting with a single `/`, and otherwise falls back to `/`. It rejects:

- `//…`, backslashes and any scheme (`https:`, `javascript:` …)
- control characters and anything longer than 2048 characters
- an encoded slash, backslash, dot, percent sign or control character in the path
- the login page itself, which would redirect forever

### Role landing page (React, SCRUM-130)

After a login the React app sends the visitor to `postLoginPath(account, returnUrl)` in
`frontend/src/lib/role-routes.js`. A safe `returnUrl` that the role may open is used;
anything else (no `returnUrl`, an unsafe one, or a page for another role) goes to the
landing page of the role:

| Role | Landing page |
| --- | --- |
| Customer | `/` (home page; the profile icon in the header opens `/dashboard`) |
| ProductManager, SalesManager | `/manage` |
| SupportAgent | `/support` |

The customer's `/dashboard` page (SCRUM-125) shows the three newest orders from
`GET /api/customer/orders` and the profile from `GET /api/customer/profile`; each section
loads on its own, so one failing request does not hide the other. The profile icon's
menu lists "My account" (the role's own page), the role's shortcuts and Logout.

The same file lists which role may open which page (`canAccess`). It only chooses where
the browser goes; the server still enforces every role. `PurchaseAccess` guards each
private page: it waits for the first `/api/auth/me` check, sends a guest to
`/login?returnUrl=…` and shows a message instead of the page to the wrong role.

Session expiry: a page that makes private requests calls `useSessionExpiry(...errors)`.
A 401 clears the account and the cart merge state, fetches a new CSRF token and sends
the visitor to the login page; the page unmounts, which aborts its requests, so a late
answer cannot bring back the previous user's data. A 403 is a wrong role and is not
treated as an expired session. Logging out on a private page returns to `/`.

### Login errors

| Response | What the page does |
| --- | --- |
| 401 | "Invalid email or password." for a wrong password and for an unknown account; the password is cleared and focused |
| 429 | "Too many attempts. Please try again in a minute."; the button counts down from `Retry-After`, or 60 s if it is missing. The backend does not send this header today, so the countdown is 60 s |
| 503 | "Service is temporarily unavailable. Please try again." |
| 400 | Fetches a new CSRF token; "Something went wrong. Please try again." |
| Network error or 15 s timeout | "Could not reach the server. Check your connection and try again." |

The 12-character password minimum is not checked on login, so the page does not reveal
the password policy.

## Role boundaries for feature owners

New endpoints require authentication by default. Public browsing must opt in
with AllowAnonymous. Map role-specific operations through RolePolicies groups:

| Group helper / prefix | Required role | Feature responsibilities |
| --- | --- | --- |
| MapCustomerApi /api/customer | Customer | Own cart/orders/profile/wishlist/reviews/returns |
| MapSalesManagerApi /api/sales | SalesManager | Prices, discounts, financial reports, invoice exports, refunds |
| MapProductManagerApi /api/product-management | ProductManager | Catalog/category/stock/delivery management and comment moderation |
| MapSupportAgentApi /api/support | SupportAgent | Claim/respond to support conversations and authorized context |

Roles do not inherit each other's privileges. A staff role is not a customer or
another staff role. Ownership checks remain mandatory inside each customer/chat
operation; a role check alone does not authorize arbitrary user IDs. The profile
endpoint resolves its ID from the authenticated session, not from a query value.
Actual checkout/management/chat operations still belong to their backlog items;
the role-group HTTP matrix tests exercise the shared guards using test-only routes.

Staff assignment is a trusted operator command, not a public API. Register an
account first, then, with the intended database configuration loaded:

```sh
dotnet run --project OnlineStore --no-launch-profile -- --set-role person@example.com ProductManager
```

Allowed exact values: Customer, SalesManager, ProductManager, SupportAgent.
Do not expose the command through a web process or let registration accept roles.
No demo staff passwords or real account credentials are seeded or committed.

## React purchasing migration (SCRUM-108)

`AuthProvider` restores the signed-in Customer's guest cart using the fresh
identity's CSRF token before allowing checkout. It exposes merge errors, conflicts
and an explicit retry; the React cart refreshes when the merge completes.
See [React hosting](react-hosting.md).
