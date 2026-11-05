# Shared request states (SCRUM-111)

These additions are independent of routing, authentication and the SCRUM-109 API
client. No existing page is changed. SCRUM-109/110 can import:

- `LoadingState` and `ErrorState` from `src/components/RequestStates.js`.
- `useRequest` from `src/lib/useRequest.js`.
- `normalizeRequestError` from `src/lib/request-errors.js` when adapting client errors.
- `src/styles/request-states.css` once in the consuming module. Its selectors are
  scoped to `.request-state` and use the existing theme tokens. There is no animation.

## Read-only page example

This is a usage example, not another ProductsPage or API client. `loadProducts` is
the SCRUM-109 client function, accepting `{ signal }` and returning validated data.
Keep that function stable (an imported function or `useCallback`).

```jsx
import { useEffect } from "react";
import { LoadingState, ErrorState } from "./components/RequestStates.js";
import { useRequest } from "./lib/useRequest.js";
import "./styles/request-states.css";

export function ProductListRequestExample({ loadProducts }) {
  const { status, data, error, run, cancel } = useRequest();
  const load = () => run(({ signal }) => loadProducts({ signal }), {
    validate: Array.isArray
  });

  useEffect(() => {
    void run(({ signal }) => loadProducts({ signal }), { validate: Array.isArray });
    return cancel;
  }, [loadProducts, run, cancel]);

  return (
    <section aria-label="Products" aria-busy={status === "loading"}>
      {status === "loading" && <LoadingState label="Loading products…" />}
      {status === "error" && <ErrorState error={error} onRetry={load} />}
      {status === "success" && <p>{data.length} products</p>}
    </section>
  );
}
```

`useRequest()` starts idle and makes no requests on its own. `run(task, options)`
invokes `task({ signal })` once and clears previous data/errors. It exposes loading,
success or error. `cancel()` and `reset()` abort pending work and return to idle.
Unmounting aborts work. Older results/errors cannot replace newer state, including
when the executor ignores its signal or React StrictMode replays effects.

The default timeout is 15 seconds. Set a positive `timeoutMs` up to 2,147,483,647,
or `0` to disable it if the client already owns the timeout. Forward the signal
through the API client. A timeout is displayed as an error; cancellation is silent.
Run resolves a state snapshot, or `{ status: "cancelled" }` when superseded/unmounted.
Use rendered state for results rather than triggering side effects from an old run.

## API client boundary and safe errors

The hook does not fetch, parse `Response` bodies, change cookies/CSRF, redirect on
401, or implement an API client. The API client must reject failed HTTP responses,
parse JSON and validate endpoint-specific shapes before returning data. The
optional synchronous `validate(data)` must return `true` to accept a result; it is
an additional guard, not a substitute for endpoint-specific validation.

Pass HTTP failures as `{ status, problem, headers }` or an error with
`response: { status, headers }`. Direct ProblemDetails objects and rejected
`Response` objects are also accepted. Keep the actual HTTP status when a body is
empty, HTML, malformed JSON or conflicts with `problem.status`.

Only fixed local messages are displayed. Server title/detail, validation values,
URLs, trace IDs, exception messages and stacks are neither rendered nor retained
in the error state. This also handles the documented empty/plain-text errors that
SCRUM-60 has not yet standardized. Do not render the original caught error yourself.

Network failures (`TypeError` or `{ kind: "network" }`), timeout (`TimeoutError` or
`{ kind: "timeout" }`), cancellation (`AbortError` or `{ kind: "cancelled" }`) and
invalid responses (`SyntaxError` or `{ kind: "invalid-response" }`) are supported.
Unexpected errors get a generic safe message. A TypeError can also be a programming
error; client adapters should use the explicit kinds when available.

429 errors accept a case-insensitive `Retry-After` header through `Headers` or a
plain object. Valid non-negative delta seconds and IMF-fixdate HTTP dates produce
`retryAfterSeconds`; missing/invalid headers use a generic rate-limit message.
Guidance is limited to 24 hours and headers to 128 characters. Longer delays use
the generic message rather than shortening the server's requested wait.
The displayed delay is guidance at normalization time, not a live countdown.

## Retry and mutations

`ErrorState` shows a retry button only when `onRetry` is a function. `retrying`
disables it. Nothing retries automatically, even for 429, 500, 503 or network errors.
Callers own the decision to offer a retry and any rate-limit waiting period.

For payment/checkout or other mutations, omit `onRetry` unless the feature owner
has implemented a deliberate retry with the same idempotency key and recovery
rules. Never run a mutation from a mount effect. Cancelling or timing out a request
does not prove the server rolled it back: this hook does not authorize a new attempt.

## Verification

`npm test` uses the existing Node runner for safe status mapping, malformed errors,
Retry-After, rendered ARIA/button markup, explicit retries, timeout, out-of-order
completion, cancellation, teardown and StrictMode-style reactivation. The lifecycle
tests exercise the same controller used by the hook; they are not a browser mount
test. Pages remain responsible for endpoint-specific integration and empty states.
