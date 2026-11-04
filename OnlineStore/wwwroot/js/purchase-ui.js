import { authReady } from "./auth.js";

export const money = amount => new Intl.NumberFormat("tr-TR", { style: "currency", currency: "TRY" }).format(amount);
export const date = value => value ? new Date(value).toLocaleString() : "Unknown";

export async function requireRole(role) {
  await authReady;
  const account = window.StoreAuth.getAccount();
  if (!account) {
    location.replace(`/login.html?returnUrl=${encodeURIComponent(location.pathname + location.search)}`);
    return false;
  }
  if (account.role !== role) {
    showMessage(`A ${role} account is required for this page.`, true);
    return false;
  }
  // A logout or an account change must also remove private data already rendered on this page.
  window.addEventListener("auth:changed", event => {
    if (event.detail.account?.id !== account.id || event.detail.account?.role !== role) {
      document.querySelector("main").replaceChildren();
      location.replace(`/login.html?returnUrl=${encodeURIComponent(location.pathname + location.search)}`);
    }
  });
  return true;
}

export function showMessage(message, error = false) {
  const element = document.getElementById("message");
  element.textContent = message;
  element.classList.toggle("error", error);
  element.setAttribute("role", error ? "alert" : "status");
  element.hidden = !message;
}

export function text(tag, content, className) {
  const element = document.createElement(tag);
  element.textContent = content;
  if (className) element.className = className;
  return element;
}

export function renderLines(container, items) {
  container.replaceChildren(...items.map(item => {
    const line = text("li", "", "purchase-line");
    line.append(text("span", `${item.name} · ${item.quantity} × ${money(item.unitPrice)}`), text("strong", money(item.total)));
    return line;
  }));
}

export async function api(path, body) {
  const headers = { Accept: "application/json" };
  if (body !== undefined) {
    headers["Content-Type"] = "application/json";
    headers["X-CSRF-TOKEN"] = await window.StoreAuth.csrfToken();
  }
  const response = await fetch(path, {
    method: body === undefined ? "GET" : "POST", headers,
    ...(body === undefined ? {} : { body: JSON.stringify(body) }), signal: AbortSignal.timeout(15000)
  });
  if (!response.ok) {
    let problem;
    try { problem = await response.json(); } catch { /* Use a safe fallback. */ }
    const error = new Error(typeof problem?.title === "string" ? problem.title : "The request failed. Please try again.");
    error.status = response.status;
    throw error;
  }
  return response.json();
}

export function errorMessage(error) {
  if (error.status === 401) return "Your session ended. Log in again before continuing.";
  if (error.status === 403) return "Your account does not have access to this action.";
  if (error.status === 503) return "The service is temporarily unavailable. Try the same request again.";
  return error.status ? error.message : "Could not confirm the result. Check your connection and retry the same request.";
}
