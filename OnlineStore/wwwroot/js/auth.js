// Shared by every page: header login state and the window.StoreAuth contract (docs/authentication.md).
import { loginHref, parseAccount, runHooks } from "./auth-utils.js";

const REQUEST_TIMEOUT_MS = 10000;
const LOGIN_HOOK_LIMIT_MS = 5000;

let account = null;
let tokenRequest = null;
const loginHooks = [];

async function fetchToken() {
  try {
    const response = await fetch("/api/auth/csrf", {
      headers: { Accept: "application/json" }, signal: AbortSignal.timeout(REQUEST_TIMEOUT_MS)
    });
    if (!response.ok) throw new Error("CSRF request failed.");
    const { token } = await response.json();
    if (typeof token !== "string" || !token) throw new Error("Invalid CSRF response.");
    return token;
  } catch (error) {
    tokenRequest = null; // The next caller tries again.
    throw error;
  }
}

function csrfToken() {
  tokenRequest ??= fetchToken();
  return tokenRequest;
}

// The token is bound to the current identity, so it changes after login and logout.
function invalidateCsrf() {
  tokenRequest = null;
}

// Never rejects.
export async function refreshCsrf() {
  invalidateCsrf();
  try {
    await csrfToken();
  } catch {
    // csrfToken() tries again the next time something needs a token.
  }
}

function announce() {
  window.dispatchEvent(new CustomEvent("auth:changed", {
    detail: { authenticated: account !== null, account }
  }));
}

function renderHeader() {
  const link = document.getElementById("auth-link");
  const name = document.getElementById("user-name");
  const logoutButton = document.getElementById("logout");
  const signedIn = account !== null;
  if (link) {
    link.hidden = signedIn;
    link.href = loginHref(window.location);
  }
  if (name) {
    name.hidden = !signedIn;
    name.textContent = signedIn ? account.name : "";
    name.title = signedIn ? account.name : "";
  }
  if (logoutButton) logoutButton.hidden = !signedIn;
}

async function checkSession() {
  try {
    const response = await fetch("/api/auth/me", {
      headers: { Accept: "application/json" }, signal: AbortSignal.timeout(REQUEST_TIMEOUT_MS)
    });
    // 401 is the normal answer for a guest.
    account = response.ok ? parseAccount(await response.json()) : null;
  } catch {
    account = null;
  }
  renderHeader();
  announce();
}

// Used by login.js after a 200 from POST /api/auth/login. Never rejects.
export async function completeLogin(signedInAccount) {
  account = signedInAccount;
  await refreshCsrf(); // Before the hooks, because they may POST with the new token.
  renderHeader();
  announce();
  await runHooks(loginHooks, account, LOGIN_HOOK_LIMIT_MS);
}

async function postLogout(mayRetry = true) {
  const response = await fetch("/api/auth/logout", {
    method: "POST",
    headers: { "X-CSRF-TOKEN": await csrfToken() },
    signal: AbortSignal.timeout(REQUEST_TIMEOUT_MS)
  });
  // A token from before a login in another tab is stale. Get a new one and try once more.
  if (response.status === 400 && mayRetry) {
    invalidateCsrf();
    return postLogout(false);
  }
  return response.status;
}

async function logout(button) {
  button.disabled = true;
  button.textContent = "Logout";
  try {
    const status = await postLogout();
    // 401 means the session had already ended, so the visitor is signed out either way.
    if (status !== 204 && status !== 401) throw new Error("Logout failed.");
    account = null;
    await refreshCsrf();
    renderHeader();
    document.getElementById("auth-link")?.focus();
    announce();
  } catch {
    button.textContent = "Logout failed, retry";
  } finally {
    button.disabled = false;
  }
}

window.StoreAuth = {
  getAccount: () => account,
  csrfToken,
  onLogin(fn) {
    if (typeof fn === "function") loginHooks.push(fn);
  }
};

const logoutButton = document.getElementById("logout");
if (logoutButton) logoutButton.addEventListener("click", () => logout(logoutButton));
// Clicking a category link changes the hash without loading a page, and the login link must follow it.
window.addEventListener("hashchange", renderHeader);

// Resolves after the first /me check, whatever its result. Never rejects.
export const authReady = checkSession();
