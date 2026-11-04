// Pure helpers for the login page and the shared header. No DOM and no network, so Node can test them.
const LOGIN_PATH = "/login.html";
const REGISTER_PATH = "/register.html";
const MAX_RETURN_URL_LENGTH = 2048;
const DEFAULT_RETRY_SECONDS = 60;
const MAX_RETRY_SECONDS = 3600;

export function isLoginPath(pathname) {
  const path = String(pathname).toLowerCase();
  return path === LOGIN_PATH || path === "/login";
}

// Accepts only a same-site relative path. Anything else falls back to the home page.
export function safeReturnUrl(value) {
  if (typeof value !== "string" || value.length === 0 || value.length > MAX_RETURN_URL_LENGTH) return "/";
  if (!value.startsWith("/") || value.startsWith("//")) return "/";
  // Browsers treat "\" like "/" and drop tabs and newlines, so "/\evil" and "/\t/evil" can both become "//evil".
  if (value.includes("\\") || /[\u0000-\u001f\u007f-\u009f]/.test(value)) return "/";
  // A real page path never needs an encoded slash, backslash, dot, percent sign or control character.
  const path = value.split(/[?#]/, 1)[0];
  if (/%(2f|5c|2e|25|0[0-9a-f]|1[0-9a-f]|7f)/i.test(path)) return "/";

  const base = "https://store.invalid";
  let url;
  try {
    url = new URL(value, base);
  } catch {
    return "/";
  }
  // A signed-in visitor sent back to the login page would be redirected forever.
  if (url.origin !== base || url.pathname.startsWith("//") || isLoginPath(url.pathname)) return "/";
  return value;
}

// Link for the header. On the login page itself it keeps the query, so a returnUrl already there is not lost.
export function loginHref({ pathname, search = "", hash = "" }) {
  if (isLoginPath(pathname)) return `${LOGIN_PATH}${search}`;
  return `${LOGIN_PATH}?returnUrl=${encodeURIComponent(safeReturnUrl(`${pathname}${search}${hash}`))}`;
}

export function registerHref(returnUrl) {
  const safe = safeReturnUrl(returnUrl);
  return safe === "/" ? REGISTER_PATH : `${REGISTER_PATH}?returnUrl=${encodeURIComponent(safe)}`;
}

// Deliberately loose: registration accepts addresses such as user@localhost, and they must still be able to log in.
export function validateEmail(value) {
  const email = String(value ?? "").trim();
  if (!email) return "Enter your email address.";
  return /^[^\s@]+@[^\s@]+$/.test(email) ? "" : "Enter a valid email address.";
}

// The password policy is not enforced or revealed here. Only an empty value is an error.
export function validatePassword(value) {
  return value ? "" : "Enter your password.";
}

export const NETWORK_ERROR = "Could not reach the server. Check your connection and try again.";

const LOGIN_MESSAGES = new Map([
  [400, "Something went wrong. Please try again."],
  [401, "Invalid email or password."],
  [429, "Too many attempts. Please try again in a minute."],
  [503, "Service is temporarily unavailable. Please try again."]
]);

export function loginErrorMessage(status) {
  return LOGIN_MESSAGES.get(status) ?? LOGIN_MESSAGES.get(400);
}

// Whole seconds only. A missing, zero or unreadable header (including the HTTP-date form) uses the default.
export function parseRetryAfter(value) {
  const text = String(value ?? "").trim();
  if (!/^\d+$/.test(text)) return DEFAULT_RETRY_SECONDS;
  const seconds = Number(text);
  return seconds > 0 ? Math.min(seconds, MAX_RETRY_SECONDS) : DEFAULT_RETRY_SECONDS;
}

export function parseAccount(data) {
  if (!data || !Number.isInteger(data.id) || typeof data.name !== "string"
      || typeof data.email !== "string" || typeof data.role !== "string") return null;
  return { id: data.id, name: data.name, email: data.email, role: data.role };
}

// Waits for every hook, for at most timeoutMs in total. A hook that fails or is too slow never blocks the caller.
export async function runHooks(hooks, account, timeoutMs) {
  const work = Promise.allSettled(hooks.map(async hook => hook(account))).then(results => {
    for (const result of results) {
      if (result.status === "rejected") console.warn("A login hook failed.", result.reason);
    }
  });
  let timer;
  const timeout = new Promise(resolve => { timer = setTimeout(resolve, timeoutMs); });
  await Promise.race([work, timeout]);
  clearTimeout(timer);
}
