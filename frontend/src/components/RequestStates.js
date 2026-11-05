// Plain JavaScript React elements keep these components testable with the Node runner.
import { createElement as h } from "react";
import { normalizeRequestError } from "../lib/request-errors.js";

export function LoadingState({ label = "Loading…" } = {}) {
  return h("div", {
    className: "request-state request-state--loading", role: "status", "aria-live": "polite", "aria-atomic": true
  }, label);
}

export function ErrorState({ error, onRetry, retrying = false } = {}) {
  if (!error) return null;
  const safe = normalizeRequestError(error);
  if (safe.kind === "cancelled") return null;
  return h("div", { className: "request-state request-state--error", role: "alert", "aria-atomic": true },
    h("p", null, safe.message),
    typeof onRetry === "function" && h("button", {
      type: "button", onClick: onRetry, disabled: retrying
    }, "Try again")
  );
}
