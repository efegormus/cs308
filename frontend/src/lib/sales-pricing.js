// Logic of the sales manager's pricing and discount screen (SCRUM-156). Pure, so Node can test it.
// The server decides everything that counts (docs/sales-pricing-api.md); this only validates and previews.

export const MAX_PRICE = 9_999_999.99;
export const MAX_DISCOUNT_PRODUCTS = 200;
export const MAX_NAME_LENGTH = 100;

/** A product with a list price of 0 has no price yet. (The API does not hide such products from shoppers yet.) */
export const isUnpriced = product => product?.listPrice === 0;

const decimal = /^\d+(?:[.,]\d+)?$/;
const normalize = text => String(text ?? "").trim().replace(",", ".");

/** The price typed by the manager: above 0, at most 2 decimals, at most MAX_PRICE. Returns a number or null. */
export function parsePrice(text) {
  const value = normalize(text);
  if (!decimal.test(value)) return null;
  const [, fraction = ""] = value.split(".");
  if (fraction.length > 2) return null;
  const price = Number(value);
  return price > 0 && price <= MAX_PRICE ? price : null;
}

/** A percentage such as "20" or "12,5" as a rate (0.2, 0.125): above 0, below 100, at most 2 decimals. Or null. */
export function parsePercent(text) {
  const value = normalize(text);
  if (!decimal.test(value)) return null;
  const [, fraction = ""] = value.split(".");
  if (fraction.length > 2) return null;
  const percent = Number(value);
  if (!(percent > 0 && percent < 100)) return null;
  return Math.round(percent * 100) / 10000;
}

/**
 * The price payable under a discount, with the server's rule: floor(listPrice * (1 - rate) * 100 + .5) / 100.
 * Integer arithmetic (cents and basis points), so float noise never moves a half cent.
 */
export function discountedPrice(listPrice, rate) {
  const cents = Math.round(listPrice * 100);
  const basisPoints = Math.round(rate * 10000);
  return Math.floor((cents * (10000 - basisPoints) + 5000) / 10000) / 100;
}

/** One line per selected product: the list price now and the price shoppers would pay with this rate. */
export function previewRows(products, selectedIds, rate) {
  const chosen = new Set(selectedIds);
  return products.filter(product => chosen.has(product.id)).map(product => ({
    id: product.id, name: product.name, listPrice: product.listPrice,
    newPrice: rate === null ? null : discountedPrice(product.listPrice, rate)
  }));
}

/** The product list for a view: "unpriced" or "all", narrowed by a name/model search (case-insensitive). */
export function visibleProducts(products, { view = "all", search = "" } = {}) {
  const needle = search.trim().toLowerCase();
  return products.filter(product => (view !== "unpriced" || isUnpriced(product))
    && (needle === "" || `${product.name} ${product.model}`.toLowerCase().includes(needle)));
}

/** Toggles one id in a selection, keeping it a list without duplicates. */
export function toggleSelection(selectedIds, id) {
  return selectedIds.includes(id) ? selectedIds.filter(existing => existing !== id) : [...selectedIds, id];
}

/**
 * Checks the discount form and builds the request body. `endsAt` and `startsAt` are datetime-local values
 * ("2026-11-30T23:59", in the manager's own time zone). Returns { ok: true, request } or { ok: false, errors }.
 */
export function buildDiscountRequest({ name, percent, startsAt, endsAt, productIds }, now = new Date()) {
  const errors = {};
  const trimmed = String(name ?? "").trim();
  if (trimmed === "" || trimmed.length > MAX_NAME_LENGTH) errors.name = `Use a name of 1 to ${MAX_NAME_LENGTH} characters.`;
  const rate = parsePercent(percent);
  if (rate === null) errors.rate = "Use a percentage above 0 and below 100, with at most 2 decimals.";
  const end = parseLocal(endsAt);
  const start = startsAt ? parseLocal(startsAt) : null;
  if (end === null) errors.endsAt = "Give the date and time the discount ends.";
  else if (end <= now) errors.endsAt = "The discount must end in the future.";
  if (startsAt && start === null) errors.startsAt = "Use a valid start date and time, or leave it empty to start now.";
  else if (start !== null && end !== null && end <= start) errors.endsAt = "The discount must end after it starts.";
  if (productIds.length === 0) errors.productIds = "Select at least one product.";
  else if (productIds.length > MAX_DISCOUNT_PRODUCTS) errors.productIds = `Select at most ${MAX_DISCOUNT_PRODUCTS} products.`;
  if (Object.keys(errors).length > 0) return { ok: false, errors };
  return {
    ok: true,
    request: { name: trimmed, rate, ...(start === null ? {} : { startsAt: start.toISOString() }),
      endsAt: end.toISOString(), productIds: [...productIds].sort((a, b) => a - b) }
  };
}

function parseLocal(value) {
  if (typeof value !== "string" || !/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(?::\d{2})?$/.test(value)) return null;
  const date = new Date(value);
  return Number.isFinite(date.getTime()) ? date : null;
}

/** Messages for the failures the pricing endpoints can return; nothing from the error body is shown. */
export function pricingFailureMessage(error) {
  switch (error?.status) {
    case 400: return "The server rejected these values. Check the products are active and the dates are valid.";
    case 401: return "Your session ended. Log in again before continuing.";
    case 403: return "Only a sales manager account can change prices and discounts.";
    case 404: return "That product or discount no longer exists. The list was refreshed.";
    default: return "The change could not be saved. Please try again.";
  }
}
