// Pure helpers for the customer profile page (SCRUM-103). No DOM and no network, so Node can test them.
// The limits are the ones in OnlineStore/Accounts/CustomerProfile.cs; the backend trims before it counts.
import { safeReturnUrl, validateHomeAddress, validateName } from "./auth-utils.js";

const PROFILE_PATH = "/profile";
const CHECKOUT_PATH = "/checkout";

/** The editable fields, top to bottom. The email is shown but never sent. */
export const PROFILE_FIELDS = ["name", "taxId", "homeAddress"];

/** The form's starting values: exactly what the server has, so a blank field stays blank (nothing is invented). */
export function profileValues(profile) {
  return Object.fromEntries(PROFILE_FIELDS.map(field => [field, typeof profile?.[field] === "string" ? profile[field] : ""]));
}

/** A legacy account may have no tax ID or home address; checkout refuses it until both are filled in. */
export function isProfileIncomplete(profile) {
  return !String(profile?.taxId ?? "").trim() || !String(profile?.homeAddress ?? "").trim();
}

// Only empty and too long, as the backend checks. A stored tax ID that is not a TC kimlik number (older accounts)
// must not block a name change, so the register page's check digits are not applied here.
export function validateProfileTaxId(value) {
  const taxId = String(value ?? "").trim();
  if (!taxId) return "Enter your tax ID.";
  return taxId.length > 64 ? "Use at most 64 characters." : "";
}

/** One message per field; an empty message means the field is valid. */
export function validateProfile(values) {
  return {
    name: validateName(values.name),
    taxId: validateProfileTaxId(values.taxId),
    homeAddress: validateHomeAddress(values.homeAddress)
  };
}

/** The PUT body: the three fields, trimmed like CustomerProfileValidation.Apply. */
export function profileRequestBody(values) {
  return Object.fromEntries(PROFILE_FIELDS.map(field => [field, String(values[field] ?? "").trim()]));
}

// The backend's own field messages. Anything else from a response body is replaced, so no server text is echoed.
const KNOWN_SERVER_MESSAGE = /^(This field is required\.|Use at most \d{1,4} characters\.|This value is too long when encoded as UTF-8\.)$/;
const FALLBACK_FIELD_MESSAGE = "Check this field.";

/** Field messages from the errors of a 400 answer, for the editable fields only. */
export function profileFieldErrors(errors) {
  const found = {};
  for (const field of PROFILE_FIELDS) {
    const messages = errors?.[field];
    if (!Array.isArray(messages) || messages.length === 0) continue;
    found[field] = typeof messages[0] === "string" && KNOWN_SERVER_MESSAGE.test(messages[0]) ? messages[0] : FALLBACK_FIELD_MESSAGE;
  }
  return found;
}

function pathOf(url) {
  return url.split(/[?#]/, 1)[0].toLowerCase().replace(/\/+$/, "") || "/";
}

/**
 * The "Continue" link after a save: { href, label } for a safe same-site returnUrl, otherwise null.
 * The home page and the profile page itself are not worth a link.
 */
export function continueLink(returnUrl) {
  const href = safeReturnUrl(returnUrl);
  const path = pathOf(href);
  if (path === "/" || path === PROFILE_PATH) return null;
  return { href, label: path === CHECKOUT_PATH ? "Continue to checkout" : "Continue" };
}

/** Where checkout sends a customer whose profile is incomplete: the profile page, coming back afterwards. */
export function profileHref(returnUrl) {
  const safe = safeReturnUrl(returnUrl);
  return safe === "/" ? PROFILE_PATH : `${PROFILE_PATH}?returnUrl=${encodeURIComponent(safe)}`;
}
