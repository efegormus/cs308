// The customer profile client (SCRUM-125): GET /api/customer/profile.
// The response is checked before it is shown; nothing from an error body is forwarded.
const text = value => typeof value === "string";

export function isCustomerProfile(value) {
  return Number.isSafeInteger(value?.id) && value.id > 0
    && [value.name, value.email, value.taxId, value.homeAddress].every(text);
}

export async function fetchCustomerProfile({ signal } = {}) {
  signal?.throwIfAborted();
  const response = await fetch("/api/customer/profile", { signal, headers: { Accept: "application/json" } });
  if (!response.ok) throw { status: response.status, headers: response.headers };
  let data;
  try { data = await response.json(); } catch { throw { kind: "invalid-response" }; }
  if (!isCustomerProfile(data)) throw { kind: "invalid-response" };
  return data;
}
