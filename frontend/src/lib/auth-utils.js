// Pure helpers for the login and register pages and the shared header. No DOM and no network, so Node can test them.
const LOGIN_PATH = "/login";
const REGISTER_PATH = "/register";
const MAX_RETURN_URL_LENGTH = 2048;
const DEFAULT_RETRY_SECONDS = 60;
const MAX_RETRY_SECONDS = 3600;

export function isLoginPath(pathname) {
  const path = String(pathname).toLowerCase();
  return path === LOGIN_PATH || path === "/login.html"; // The old page counts until SCRUM-112 removes it.
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

// The register form, top to bottom. confirmPassword is only checked here and is never sent to the API.
export const REGISTER_FIELDS = ["name", "email", "password", "confirmPassword", "taxId", "homeAddress"];
const REGISTER_API_FIELDS = ["name", "email", "password", "taxId", "homeAddress"];

// The limits are the ones in OnlineStore/Accounts/CustomerProfile.cs and PasswordService.cs. The backend trims a
// value before it counts the length, except the password.
export function validateName(value) {
  const name = String(value ?? "").trim();
  if (!name) return "Enter your full name.";
  return name.length > 100 ? "Use at most 100 characters." : "";
}

// The backend's check is stricter than validateEmail. Its 400 answer is shown under the field.
export function validateRegisterEmail(value) {
  const message = validateEmail(value);
  if (message) return message;
  return String(value).trim().length > 254 ? "Use at most 254 characters." : "";
}

export function validateNewPassword(value) {
  const length = String(value ?? "").length;
  return length >= 12 && length <= 128 ? "" : "Use a password between 12 and 128 characters.";
}

export function validateConfirmPassword(password, confirmPassword) {
  if (!confirmPassword) return "Confirm your password.";
  return password === confirmPassword ? "" : "Passwords do not match.";
}

// TC kimlik number: 11 digits, the first is not 0, and the last two are check digits. The backend only requires a
// value of at most 64 characters for now.
export function validateTaxId(value) {
  const taxId = String(value ?? "").trim();
  if (!taxId) return "Enter your tax ID.";
  const invalid = "Enter a valid 11-digit TC kimlik number.";
  if (!/^[1-9][0-9]{10}$/.test(taxId)) return invalid;
  const digits = [...taxId].map(Number);
  const odd = digits[0] + digits[2] + digits[4] + digits[6] + digits[8];
  const even = digits[1] + digits[3] + digits[5] + digits[7];
  // odd * 7 - even can be negative, and % keeps the sign in JavaScript.
  const tenth = (((odd * 7 - even) % 10) + 10) % 10;
  const eleventh = (odd + even + digits[9]) % 10;
  return digits[9] === tenth && digits[10] === eleventh ? "" : invalid;
}

export function validateHomeAddress(value) {
  const address = String(value ?? "").trim();
  if (!address) return "Enter your home address.";
  return address.length > 1000 ? "Use at most 1000 characters." : "";
}

// Returns one message per field. An empty message means the field is valid.
export function validateRegistration(values) {
  return {
    name: validateName(values.name),
    email: validateRegisterEmail(values.email),
    password: validateNewPassword(values.password),
    confirmPassword: validateConfirmPassword(values.password, values.confirmPassword),
    taxId: validateTaxId(values.taxId),
    homeAddress: validateHomeAddress(values.homeAddress)
  };
}

// Reads the field messages from a 400 answer of POST /api/auth/register. A CSRF failure has none, so it gives {}.
export function registerFieldErrors(problem) {
  const errors = {};
  for (const field of REGISTER_API_FIELDS) {
    const messages = problem?.errors?.[field];
    if (Array.isArray(messages) && typeof messages[0] === "string" && messages[0]) errors[field] = messages[0];
  }
  return errors;
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
