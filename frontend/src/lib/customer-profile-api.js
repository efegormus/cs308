// The customer profile client: GET (SCRUM-125) and PUT (SCRUM-103) /api/customer/profile.
// The response is checked before it is shown; nothing from an error body is forwarded, except the field messages
// of a 400, which profileFieldErrors (profile-form.js) reduces to known texts.
import { withCsrfRetry } from "./add-to-cart.js";

const text = value => typeof value === "string";

export function isCustomerProfile(value) {
  return Number.isSafeInteger(value?.id) && value.id > 0
    && [value.name, value.email, value.taxId, value.homeAddress].every(text);
}

async function readProfile(response) {
  let data;
  try { data = await response.json(); } catch { throw { kind: "invalid-response" }; }
  if (!isCustomerProfile(data)) throw { kind: "invalid-response" };
  return data;
}

export async function fetchCustomerProfile({ signal } = {}) {
  signal?.throwIfAborted();
  const response = await fetch("/api/customer/profile", { signal, headers: { Accept: "application/json" } });
  if (!response.ok) throw { status: response.status, headers: response.headers };
  return readProfile(response);
}

/**
 * PUT /api/customer/profile with exactly name, taxId and homeAddress (the API rejects anything else).
 * Rejects with { status, headers } for a failed request, { kind: "validation", errors } for a 400 that names fields
 * (no status, so withCsrfRetry does not send it again), or { kind: "invalid-response" }.
 */
export async function updateCustomerProfile({ name, taxId, homeAddress }, { token, signal } = {}) {
  signal?.throwIfAborted();
  const response = await fetch("/api/customer/profile", {
    method: "PUT",
    signal,
    headers: { Accept: "application/json", "Content-Type": "application/json", "X-CSRF-TOKEN": token },
    body: JSON.stringify({ name, taxId, homeAddress })
  });
  if (response.status === 400) {
    let problem = null;
    try { problem = await response.json(); } catch { /* A CSRF failure or a body that is not JSON. */ }
    const errors = problem?.errors;
    if (errors && typeof errors === "object" && Object.keys(errors).length > 0) throw { kind: "validation", errors };
  }
  if (!response.ok) throw { status: response.status, headers: response.headers };
  return readProfile(response);
}

/** Saves with the current CSRF token; a token from before a login or logout is renewed once (withCsrfRetry). */
export function saveCustomerProfile(values, { csrfToken, refreshCsrf, signal }) {
  return withCsrfRetry(token => updateCustomerProfile(values, { token, signal }), csrfToken, refreshCsrf);
}
